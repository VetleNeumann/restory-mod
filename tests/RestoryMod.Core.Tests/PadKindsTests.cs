using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PadKindsTests
{
    [Theory]
    [InlineData("k_ESteamInputType_XBox360Controller", PadKind.Xbox)]
    [InlineData("k_ESteamInputType_XBoxOneController", PadKind.Xbox)]
    [InlineData("k_ESteamInputType_PS3Controller", PadKind.PlayStation)]
    [InlineData("k_ESteamInputType_PS4Controller", PadKind.PlayStation)]
    [InlineData("k_ESteamInputType_PS5Controller", PadKind.PlayStation)]
    [InlineData("k_ESteamInputType_SwitchProController", PadKind.Nintendo)]
    [InlineData("k_ESteamInputType_SwitchJoyConPair", PadKind.Nintendo)]
    [InlineData("k_ESteamInputType_SwitchJoyConSingle", PadKind.Nintendo)]
    [InlineData("k_ESteamInputType_SteamDeckController", PadKind.SteamDeck)]
    [InlineData("k_ESteamInputType_SteamController", PadKind.SteamDeck)]
    [InlineData("k_ESteamInputType_GenericGamepad", PadKind.Generic)]
    [InlineData("k_ESteamInputType_AndroidController", PadKind.Generic)]
    [InlineData("k_ESteamInputType_Unknown", PadKind.Unknown)]
    [InlineData("k_ESteamInputType_SomethingNew", PadKind.Generic)]
    [InlineData(null, PadKind.Unknown)]
    public void Steam_input_type_names_classify(string? steamType, PadKind expected)
    {
        Assert.Equal(expected, PadKinds.FromSteamInputType(steamType));
    }

    private const string Xbox360Guid = "d74a350e-fe8b-4e9e-bbcd-efff16d34115";
    private const string DualSenseGuid = "5286706d-19b4-4a45-b635-207ce78d8394";
    private const string SwitchProGuid = "7bf3154b-9db8-4d52-950f-cd0eed8a5819";

    [Theory]
    // Steam Input on: Rewired sees an Xbox 360 pad, Steam knows the real one.
    [InlineData("k_ESteamInputType_PS5Controller", Xbox360Guid, PadKind.PlayStation)]
    [InlineData("k_ESteamInputType_SwitchProController", Xbox360Guid, PadKind.Nintendo)]
    // Steam Input off for the pad: only Rewired's hardware id is left.
    [InlineData(null, DualSenseGuid, PadKind.PlayStation)]
    [InlineData(null, Xbox360Guid, PadKind.Xbox)]
    [InlineData(null, SwitchProGuid, PadKind.Nintendo)]
    // Steam owns the pad but hasn't named it yet: the hardware id is Steam's virtual Xbox 360 pad, not the real one.
    [InlineData("k_ESteamInputType_Unknown", Xbox360Guid, PadKind.Unknown)]
    // A pad neither knows: no opinion.
    [InlineData(null, "00000000-0000-0000-0000-000000000000", PadKind.Unknown)]
    [InlineData(null, "11111111-2222-3333-4444-555555555555", PadKind.Unknown)]
    public void Steam_report_wins_over_rewired_hardware_id(string? steamType, string rewiredGuid, PadKind expected)
    {
        Assert.Equal(expected, PadKinds.Resolve(steamType, new Guid(rewiredGuid)));
    }

    [Theory]
    [InlineData(PromptStyle.Auto, PadKind.PlayStation, PadKind.PlayStation)]
    [InlineData(PromptStyle.Auto, PadKind.Xbox, PadKind.Xbox)]
    [InlineData(PromptStyle.Auto, PadKind.SteamDeck, PadKind.SteamDeck)]
    [InlineData(PromptStyle.Auto, PadKind.Unknown, PadKind.Unknown)]
    // No Nintendo art in the game yet: its positional A/B/X/Y would mislabel, so draw generic.
    [InlineData(PromptStyle.Auto, PadKind.Nintendo, PadKind.Generic)]
    [InlineData(PromptStyle.PlayStation, PadKind.Xbox, PadKind.PlayStation)]
    [InlineData(PromptStyle.PlayStation, PadKind.Unknown, PadKind.PlayStation)]
    [InlineData(PromptStyle.Generic, PadKind.PlayStation, PadKind.Generic)]
    public void Setting_overrides_detection(PromptStyle setting, PadKind detected, PadKind expected)
    {
        Assert.Equal(expected, PadKinds.ArtToShow(setting, detected));
    }

    [Theory]
    [InlineData(PadKind.Xbox, "19002688-7406-4f4a-8340-8d25335406c8")]
    [InlineData(PadKind.PlayStation, DualSenseGuid)]
    [InlineData(PadKind.SteamDeck, "2694f4b9-9d84-4f55-9ee8-78fbba744b7d")]
    [InlineData(PadKind.Generic, null)]
    [InlineData(PadKind.Nintendo, null)]
    [InlineData(PadKind.Unknown, null)]
    public void Art_is_looked_up_by_a_hardware_id_the_game_maps(PadKind art, string? expected)
    {
        Assert.Equal(expected is null ? null : new Guid(expected), PadKinds.GameMapGuid(art));
    }

    [Fact]
    public void Menu_offers_every_style_once_with_a_label()
    {
        Assert.Equal(Enum.GetValues<PromptStyle>().OrderBy(s => s), PromptStyles.InMenuOrder.OrderBy(s => s));
        Assert.Equal(PromptStyle.Auto, PromptStyles.InMenuOrder[0]);
        Assert.Equal(new[] { PromptStyle.Auto, PromptStyle.PlayStation, PromptStyle.Generic }, PromptStyles.InMenuOrder);
        Assert.All(PromptStyles.InMenuOrder, s => Assert.False(string.IsNullOrEmpty(PromptStyles.Label(s))));
    }

    [Theory]
    [InlineData(-3, PromptStyle.Auto)]
    [InlineData(1, PromptStyle.PlayStation)]
    [InlineData(99, PromptStyle.Generic)]
    public void Menu_indexes_clamp_to_a_style(int index, PromptStyle expected)
    {
        Assert.Equal(expected, PromptStyles.FromIndex(index));
        Assert.Equal(Math.Clamp(index, 0, PromptStyles.InMenuOrder.Length - 1), PromptStyles.ToIndex(expected));
    }

    [Theory]
    [InlineData(PromptStyle.Auto, PadKind.PlayStation, "Auto (PlayStation)")]
    [InlineData(PromptStyle.Auto, PadKind.Xbox, "Auto (Xbox)")]
    [InlineData(PromptStyle.Auto, PadKind.SteamDeck, "Auto (Steam Deck)")]
    [InlineData(PromptStyle.Auto, PadKind.Nintendo, "Auto (Switch)")]
    [InlineData(PromptStyle.Auto, PadKind.Generic, "Auto (Generic)")]
    [InlineData(PromptStyle.Auto, PadKind.Unknown, "Auto")]
    [InlineData(PromptStyle.PlayStation, PadKind.Xbox, "PlayStation")]
    [InlineData(PromptStyle.Generic, PadKind.PlayStation, "Generic")]
    public void Auto_names_the_detected_pad(PromptStyle style, PadKind detected, string expected)
    {
        Assert.Equal(expected, PromptStyles.Label(style, detected));
    }
}
