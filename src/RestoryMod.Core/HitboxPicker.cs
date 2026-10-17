using System.Collections.Generic;
using System.Numerics;

namespace RestoryMod.Core;

/// <summary>Configurable minimum hitbox size, as a percentage of screen height.</summary>
public static class MinimumHitboxSize
{
    public const float DefaultPercent = 4f;
    public const float MaxPercent = 15f;
}

/// <summary>Why the cursor hovers what it does.</summary>
public enum PickReason
{
    /// <summary>A panel is under the cursor. Panels always win, so nothing at the workbench is hovered.</summary>
    Panel,

    /// <summary>No enlarged hitbox reaches the cursor, so the game's own pick stands.</summary>
    Native,

    /// <summary>An enlarged hitbox reaches the cursor and wins.</summary>
    Enlarged,
}

/// <summary>
/// A thing at the workbench and its native hitbox. <paramref name="Enlargeable"/> is false for things
/// the minimum hitbox size leaves alone, such as a blocked part or a part ghost (a press does nothing to
/// them); they count as empty space. <paramref name="Seen"/> is whether the camera can see its centre:
/// a hidden thing keeps its native hitbox.
/// </summary>
public sealed record HitboxThing<T>(T Thing, Hitbox Hitbox, bool Enlargeable, bool Seen) where T : class
{
    /// <summary>How far its enlarged hitbox reaches past the native one; 0 when it is not enlarged.</summary>
    public float Growth(float minimumSize) => Enlargeable && Seen ? Hitbox.GrowthFor(minimumSize) : 0;
}

/// <summary>What the cursor hovers, and why.</summary>
public readonly record struct HitboxPick<T>(T? Thing, PickReason Reason) where T : class;

/// <summary>
/// Minimum hitbox size: decides what the cursor hovers once small hitboxes are enlarged. An enlarged
/// hitbox beats the native hitbox of any larger thing and of anything not enlargeable; where enlarged
/// hitboxes overlap, the thing whose native outline lies nearest the cursor wins.
/// </summary>
public static class HitboxPicker
{
    /// <summary>Distances closer than this count as a tie.</summary>
    private const float Tie = 0.01f;

    /// <summary>
    /// What the cursor at <paramref name="cursor"/> hovers. <paramref name="things"/> are listed front to
    /// back; <paramref name="gamePick"/> is what the game itself hovers there. Ties go to the game's pick,
    /// then to the front-most thing.
    /// </summary>
    public static HitboxPick<T> Pick<T>(Vector2 cursor, float minimumSize, IReadOnlyList<HitboxThing<T>> things,
        T? gamePick, bool panelUnderCursor) where T : class
    {
        if (panelUnderCursor) return new HitboxPick<T>(null, PickReason.Panel);

        HitboxThing<T>? best = null;
        var bestDistance = float.MaxValue;
        foreach (var thing in things)
        {
            var growth = thing.Growth(minimumSize);
            var distance = thing.Hitbox.DistanceTo(cursor);
            if (growth <= 0 || distance > growth) continue;

            var nearer = distance < bestDistance - Tie;
            var tiedWithGamePick = distance <= bestDistance + Tie && ReferenceEquals(thing.Thing, gamePick);
            if (!nearer && !tiedWithGamePick) continue;
            best = thing;
            bestDistance = distance;
        }

        if (best is null) return new HitboxPick<T>(gamePick, PickReason.Native);
        return new HitboxPick<T>(best.Thing, ReferenceEquals(best.Thing, gamePick) ? PickReason.Native : PickReason.Enlarged);
    }
}
