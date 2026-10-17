using System.Collections.Generic;

namespace RestoryMod.Core;

/// <summary>Which of the workbench's hover layers a ray hit landed on.</summary>
public enum RayHitLayer
{
    /// <summary>A screw slot or part ghost.</summary>
    SlotOrGhost,

    /// <summary>A part, or the device body.</summary>
    PartOrDevice,

    /// <summary>Anything else the hover ray can hit, such as bench equipment.</summary>
    Other,
}

/// <summary>
/// One hit of the game's hover ray. <paramref name="Thing"/> is what the game would hover for it, or
/// null when it hovers nothing there (the device body, bench equipment).
/// </summary>
public readonly record struct RayHit<T>(RayHitLayer Layer, float Distance, T? Thing) where T : class;

/// <summary>The game's own choice of what the cursor hovers, for spots the cursor is not on.</summary>
public static class GamePick
{
    /// <summary>
    /// What the game hovers at the workbench for one ray's hits, given in the order the physics query
    /// returned them (which is not sorted by distance). Mirrors the detection in the game's workbench
    /// hover states: the first screw slot or part ghost wins outright; otherwise the nearest part or
    /// device body, and only without one of those, the nearest other hit. (When that last hit is the
    /// washing bin, the game also looks for a part in it near the hit point; that is not mirrored.)
    /// </summary>
    public static T? AtWorkbench<T>(IReadOnlyList<RayHit<T>> hits) where T : class
    {
        if (hits.Count == 0) return null;

        var chosen = hits[0];
        var onPartOrDevice = false;
        foreach (var hit in hits)
        {
            if (hit.Layer == RayHitLayer.SlotOrGhost) return hit.Thing;
            if (hit.Layer == RayHitLayer.PartOrDevice)
            {
                if (!onPartOrDevice || hit.Distance < chosen.Distance) chosen = hit;
                onPartOrDevice = true;
            }
            else if (!onPartOrDevice && hit.Distance < chosen.Distance)
            {
                chosen = hit;
            }
        }
        return chosen.Thing;
    }
}
