namespace RestoryMod.Core;

/// <summary>Kinds of thing the hover ray can hit at the workbench, in the glossary's words.</summary>
public enum ThingKind
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

public static class ThingKinds
{
    /// <summary>What kind of thing a part is, from its state.</summary>
    public static ThingKind OfPart(bool inWashingBin, bool inSocket, bool small, bool blocked, bool switchable)
    {
        if (inWashingBin) return ThingKind.WashingBinPart;
        if (!inSocket) return ThingKind.LoosePart;
        if (small) return ThingKind.Screw;
        if (!blocked) return ThingKind.AttachedPart;
        // A switch works while it cannot come off; any other blocked part ignores a press.
        return switchable ? ThingKind.Switch : ThingKind.BlockedPart;
    }

    /// <summary>
    /// Whether the minimum hitbox size applies: to things a press works through hovering. Not to
    /// blocked parts and part ghosts (a press does nothing to them) or the device body; not to washing
    /// bin parts, which the game already picks by nearness; not to bench equipment, which the game
    /// clicks another way. All of these count as empty space for enlarged hitboxes.
    /// </summary>
    public static bool Enlargeable(this ThingKind kind) =>
        kind is ThingKind.Screw or ThingKind.ScrewSlot or ThingKind.LoosePart or ThingKind.AttachedPart or ThingKind.Switch;

    /// <summary>
    /// Whether the game's hover highlight may show on a part: not on a blocked part, which a press does
    /// nothing to. Some prefabs let the game outline a blocked part in its "can't act" red; the mod
    /// leaves it dark, as most prefabs do.
    /// </summary>
    public static bool ShowsHoverHighlight(bool blocked, bool switchable) => !blocked || switchable;

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
