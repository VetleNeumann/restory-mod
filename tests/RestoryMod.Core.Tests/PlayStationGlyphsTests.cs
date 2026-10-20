using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PlayStationGlyphsTests
{
    [Theory]
    [InlineData(4, "PS5Cross")]
    [InlineData(5, "PS5_Circle")]
    [InlineData(6, "PS5_Circle")]
    // The game's Square sprite is misnamed as a second triangle.
    [InlineData(7, "PS5_Triangle-1")]
    [InlineData(8, "PS5_Triangle")]
    [InlineData(9, "PS5_Triangle")]
    [InlineData(10, "PS5_L1")]
    [InlineData(11, "PS5_L2")]
    [InlineData(12, "PS5_R1")]
    [InlineData(13, "PS5_R2")]
    [InlineData(14, "PS5_Share")]
    [InlineData(15, "PS5_Options")]
    // Stick movement: the PS5 stick sprites read "L3"/"R3", which means pressing the stick, and the
    // game's PlayStation template draws "?"; the Xbox "L"/"R" badges look the same as the PS5 set.
    [InlineData(0, "XBOX_LS")]
    [InlineData(1, "XBOX_LS")]
    [InlineData(23, "XBOX_LS")]
    [InlineData(2, "XBOX_RS")]
    [InlineData(3, "XBOX_RS")]
    [InlineData(24, "XBOX_RS")]
    [InlineData(17, "PS5_LeftStick_click")]
    [InlineData(18, "PS5_RightStick_click")]
    [InlineData(19, "PS5_DPad_Top")]
    [InlineData(20, "PS5_DPad_Right")]
    [InlineData(21, "PS5_DPad_Bottom")]
    [InlineData(22, "PS5_DPad_Left")]
    [InlineData(25, "PS5_DPad")]
    public void Gamepad_template_elements_map_to_the_games_unused_PS5_sprites(int elementId, string sprite)
    {
        Assert.Equal(sprite, PlayStationGlyphs.SpriteFor(elementId));
    }

    [Theory]
    [InlineData(16)] // PS button: no sprite, keep the game's art.
    [InlineData(-1)]
    [InlineData(26)]
    public void Elements_without_a_sprite_keep_the_games_art(int elementId)
    {
        Assert.Null(PlayStationGlyphs.SpriteFor(elementId));
    }

    [Fact]
    public void Every_sprite_name_is_listed_for_preloading()
    {
        for (var id = -1; id <= 26; id++)
        {
            var sprite = PlayStationGlyphs.SpriteFor(id);
            if (sprite is not null) Assert.Contains(sprite, PlayStationGlyphs.AllSprites);
        }
    }
}
