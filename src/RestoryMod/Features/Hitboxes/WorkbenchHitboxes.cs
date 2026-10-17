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

/// <summary>Kinds of thing the hover ray can hit at the workbench, in the glossary's words.</summary>
internal enum ThingKind
{
    Screw,
    ScrewSlot,
    LoosePart,
    AttachedPart,
    Switch,
    BlockedPart,
    PartGhost,
    WashingBinPart,
    DeviceBody,
    Equipment,
}

internal static class ThingKinds
{
    /// <summary>
    /// Whether the minimum hitbox size applies: to things a press acts on. Not to blocked parts and
    /// part ghosts (a press does nothing to them) or the device body; not to washing bin parts, which
    /// the game already picks by nearness; not to bench equipment, which the game clicks another way.
    /// </summary>
    public static bool Enlargeable(this ThingKind kind) =>
        kind is ThingKind.Screw or ThingKind.ScrewSlot or ThingKind.LoosePart or ThingKind.AttachedPart or ThingKind.Switch;

    public static string Label(this ThingKind kind) => kind switch
    {
        ThingKind.Screw => "screw",
        ThingKind.ScrewSlot => "screw slot",
        ThingKind.LoosePart => "loose part",
        ThingKind.AttachedPart => "attached part",
        ThingKind.Switch => "switch",
        ThingKind.BlockedPart => "blocked part",
        ThingKind.PartGhost => "part ghost",
        ThingKind.WashingBinPart => "washing bin part",
        ThingKind.DeviceBody => "device body",
        _ => "equipment",
    };
}

/// <summary>One thing the hover ray can hit, with this frame's hitbox on screen.</summary>
internal sealed class WorkbenchThing(Object identity, ThingKind kind, string name)
{
    /// <summary>What the game hovers for it: an element, projection or button; a collider otherwise.</summary>
    public Object Identity { get; } = identity;

    public ThingKind Kind { get; } = kind;
    public string Name { get; } = name;
    public List<Collider> Colliders { get; } = new();
    public HitboxThing<WorkbenchThing> Hitbox { get; set; } = null!;

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

    private static readonly int[] CornerSigns = [-1, 1];
    private static AccessTools.FieldRef<ElementSocket, ElementProjection>? screwSlotOf;

    private readonly List<Collider> colliders = new();
    private float nextScan;
    private int scannedMask;

    private readonly Dictionary<Object, WorkbenchThing> byIdentity = new();
    private readonly Dictionary<Collider, WorkbenchThing> byCollider = new();
    private readonly List<HitboxThing<WorkbenchThing>> hitboxes = new();
    private readonly List<List<NVector2>> shapes = new();
    private readonly RaycastHit[] viewHits = new RaycastHit[16];

    /// <summary>This frame's things, front to back.</summary>
    public List<WorkbenchThing> Things { get; } = new();

    /// <summary>This frame's hitboxes, front to back, as the minimum hitbox size sees them.</summary>
    public IReadOnlyList<HitboxThing<WorkbenchThing>> Hitboxes => hitboxes;

    public void Update(Camera camera, int mask)
    {
        if (Time.unscaledTime >= nextScan || mask != scannedMask) Rescan(mask);

        byIdentity.Clear();
        byCollider.Clear();
        Things.Clear();
        foreach (var collider in colliders)
        {
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if ((mask & (1 << collider.gameObject.layer)) == 0) continue;

            var (identity, kind, name) = Identify(collider);
            if (!byIdentity.TryGetValue(identity, out var thing))
            {
                thing = new WorkbenchThing(identity, kind, name);
                byIdentity.Add(identity, thing);
                Things.Add(thing);
            }
            thing.Colliders.Add(collider);
            byCollider[collider] = thing;
        }

        var origin = camera.transform.position;
        for (var i = Things.Count - 1; i >= 0; i--)
        {
            var thing = Things[i];
            if (!Measure(camera, thing))
            {
                Things.RemoveAt(i);
                continue;
            }
            var depth = float.MaxValue;
            foreach (var collider in thing.Colliders)
                depth = Mathf.Min(depth, Vector3.Distance(origin, collider.bounds.center));
            thing.Depth = depth;
        }
        Things.Sort((a, b) => a.Depth.CompareTo(b.Depth));

        hitboxes.Clear();
        foreach (var thing in Things)
        {
            var seen = !thing.Kind.Enlargeable() || IsSeen(origin, thing, mask);
            thing.Hitbox = new HitboxThing<WorkbenchThing>(thing, thing.Hitbox.Hitbox, thing.Kind.Enlargeable(), seen);
            hitboxes.Add(thing.Hitbox);
        }
    }

    /// <summary>The thing the game hovers for <paramref name="hovered"/>, if it is on screen.</summary>
    public WorkbenchThing? Find(Object? hovered) =>
        hovered && byIdentity.TryGetValue(hovered!, out var thing) ? thing : null;

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
        // The game tells projections by the hit's transform and parts and the device by its collider.
        var thing = Find(hit.collider);
        if (hit.transform.gameObject.layer == ProjectConstants.Layers.Projections)
            return new RayHit<WorkbenchThing>(RayHitLayer.Projection, hit.distance, thing);

        var layer = hit.collider.gameObject.layer;
        var partOrDevice = layer == ProjectConstants.Layers.Elements || layer == ProjectConstants.Layers.Device;
        return new RayHit<WorkbenchThing>(partOrDevice ? RayHitLayer.PartOrDevice : RayHitLayer.Other, hit.distance,
            Hoverable(thing) ? thing : null);
    }

    /// <summary>Whether the game hovers this thing when its ray picks it (it only detects the rest).</summary>
    private static bool Hoverable(WorkbenchThing? thing)
    {
        if (thing is null || thing.Kind is ThingKind.DeviceBody or ThingKind.Equipment) return false;
        return thing.Identity is not ElementBase element || element.IsActivated;
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
            var forPart = socket && socket.CompatibleElementInfo ? socket.CompatibleElementInfo.ID : projection.name;
            return IsScrewSlot(socket, projection)
                ? (projection, ThingKind.ScrewSlot, "slot for " + forPart)
                : (projection, ThingKind.PartGhost, "ghost of " + forPart);
        }

        if (collider.TryGetComponent<ElementButton>(out var button))
        {
            var onPart = button.GetComponentInParent<ElementBase>();
            return (button, ThingKind.Switch, "button on " + (onPart ? NameOf(onPart) : button.name));
        }

        if (owner.TryGetComponent<ElementBase>(out var element))
            return (element, KindOf(element), NameOf(element));

        return collider.gameObject.layer == ProjectConstants.Layers.Device
            ? (collider, ThingKind.DeviceBody, collider.name)
            : (collider, ThingKind.Equipment, collider.name);
    }

    private static ThingKind KindOf(ElementBase element)
    {
        if (element.GetComponentInParent<SonicBath>()) return ThingKind.WashingBinPart;
        if (!element.InSocket) return ThingKind.LoosePart;
        if (element.Info && element.Info.Category == ElementCategory.Small) return ThingKind.Screw;
        if (!element.IsBlocked) return ThingKind.AttachedPart;
        // A switch works while it cannot come off; any other blocked part ignores a press.
        return element is ISwitchableElement ? ThingKind.Switch : ThingKind.BlockedPart;
    }

    private static string NameOf(ElementBase element) => element.Info ? element.Info.ID : element.name;

    private static bool IsScrewSlot(ElementSocket? socket, ElementProjection projection)
    {
        if (!socket) return false;
        if (screwSlotOf is null && GameTargets.AllPresent(PatchTargets.SocketScrewSlot))
            screwSlotOf = AccessTools.FieldRefAccess<ElementSocket, ElementProjection>(PatchTargets.SocketScrewSlot.MemberName!);
        return screwSlotOf is not null && screwSlotOf(socket!) == projection;
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
            if (!ScreenShape(camera, collider, points, ref approximate)) continue;
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
        thing.Hitbox = new HitboxThing<WorkbenchThing>(thing, Core.Hitbox.FromShapes(shapes), false, false);
        return true;
    }

    /// <summary>
    /// The collider's outline points on screen: a box's corners, points around a sphere or capsule,
    /// or the corners of a mesh's bounding box. False when part of it is behind the camera.
    /// </summary>
    private static bool ScreenShape(Camera camera, Collider collider, List<NVector2> points, ref bool approximate)
    {
        var transform = collider.transform;
        switch (collider)
        {
            case BoxCollider box:
                return BoxCorners(camera, transform, new Bounds(box.center, box.size), points);
            case SphereCollider sphere:
            {
                var scale = transform.lossyScale;
                var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return Ring(camera, transform.TransformPoint(sphere.center), radius, points);
            }
            case CapsuleCollider capsule:
            {
                var scale = transform.lossyScale;
                var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                var radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var half = Mathf.Max(0f, capsule.height / 2 - capsule.radius) * axis;
                return Ring(camera, transform.TransformPoint(capsule.center + half), radius, points)
                    && Ring(camera, transform.TransformPoint(capsule.center - half), radius, points);
            }
            case MeshCollider mesh when mesh.sharedMesh:
                approximate = true;
                return BoxCorners(camera, transform, mesh.sharedMesh.bounds, points);
            default:
                approximate = true;
                var world = collider.bounds;
                return BoxCorners(camera, null, world, points);
        }
    }

    private static bool BoxCorners(Camera camera, Transform? transform, Bounds box, List<NVector2> points)
    {
        foreach (var x in CornerSigns)
        foreach (var y in CornerSigns)
        foreach (var z in CornerSigns)
        {
            var corner = box.center + Vector3.Scale(box.extents, new Vector3(x, y, z));
            if (!Project(camera, transform ? transform!.TransformPoint(corner) : corner, points)) return false;
        }
        return true;
    }

    /// <summary>Points around a sphere's outline as the camera sees it.</summary>
    private static bool Ring(Camera camera, Vector3 centre, float radius, List<NVector2> points)
    {
        var right = camera.transform.right * radius;
        var up = camera.transform.up * radius;
        for (var i = 0; i < 16; i++)
        {
            var angle = i * Mathf.PI / 8;
            if (!Project(camera, centre + right * Mathf.Cos(angle) + up * Mathf.Sin(angle), points)) return false;
        }
        return true;
    }

    private static bool Project(Camera camera, Vector3 world, List<NVector2> points)
    {
        var screen = camera.WorldToScreenPoint(world);
        if (screen.z <= camera.nearClipPlane) return false;
        points.Add(new NVector2(screen.x, screen.y));
        return true;
    }

    /// <summary>
    /// Whether the camera sees the centre of any of the thing's shapes: nothing the hover ray can hit
    /// lies in front of it, apart from projections, which are see-through.
    /// </summary>
    private bool IsSeen(Vector3 origin, WorkbenchThing thing, int mask)
    {
        foreach (var collider in thing.Colliders)
        {
            var toCentre = collider.bounds.center - origin;
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
            if (nearest is null || thing.Colliders.Contains(nearest)) return true;
        }
        return false;
    }
}
