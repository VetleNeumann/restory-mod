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
    public void Screws_keep_the_pulse_look(HintKind kind)
    {
        Assert.Null(Looks.OfHint(kind));
    }

    [Fact]
    public void A_ready_ghost_takes_the_pulse_color_and_its_fill_thickens_as_it_brightens()
    {
        var dim = Looks.Paint(Look.Ready, faint: false, White, hidden: false);
        var bright = Looks.Paint(Look.Ready, faint: false, Yellow, hidden: false);

        Assert.Equal(White, dim.Outline);
        Assert.Equal(Yellow, bright.Outline);
        Assert.Equal(0.15f, dim.Fill.A, 3);
        Assert.Equal(0.35f, bright.Fill.A, 3);
        Assert.Equal(1f, bright.Width);
    }

    [Fact]
    public void A_missing_socket_keeps_the_pulse_brightness_in_red()
    {
        var paint = Looks.Paint(Look.Missing, faint: false, Yellow, hidden: false);
        Assert.Equal(new Rgba(1f, 0.2f, 0f, 1f), paint.Outline);
        Assert.Equal(1f, paint.Fill.R);
        Assert.Equal(0.2f, paint.Fill.G, 3);
    }

    [Fact]
    public void A_faint_look_holds_still_and_dark()
    {
        var dim = Looks.Paint(Look.Ready, faint: true, White, hidden: false);
        var bright = Looks.Paint(Look.Ready, faint: true, Yellow, hidden: false);

        Assert.Equal(dim, bright);
        Assert.Equal(new Rgba(0.45f, 0.45f, 0f, 1f), dim.Outline);
        Assert.Equal(0.08f, dim.Fill.A, 3);
    }

    [Theory]
    [InlineData(Look.Ready, false)]
    [InlineData(Look.Ready, true)]
    [InlineData(Look.Missing, false)]
    public void The_hidden_style_is_thinner_and_dimmer_than_the_same_look_in_view(Look look, bool faint)
    {
        var inView = Looks.Paint(look, faint, Yellow, hidden: false);
        var hidden = Looks.Paint(look, faint, Yellow, hidden: true);

        Assert.True(hidden.Width < inView.Width);
        Assert.True(hidden.Outline.R + hidden.Outline.G + hidden.Outline.B < inView.Outline.R + inView.Outline.G + inView.Outline.B);
        Assert.True(hidden.Fill.A < inView.Fill.A);
    }
}
