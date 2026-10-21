using System;
using System.Collections.Generic;

namespace RestoryMod.Core;

/// <summary>A destination's circle with the D-pad direction that sends there.</summary>
public readonly record struct SendGuide(Destination Destination, DpadDirection Direction);

/// <summary>
/// Which destinations show a send guide: exactly those a D-pad press would send to right now. The
/// rules are the sends' own, so a guide never offers a send that would be refused.
/// </summary>
public static class SendGuides
{
    private static readonly DpadDirection[] Directions =
        [DpadDirection.Up, DpadDirection.Right, DpadDirection.Left, DpadDirection.Down];

    /// <param name="ready">The destination's own state allows a send (washing bin unlocked, not full or running).</param>
    /// <param name="circledByGame">The game already shows the destination's circle for this part.</param>
    public static IReadOnlyList<SendGuide> For(HeldPart part, bool shredderOwned, Func<Destination, bool> ready,
        Func<Destination, bool> circledByGame)
    {
        var guides = new List<SendGuide>();
        foreach (var direction in Directions)
        {
            var destination = PartSend.DestinationFor(direction, shredderOwned);
            if (!PartSend.Accepts(destination, part) || !ready(destination)) continue;
            // The parts box takes nearly every part; circling it on each pick-up would only blink. It is
            // pointed out for a part the device has no room for, or where the game does so itself.
            if (destination == Destination.PartsBox && !part.Surplus && !circledByGame(destination)) continue;
            guides.Add(new SendGuide(destination, direction));
        }
        return guides;
    }

    /// <summary>Away from the workbench, where only left and down send.</summary>
    public static IReadOnlyList<SendGuide> For(CarriedObject carried, bool shredderOwned, Func<Destination, bool> ready)
    {
        var guides = new List<SendGuide>();
        foreach (var direction in Directions)
        {
            if (PartSend.CarriedDestinationFor(direction, shredderOwned) is not { } destination) continue;
            if (PartSend.Accepts(destination, carried) && ready(destination))
                guides.Add(new SendGuide(destination, direction));
        }
        return guides;
    }
}
