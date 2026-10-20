using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>
/// PlayStation button art for the game's prompts. The game's glyph lists map PlayStation pads to its
/// generic A/B/X/Y art, but its image bundle also ships a full, unused set of PS5 sprites; this names
/// the one for each Rewired Gamepad Template element id (the ids the game's prompt templates use).
/// </summary>
public static class PlayStationGlyphs
{
    private static readonly Dictionary<int, string> Sprites = new()
    {
        // Stick movement: the PS5 stick sprites read "L3"/"R3", which means pressing the stick, and the
        // game's own PlayStation art is "?". The Xbox "L"/"R" badges match the PS5 set's look.
        [0] = "XBOX_LS", [1] = "XBOX_LS", [23] = "XBOX_LS",
        [2] = "XBOX_RS", [3] = "XBOX_RS", [24] = "XBOX_RS",
        [4] = "PS5Cross",
        // The game's own template draws ids 5/6 and 8/9 alike.
        [5] = "PS5_Circle", [6] = "PS5_Circle",
        // The game's Square sprite is misnamed as a second triangle.
        [7] = "PS5_Triangle-1",
        [8] = "PS5_Triangle", [9] = "PS5_Triangle",
        [10] = "PS5_L1", [11] = "PS5_L2",
        [12] = "PS5_R1", [13] = "PS5_R2",
        [14] = "PS5_Share", [15] = "PS5_Options",
        [17] = "PS5_LeftStick_click", [18] = "PS5_RightStick_click",
        [19] = "PS5_DPad_Top", [20] = "PS5_DPad_Right", [21] = "PS5_DPad_Bottom", [22] = "PS5_DPad_Left",
        [25] = "PS5_DPad",
    };

    /// <summary>Every sprite name used, so they can be loaded up front.</summary>
    public static IReadOnlyCollection<string> AllSprites { get; } = Sprites.Values.Distinct().ToArray();

    /// <summary>The sprite name for a template element id, or null to keep the game's art (e.g. the PS button).</summary>
    public static string? SpriteFor(int templateElementId) =>
        Sprites.TryGetValue(templateElementId, out var sprite) ? sprite : null;
}
