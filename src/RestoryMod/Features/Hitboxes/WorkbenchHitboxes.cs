using System.Collections.Generic;
using HarmonyLib;
using Restory.Constants;
using Restory.Data.Elements;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment.Ultrasonic;
using Restory.Gameplay.Workplace;
using RestoryMod.Core;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace RestoryMod.Features.Hitboxes;

/// <summary>One thing the hover ray can hit, with this frame's hitbox on screen.</summary>
internal sealed class WorkbenchThing(Object identity, ThingKind kind, string name)
{
    /// <summary>What the game hovers for it: a part, screw slot, part ghost or switch button; a collider otherwise.</summary>
    public Object Identity { get; } = identity;

    public ThingKind Kind { get; } = kind;
    public string Name { get; } = name;
    public List<Collider> Colliders { get; } = new();

    /// <summary>The thing as the minimum hitbox size sees it: native hitbox, enlargeable, seen.</summary>
    public HitboxThing<WorkbenchThing> Sizing { get; set; } = null!;

    public Hitbox Native => Sizing.Hitbox;

    /// <summary>Distance from the camera, to list things front to back.</summary>
    public float Depth { get; set; }

    /// <summary>Screen rectangle around the native hitbox, in pixels from the bottom left.</summary>
    public Rect ScreenBounds { get; set; }

    /// <summary>Some shape is drawn from its bounding box rather than its true outline (a mesh).</summary>
    public bool Approximate { get; set; }

    /// <summary>A colour that stays the same for the same thing from frame to frame.</summary>
    public Color Colour => Color.HSVToRGB(Mathf.Repeat(Identity.GetInstanceID() * 0.618034f, 1f), 0.8f, 1f);
}

/// <summary>
/// The things on the workbench the hover ray can hit: the placed device and the loose parts on the
/// bench, grouped the way the game identifies a ray hit, each with its native hitbox on screen and
/// whether the camera can see it.
/// </summary>
internal sealed class WorkbenchHitboxes
{
    /// <summary>The game's hover ray length, in metres (a literal in its cursor detector).</summary>
    public const float GameRayLength = 4f;

    private const float RescanSeconds = 0.25f;

    private static AccessTools.FieldRef<ElementSocket, ElementProjection>? screwSlotOf;
    private static AccessTools.FieldRef<DeviceService, WorkSurface>? workSurfaceOf;

    /// <summary>A collider and the thing it belongs to. Kind is null for a part, whose kind changes as it is worked on.</summary>
    private readonly struct Entry(Collider collider, Object identity, ThingKind? kind, string name)
    {
        public Collider Collider { get; } = collider;
        public Object Identity { get; } = identity;
        public ThingKind? Kind { get; } = kind;
        public string Name { get; } = name;
    }

    private readonly List<Entry> entries = new();
    private readonly List<Collider> gathered = new();
    private float nextScan;

    private readonly Dictionary<Object, WorkbenchThing> byIdentity = new();
    private readonly Dictionary<Collider, WorkbenchThing> byCollider = new();
    private readonly List<WorkbenchThing> found = new();
    private readonly List<HitboxThing<WorkbenchThing>> sizings = new();
    private readonly List<List<NVector2>> shapes = new();
    private readonly RaycastHit[] viewHits = new RaycastHit[16];
    private readonly List<RayHit<WorkbenchThing>> rayHits = new();

    /// <summary>Resolves the private members the listing needs; for the patches' Prepare().</summary>
    public static bool Prepare()
    {
        if (!GameTargets.AllPresent(PatchTargets.SocketScrewSlot, PatchTargets.DeviceServiceWorkSurface)) return false;
        screwSlotOf ??= AccessTools.FieldRefAccess<ElementSocket, ElementProjection>(PatchTargets.SocketScrewSlot.MemberName!);
        workSurfaceOf ??= AccessTools.FieldRefAccess<DeviceService, WorkSurface>(PatchTargets.DeviceServiceWorkSurface.MemberName!);
        return true;
    }

    /// <summary>This frame's things on screen, front to back.</summary>
    public List<WorkbenchThing> Things { get; } = new();

    /// <summary>This frame's things as the minimum hitbox size sees them, front to back.</summary>
    public IReadOnlyList<HitboxThing<WorkbenchThing>> Sizings => sizings;

    /// <summary>
    /// Works out this frame's things. With <paramref name="near"/>, only things whose hitbox could
    /// reach that screen point once enlarged by up to <paramref name="reach"/> px.
    /// </summary>
    public void Update(Camera camera, int mask, DeviceService? devices, Vector2? near = null, float reach = 0f)
    {
        if (Time.unscaledTime >= nextScan) Rescan(devices);

        var nearRay = near is { } point ? camera.ScreenPointToRay(point) : default;
        // World size of one pixel per metre of depth, for the near test.
        var pixelPerDepth = 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2f) / Screen.height;

        found.Clear();
        byIdentity.Clear();
        foreach (var entry in entries)
        {
            var collider = entry.Collider;
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if ((mask & (1 << collider.gameObject.layer)) == 0) continue;

            if (!byIdentity.TryGetValue(entry.Identity, out var thing))
            {
                thing = new WorkbenchThing(entry.Identity, entry.Kind ?? KindOf((ElementBase)entry.Identity), entry.Name);
                byIdentity.Add(entry.Identity, thing);
                found.Add(thing);
            }
            thing.Colliders.Add(collider);
        }

        // Only things on screen (and near, when asked) stay findable, so everything found has a hitbox.
        Things.Clear();
        byIdentity.Clear();
        byCollider.Clear();
        var origin = camera.transform.position;
        foreach (var thing in found)
        {
            if (near is not null && !NearRay(thing, nearRay, reach, pixelPerDepth)) continue;
            if (!Measure(camera, thing)) continue;

            var depth = float.MaxValue;
            foreach (var collider in thing.Colliders)
            {
                depth = Mathf.Min(depth, Vector3.Distance(origin, collider.bounds.center));
                byCollider[collider] = thing;
            }
            thing.Depth = depth;
            byIdentity[thing.Identity] = thing;
            Things.Add(thing);
        }
        Things.Sort((a, b) => a.Depth.CompareTo(b.Depth));

        sizings.Clear();
        foreach (var thing in Things)
        {
            thing.Sizing = thing.Sizing with { Enlargeable = Enlargeable(thing), Seen = IsSeen(camera, thing, mask) };
            sizings.Add(thing.Sizing);
        }
    }

    /// <summary>The thing the game hovers for <paramref name="hovered"/>, if it is on screen.</summary>
    public WorkbenchThing? Find(Object? hovered) =>
        hovered && byIdentity.TryGetValue(hovered!, out var thing) ? thing : null;

    /// <summary>The thing a collider belongs to, if it is on screen.</summary>
    public WorkbenchThing? Find(Collider collider) => byCollider.TryGetValue(collider, out var thing) ? thing : null;

    /// <summary>What the game hovers for these ray hits, read the way the game reads them.</summary>
    public WorkbenchThing? GamePickFor(RaycastHit[] hits, int count)
    {
        rayHits.Clear();
        for (var i = 0; i < count; i++) rayHits.Add(ToRayHit(hits[i]));
        return GamePick.AtWorkbench(rayHits);
    }

    /// <summary>
    /// What the game would hover with the cursor at <paramref name="screenPoint"/>: its ray, keeping
    /// as many hits as the game does.
    /// </summary>
    public WorkbenchThing? GamePickAt(Camera camera, Vector2 screenPoint, int mask, RaycastHit[] hits)
    {
        var count = Physics.RaycastNonAlloc(camera.ScreenPointToRay(screenPoint), hits, GameRayLength, mask);
        return GamePickFor(hits, count);
    }

    /// <summary>
    /// A real hit on the thing, as the game's own ray would report it, along a line from the camera to
    /// the centre of one of its shapes.
    /// </summary>
    public static bool HitOn(Camera camera, WorkbenchThing thing, out RaycastHit hit)
    {
        var origin = camera.transform.position;
        foreach (var collider in thing.Colliders)
        {
            var toCentre = collider.bounds.center - origin;
            if (collider.Raycast(new Ray(origin, toCentre), out hit, toCentre.magnitude + 1f)) return true;
        }
        hit = default;
        return false;
    }

    private RayHit<WorkbenchThing> ToRayHit(RaycastHit hit)
    {
        // The game tells screw slots and part ghosts by the hit's transform, parts and the device by its collider.
        var thing = Find(hit.collider);
        if (hit.transform.gameObject.layer == ProjectConstants.Layers.Projections)
            return new RayHit<WorkbenchThing>(RayHitLayer.SlotOrGhost, hit.distance, thing);

        var layer = hit.collider.gameObject.layer;
        var partOrDevice = layer == ProjectConstants.Layers.Elements || layer == ProjectConstants.Layers.Device;
        return new RayHit<WorkbenchThing>(partOrDevice ? RayHitLayer.PartOrDevice : RayHitLayer.Other, hit.distance,
            Hoverable(thing) ? thing : null);
    }

    /// <summary>
    /// Whether the game hovers this thing when its ray picks it; it only detects the device body and
    /// equipment. (It also hovers a quest item whose device is active; that is not mirrored.)
    /// </summary>
    private static bool Hoverable(WorkbenchThing? thing)
    {
        if (thing is null || thing.Kind is ThingKind.DeviceBody or ThingKind.Equipment) return false;
        if (thing.Identity is ElementBase element) return element.IsActivated;
        return true;
    }

    /// <summary>The minimum hitbox size applies to it, and the game would hover it once picked.</summary>
    private static bool Enlargeable(WorkbenchThing thing) => thing.Kind.Enlargeable() && Hoverable(thing);

    /// <summary>Lists the colliders of the placed device and of the loose parts on the bench.</summary>
    private void Rescan(DeviceService? devices)
    {
        nextScan = Time.unscaledTime + RescanSeconds;
        entries.Clear();
        if (!devices) return;

        var container = devices!.PlacedDeviceContainer;
        if (container && container.Device) Gather(container.Device);

        var surface = workSurfaceOf!(devices);
        if (!surface) return;
        foreach (var part in surface.PlacedElements)
        {
            if (part) Gather(part);
        }
    }

    private void Gather(Component root)
    {
        root.GetComponentsInChildren(false, gathered);
        foreach (var collider in gathered)
        {
            var (identity, kind, name) = Identify(collider);
            entries.Add(new Entry(collider, identity, kind, name));
        }
    }

    /// <summary>Groups a collider the way the game identifies what its ray hit.</summary>
    private static (Object Identity, ThingKind? Kind, string Name) Identify(Collider collider)
    {
        // A ray hit's transform is the rigidbody's when the collider belongs to one.
        var owner = collider.attachedRigidbody ? collider.attachedRigidbody.transform : collider.transform;

        if (owner.gameObject.layer == ProjectConstants.Layers.Projections)
        {
            var projection = owner.GetComponent<ElementProjection>();
            if (!projection) return (collider, ThingKind.PartGhost, collider.name);
            var socket = projection.GetComponentInParent<ElementSocket>();
            var name = socket && socket.CompatibleElementInfo ? socket.CompatibleElementInfo.ID : projection.name;
            return (projection, IsScrewSlot(socket, projection) ? ThingKind.ScrewSlot : ThingKind.PartGhost, name);
        }

        if (collider.TryGetComponent<ElementButton>(out var button))
        {
            var part = button.GetComponentInParent<ElementBase>();
            return (button, ThingKind.Switch, part ? NameOf(part) : button.name);
        }

        if (owner.TryGetComponent<ElementBase>(out var element)) return (element, null, NameOf(element));

        return collider.gameObject.layer == ProjectConstants.Layers.Device
            ? (collider, ThingKind.DeviceBody, collider.name)
            : (collider, ThingKind.Equipment, collider.name);
    }

    private static ThingKind KindOf(ElementBase element) => ThingKinds.OfPart(
        inWashingBin: element.GetComponentInParent<SonicBath>(),
        inSocket: element.InSocket,
        small: element.Info && element.Info.Category == ElementCategory.Small,
        blocked: element.IsBlocked,
        switchable: element is ISwitchableElement);

    private static string NameOf(ElementBase element) => element.Info ? element.Info.ID : element.name;

    private static bool IsScrewSlot(ElementSocket? socket, ElementProjection projection) =>
        socket && screwSlotOf!(socket!) == projection;

    /// <summary>
    /// Whether any of the thing's shapes lies close enough to the ray for its hitbox, enlarged by up
    /// to <paramref name="reach"/> px, to take in the point the ray goes through.
    /// </summary>
    private static bool NearRay(WorkbenchThing thing, Ray ray, float reach, float pixelPerDepth)
    {
        foreach (var collider in thing.Colliders)
        {
            var bounds = collider.bounds;
            var toCentre = bounds.center - ray.origin;
            var depth = Vector3.Dot(toCentre, ray.direction);
            var radius = bounds.extents.magnitude;
            if (depth < -radius) continue;

            var offRay = (toCentre - depth * ray.direction).magnitude;
            if (offRay <= radius + reach * pixelPerDepth * Mathf.Max(depth, 0f)) return true;
        }
        return false;
    }

    /// <summary>Builds the thing's hitbox on screen. False when it is behind the camera or off screen.</summary>
    private bool Measure(Camera camera, WorkbenchThing thing)
    {
        shapes.Clear();
        var approximate = false;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var collider in thing.Colliders)
        {
            var points = new List<NVector2>(16);
            if (!ColliderOutline.OnScreen(camera, collider, points, ref approximate)) continue;
            foreach (var p in points)
            {
                min = Vector2.Min(min, new Vector2(p.X, p.Y));
                max = Vector2.Max(max, new Vector2(p.X, p.Y));
            }
            shapes.Add(points);
        }
        if (shapes.Count == 0) return false;

        var bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        if (!bounds.Overlaps(new Rect(0, 0, Screen.width, Screen.height))) return false;

        thing.ScreenBounds = bounds;
        thing.Approximate = approximate;
        thing.Sizing = new HitboxThing<WorkbenchThing>(thing, Hitbox.FromShapes(shapes), Enlargeable: false, Seen: false);
        return true;
    }

    /// <summary>
    /// Whether the camera sees the centre of any of the thing's shapes: it is on screen, and nothing
    /// the hover ray can hit lies in front of it apart from screw slots and part ghosts, which are
    /// see-through.
    /// </summary>
    private bool IsSeen(Camera camera, WorkbenchThing thing, int mask)
    {
        var origin = camera.transform.position;
        foreach (var collider in thing.Colliders)
        {
            var centre = collider.bounds.center;
            if (!ColliderOutline.IsOnScreen(camera, centre)) continue;

            var toCentre = centre - origin;
            var distance = toCentre.magnitude;
            if (distance < 0.0001f) return true;

            var count = Physics.RaycastNonAlloc(origin, toCentre / distance, viewHits, distance, mask);
            Collider? nearest = null;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var hit = viewHits[i];
                var own = thing.Colliders.Contains(hit.collider);
                if (!own && hit.transform.gameObject.layer == ProjectConstants.Layers.Projections) continue;
                if (hit.distance >= nearestDistance) continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
            }
            if (!nearest || thing.Colliders.Contains(nearest!)) return true;
        }
        return false;
    }
}
