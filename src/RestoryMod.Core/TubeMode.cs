namespace RestoryMod.Core;

/// <summary>
/// Whether the player is taking the device apart after a change in a tube (batteries). The game judges
/// by the last socket that changed: filled means putting back. Taking a part out of a tube's entry lets
/// the next one slide forward into it, which the game reads as a part put in.
/// </summary>
public static class TubeMode
{
    /// <param name="partsBefore">Parts in the tube at its last change; null if it hasn't changed before.</param>
    public static bool TakingApart(bool wasTakingApart, int? partsBefore, int partsNow, bool gameSays)
    {
        if (partsBefore is not { } before) return gameSays;
        if (partsNow < before) return true;
        if (partsNow > before) return false;
        return wasTakingApart;
    }
}
