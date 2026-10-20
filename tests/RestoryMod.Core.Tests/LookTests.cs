using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class LookTests
{
    private static readonly Rgba White = new(1f, 1f, 1f, 1f);
    private static readonly Rgba Yellow = new(1f, 1f, 0f, 1f);

    [Theory]
    [InlineData(HintKind.Part, Look.Ready)]
    [InlineData(HintKind.LoosePart, Look.Ready)]
    [InlineData(HintKind.Missing, Look.Missing)]
    public void Parts_and_missing_sockets_wear_their_look(HintKind kind, Look look)
    {
        Assert.Equal(look, Looks.OfHint(kind));
    }

    [Theory]
    [InlineData(HintKind.Screw)]
    [InlineData(HintKind.ScrewSlot)]
    public void Screws_wear_the_screw_look(HintKind kind)
    {
        Assert.Equal(Look.Screw, Looks.OfHint(kind));
    }

    [Fact]
    public void A_screw_in_view_is_the_pulse_itself_with_its_own_fill()
    {
        var paint = Looks.Paint(Look.Screw, Yellow, hidden: false);
        Assert.Equal(Yellow, paint.Outline);
        Assert.Equal(FillPattern.Pulse, paint.Pattern);
        Assert.Equal(1f, paint.Width);
    }

    [Fact]
    public void A_ready_ghost_takes_the_pulse_color_and_its_light_fill_thickens_as_it_brightens()
    {
        var dim = Looks.Paint(Look.Ready, White, hidden: false);
        var bright = Looks.Paint(Look.Ready, Yellow, hidden: false);

        Assert.Equal(White, dim.Outline);
        Assert.Equal(Yellow, bright.Outline);
        Assert.Equal(0.10f, dim.Fill.A, 3);
        Assert.Equal(0.22f, bright.Fill.A, 3);
        Assert.Equal(1f, bright.Width);
    }

    [Fact]
    public void A_missing_socket_keeps_the_pulse_brightness_in_red()
    {
        var paint = Looks.Paint(Look.Missing, Yellow, hidden: false);
        Assert.Equal(new Rgba(1f, 0.2f, 0f, 1f), paint.Outline);
        Assert.Equal(1f, paint.Fill.R);
        Assert.Equal(0.2f, paint.Fill.G, 3);
    }

    [Fact]
    public void A_missing_socket_is_dotted_so_it_reads_as_a_gap()
    {
        var missing = Looks.Paint(Look.Missing, Yellow, hidden: false);
        var ready = Looks.Paint(Look.Ready, Yellow, hidden: false);

        Assert.Equal(FillPattern.Dots, missing.Pattern);
        Assert.Equal(FillPattern.Solid, ready.Pattern);
        // Dots cover a fraction of the ghost, so each is drawn stronger than a solid fill.
        Assert.True(missing.Fill.A > ready.Fill.A);
    }

    [Theory]
    [InlineData(Look.Ready)]
    [InlineData(Look.Missing)]
    [InlineData(Look.Screw)]
    public void The_hidden_style_is_a_thinner_dimmer_outline_with_no_fill(Look look)
    {
        var inView = Looks.Paint(look, Yellow, hidden: false);
        var hidden = Looks.Paint(look, Yellow, hidden: true);

        Assert.True(hidden.Width < inView.Width);
        Assert.True(hidden.Outline.R + hidden.Outline.G + hidden.Outline.B < inView.Outline.R + inView.Outline.G + inView.Outline.B);
        Assert.Equal(FillPattern.Solid, hidden.Pattern);
        Assert.Equal(0f, hidden.Fill.A);
    }
}
