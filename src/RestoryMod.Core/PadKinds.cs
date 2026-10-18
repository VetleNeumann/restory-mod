using System;

namespace RestoryMod.Core;

/// <summary>Which family of button art a gamepad needs. Unknown means "no opinion": leave the game's choice.</summary>
public enum PadKind
{
    Unknown,
    Xbox,
    PlayStation,
    Nintendo,
    SteamDeck,
    Generic,
}

/// <summary>The "Button prompts" setting: follow the pad, or always draw one family's art.</summary>
public enum PromptStyle
{
    Auto,
    Xbox,
    PlayStation,
    SteamDeck,
    Generic,
}

/// <summary>The "Button prompts" settings row: one slider step per style.</summary>
public static class PromptStyles
{
    public static readonly PromptStyle[] InSliderOrder =
    {
        PromptStyle.Auto, PromptStyle.Xbox, PromptStyle.PlayStation, PromptStyle.SteamDeck, PromptStyle.Generic,
    };

    public static PromptStyle FromStep(int step) => InSliderOrder[Math.Max(0, Math.Min(step, InSliderOrder.Length - 1))];

    public static int ToStep(PromptStyle style) => Math.Max(0, Array.IndexOf(InSliderOrder, style));

    public static string Label(PromptStyle style) => style == PromptStyle.SteamDeck ? "Steam Deck" : style.ToString();
}

/// <summary>
/// Works out what kind of gamepad the player holds. Under Steam Input every pad reaches Rewired as an
/// Xbox 360 pad, so Steam's own report of the hardware wins over Rewired's.
/// </summary>
public static class PadKinds
{
    /// <summary>
    /// Classifies a Steamworks <c>ESteamInputType</c> by its name (e.g. "k_ESteamInputType_PS5Controller"),
    /// so a renumbered enum in a future Steamworks.NET can't silently shift the mapping. A type added
    /// after this was written counts as generic.
    /// </summary>
    public static PadKind FromSteamInputType(string? steamType)
    {
        const string prefix = "k_ESteamInputType_";
        if (steamType is null) return PadKind.Unknown;
        var name = steamType.StartsWith(prefix) ? steamType.Substring(prefix.Length) : steamType;
        return name switch
        {
            "Unknown" => PadKind.Unknown,
            "XBox360Controller" or "XBoxOneController" => PadKind.Xbox,
            "PS3Controller" or "PS4Controller" or "PS5Controller" => PadKind.PlayStation,
            "SwitchProController" or "SwitchJoyConPair" or "SwitchJoyConSingle" => PadKind.Nintendo,
            // The game draws the Steam Controller with its Steam Deck art too.
            "SteamDeckController" or "SteamController" => PadKind.SteamDeck,
            _ => PadKind.Generic,
        };
    }

    // Rewired hardware map GUIDs, as the game's RewiredControllerIdsDependencyMap uses them.
    public static readonly Guid Xbox360 = new("d74a350e-fe8b-4e9e-bbcd-efff16d34115");
    public static readonly Guid XboxOne = new("19002688-7406-4f4a-8340-8d25335406c8");
    public static readonly Guid DualSense = new("5286706d-19b4-4a45-b635-207ce78d8394");
    public static readonly Guid DualShock4 = new("cd9718bf-a87a-44bc-8716-60a0def28a9f");
    public static readonly Guid SwitchPro = new("7bf3154b-9db8-4d52-950f-cd0eed8a5819");
    /// <summary>Rewired's "Steam-configured controller"; the game draws it with Steam Deck art.</summary>
    public static readonly Guid SteamController = new("2694f4b9-9d84-4f55-9ee8-78fbba744b7d");

    /// <summary>Classifies a Rewired <c>Joystick.hardwareTypeGuid</c>. Pads the mod has no table entry for are Unknown.</summary>
    public static PadKind FromRewiredGuid(Guid hardwareTypeGuid)
    {
        if (hardwareTypeGuid == Xbox360 || hardwareTypeGuid == XboxOne) return PadKind.Xbox;
        if (hardwareTypeGuid == DualSense || hardwareTypeGuid == DualShock4) return PadKind.PlayStation;
        if (hardwareTypeGuid == SwitchPro) return PadKind.Nintendo;
        if (hardwareTypeGuid == SteamController) return PadKind.SteamDeck;
        return PadKind.Unknown;
    }

    /// <summary>
    /// The pad's kind: Steam's report when Steam Input handles the pad (<paramref name="steamType"/> not null),
    /// otherwise Rewired's hardware id. While Steam handles it, Rewired only sees Steam's virtual Xbox 360
    /// pad, so a pad Steam can't name yet stays Unknown rather than passing for an Xbox pad.
    /// </summary>
    public static PadKind Resolve(string? steamType, Guid rewiredGuid) =>
        steamType is not null ? FromSteamInputType(steamType) : FromRewiredGuid(rewiredGuid);

    /// <summary>
    /// Which art the prompts should draw. Unknown means leave the game's own pick alone. The game has no
    /// Nintendo art, and its Xbox art would put A where a Switch pad has B, so Nintendo pads get generic.
    /// </summary>
    public static PadKind ArtToShow(PromptStyle setting, PadKind detected) => setting switch
    {
        PromptStyle.Xbox => PadKind.Xbox,
        PromptStyle.PlayStation => PadKind.PlayStation,
        PromptStyle.SteamDeck => PadKind.SteamDeck,
        PromptStyle.Generic => PadKind.Generic,
        _ => detected == PadKind.Nintendo ? PadKind.Generic : detected,
    };

    /// <summary>
    /// A hardware id the game's dependency map draws with this art, or null for the game's default
    /// gamepad art (generic) or no opinion.
    /// </summary>
    public static Guid? GameMapGuid(PadKind art) => art switch
    {
        PadKind.Xbox => XboxOne,
        PadKind.PlayStation => DualSense,
        PadKind.SteamDeck => SteamController,
        _ => null,
    };
}
