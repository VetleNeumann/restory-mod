using System.Collections.Generic;
using HarmonyLib;
using Restory.Constants;
using Restory.Data.Elements;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment.Ultrasonic;
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
/// Everything the workbench hover ray can hit, grouped into things the way the game identifies a ray
/// hit, each with its native hitbox on screen and whether the camera can see it.
/// </summary>
internal sealed class WorkbenchHitboxes
{
    /// <summary>The game's hover ray length, in metres (a literal in its cursor detector).</summary>
    public const float GameRayLength = 4f;

    private const float RescanSeconds = 0.5f;

    private static AccessTools.FieldRef<ElementSocket, ElementProjection>? screwSlotOf;

    private readonly List<Collider> colliders = new();
    private float nextScan;
    private int scannedMask;

    private readonly List<WorkbenchThing> found = new();
    private readonly Dictionary<Object, WorkbenchThing> byIdentity = new();
    private readonly Dictionary<Collider, WorkbenchThing> byCollider = new();
    private readonly List<HitboxThing<WorkbenchThing>> sizings = new();
    private readonly List<List<NVector2>> shapes = new();
    private readonly RaycastHit[] viewHits = new RaycastHit[16];

    /// <summary>Resolves what telling screw slots from part ghosts needs; for the patches' Prepare().</summary>
    public static bool Prepare()
    {
        if (!GameTargets.AllPresent(PatchTargets.SocketScrewSlot)) return false;
        screwSlotOf ??= AccessTools.FieldRefAccess<ElementSocket, ElementProjection>(PatchTargets.SocketScrewSlot.MemberName!);
        return true;
    }

    /// <summary>This frame's things on screen, front to back.</summary>
    public List<WorkbenchThing> Things { get; } = new();

    /// <summary>This frame's things as the minimum hitbox size sees them, front to back.</summary>
    public IReadOnlyList<HitboxThing<WorkbenchThing>> Sizings => sizings;

    public void Update(Camera camera, int mask)
    {
        if (Time.unscaledTime >= nextScan || mask != scannedMask) Rescan(mask);

        found.Clear();
        byIdentity.Clear();
        foreach (var collider in colliders)
        {
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if ((mask & (1 << collider.gameObject.layer)) == 0) continue;

            var (identity, kind, name) = Identify(collider);
            if (!byIdentity.TryGetValue(identity, out var thing))
            {
                thing = new WorkbenchThing(identity, kind, name);
                byIdentity.Add(identity, thing);
                found.Add(thing);
            }
            thing.Colliders.Add(collider);
        }

        // Only things on screen stay findable, so everything found has a hitbox.
        Things.Clear();
        byIdentity.Clear();
        byCollider.Clear();
        var origin = camera.transform.position;
        foreach (var thing in found)
        {
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
            thing.Sizing = thing.Sizing with { Enlargeable = thing.Kind.Enlargeable(), Seen = IsSeen(camera, thing, mask) };
            sizings.Add(thing.Sizing);
        }
    }

    /// <summary>The thing the game hovers for <paramref name="hovered"/>, if it is on screen.</summary>
    public WorkbenchThing? Find(Object? hovered) =>
        hovered && byIdentity.TryGetValue(hovered!, out var thing) ? thing : null;

    /// <summary>The thing a collider belongs to, if it is on screen.</summary>
    public WorkbenchThing? Find(Collider collider) => byCollider.TryGetValue(collider, out var thing) ? thing : null;

    /// <summary>
    /// What the game would hover with the cursor at <paramref name="screenPoint"/>: its ray, keeping
    /// as many hits as the game does, read the way the game reads them.
    /// </summary>
    public WorkbenchThing? GamePickAt(Camera camera, Vector2 screenPoint, int mask, RaycastHit[] hits,
        List<RayHit<WorkbenchThing>> scratch)
    {
        var count = Physics.RaycastNonAlloc(camera.ScreenPointToRay(screenPoint), hits, GameRayLength, mask);
        scratch.Clear();
        for (var i = 0; i < count; i++) scratch.Add(ToRayHit(hits[i]));
        return GamePick.AtWorkbench(scratch);
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

    private void Rescan(int mask)
    {
        nextScan = Time.unscaledTime + RescanSeconds;
        scannedMask = mask;
        colliders.Clear();
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if ((mask & (1 << collider.gameObject.layer)) != 0) colliders.Add(collider);
        }
    }

    /// <summary>Groups a collider the way the game identifies what its ray hit.</summary>
    private static (Object Identity, ThingKind Kind, string Name) Identify(Collider collider)
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

        if (owner.TryGetComponent<ElementBase>(out var element))
        {
            var kind = ThingKinds.OfPart(
                inWashingBin: element.GetComponentInParent<SonicBath>(),
                inSocket: element.InSocket,
                small: element.Info && element.Info.Category == ElementCategory.Small,
                blocked: element.IsBlocked,
                switchable: element is ISwitchableElement);
            return (element, kind, NameOf(element));
        }

        return collider.gameObject.layer == ProjectConstants.Layers.Device
            ? (collider, ThingKind.DeviceBody, collider.name)
            : (collider, ThingKind.Equipment, collider.name);
    }

    private static string NameOf(ElementBase element) => element.Info ? element.Info.ID : element.name;

    private static bool IsScrewSlot(ElementSocket? socket, ElementProjection projection) =>
        socket && screwSlotOf!(socket!) == projection;

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
