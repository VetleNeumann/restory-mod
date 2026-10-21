namespace RestoryMod.Core;

public enum DpadDirection { Up, Down, Left, Right }

/// <summary>A place at the workbench that takes a held part away from the bench in one move.</summary>
public enum Destination { PartsBox, WashingBin, ToolCup, TrashCan, Shredder }

/// <summary>What the destinations' rules look at in a held part.</summary>
/// <param name="NeedsCleaning">Dirty or in need of soldering: what the tool cup works on.</param>
public readonly record struct HeldPart(bool Damaged, bool QuestItem, bool NeedsCleaning)
{
    /// <summary>Has something left the washing bin can clean; a part that only needs soldering has not.</summary>
    public bool Washable { get; init; }

    /// <summary>Came out of the device on the bench: the tool cup works only on that device's parts.</summary>
    public bool FromPlacedDevice { get; init; }

    /// <summary>The device on the bench has no open socket left for it; see <see cref="PartSend.IsSurplus"/>.</summary>
    public bool Surplus { get; init; }
}

/// <summary>
/// Rules for sending a held part to a destination with one D-pad press. A send the destination would
/// refuse does nothing, so these mirror the game's own drop rules: checked before the drop, a refusal
/// never reaches the game's fallback (bench surface or a nearby socket).
/// </summary>
/// <summary>What the destinations' rules look at in an object carried away from the workbench.</summary>
/// <param name="Rubbish">Marked by the game as something to throw away, such as an empty box.</param>
/// <param name="HoldsParts">Bought parts, which empty into the parts box.</param>
public readonly record struct CarriedObject(bool Rubbish, bool HoldsParts);

public static class PartSend
{
    /// <summary>
    /// Away from the workbench only the parts box and the trash can (or shredder) take carried
    /// objects; the washing bin and tool cup work on parts at the bench.
    /// </summary>
    public static Destination? CarriedDestinationFor(DpadDirection direction, bool shredderOwned) => direction switch
    {
        DpadDirection.Left => Destination.PartsBox,
        DpadDirection.Down => shredderOwned ? Destination.Shredder : Destination.TrashCan,
        _ => null,
    };

    /// <summary>The game's rules for carried objects: rubbish is thrown away, bought parts are stored.</summary>
    public static bool Accepts(Destination destination, CarriedObject carried) => destination switch
    {
        Destination.TrashCan or Destination.Shredder => carried.Rubbish,
        Destination.PartsBox => carried.HoldsParts,
        _ => false,
    };

    /// <summary>
    /// Up, right and left point roughly where the washing bin, tool cup and parts box sit on screen;
    /// down throws away. The shredder, once bought, stands in the trash can's place.
    /// </summary>
    public static Destination DestinationFor(DpadDirection direction, bool shredderOwned) => direction switch
    {
        DpadDirection.Up => Destination.WashingBin,
        DpadDirection.Right => Destination.ToolCup,
        DpadDirection.Left => Destination.PartsBox,
        _ => shredderOwned ? Destination.Shredder : Destination.TrashCan,
    };

    /// <summary>
    /// Grab held on a parts box slot before any drag: the press lifts the part and sends it, so the
    /// shortcut works without first moving the cursor. Left would send it back where it already is.
    /// </summary>
    public static bool SendsFromHeldSlot(DpadDirection direction) => direction != DpadDirection.Left;

    /// <summary>
    /// A part dragged out of the parts box shows as an icon while the cursor is over the box; left then
    /// puts it back in its slot, as letting go there does.
    /// </summary>
    public static bool PutsBackInSlot(DpadDirection direction, bool shownAsIcon) =>
        shownAsIcon && direction == DpadDirection.Left;

    /// <summary>
    /// The one refusal that speaks: the game tells the player there is no point keeping a broken part
    /// when one is dropped on the parts box, and the send says the same. Other refusals stay silent.
    /// </summary>
    public static bool WarnsBrokenPart(Destination destination, HeldPart part) =>
        destination == Destination.PartsBox && part.Damaged;

    /// <summary>
    /// The part-based half of the game's rules. The washing bin's own state (unlocked, full, running)
    /// is left to the game, which checks it when the part is fitted in.
    /// </summary>
    public static bool Accepts(Destination destination, HeldPart part) => destination switch
    {
        Destination.TrashCan or Destination.Shredder => part.Damaged,
        Destination.ToolCup => !part.Damaged && part.NeedsCleaning && part.FromPlacedDevice,
        Destination.WashingBin => !part.Damaged && !part.QuestItem && part.Washable,
        _ => !part.Damaged && !part.QuestItem,
    };

    /// <summary>
    /// A part the device on the bench has no open socket left for: more loose copies than it has empty
    /// sockets of that kind. A part of another device has none, while a bought one may fit.
    /// </summary>
    /// <param name="looseCopies">Loose parts of this kind at the bench, the held one included.</param>
    public static bool IsSurplus(int looseCopies, int openSockets) => looseCopies > openSockets;
}
