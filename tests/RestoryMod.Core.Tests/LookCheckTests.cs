using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class LookCheckTests
{
    private static readonly Rgba Yellow = new(1f, 1f, 0f, 1f);
    private static readonly Rgba GameDamaged = new(0.9f, 0.1f, 0.1f, 1f);

    private static DrawnLook? Classify(float r, float g, float b, float width, FillPattern pattern) =>
        LookCheck.Classify(new DrawnHalf(new Rgba(r, g, b, 1f), width, pattern), Yellow,
            look => look == Look.Damaged ? GameDamaged : null);

    [Fact]
    public void A_solid_outline_in_the_pulse_colour_is_the_ready_look()
    {
        Assert.Equal(new DrawnLook(Look.Ready, Hidden: false), Classify(1f, 1f, 0f, 1f, FillPattern.Solid));
    }

    [Fact]
    public void A_dotted_red_outline_is_the_missing_look()
    {
        Assert.Equal(new DrawnLook(Look.Missing, Hidden: false), Classify(1f, 0.2f, 0f, 1f, FillPattern.Dots));
    }

    [Fact]
    public void A_solid_outline_in_the_games_damaged_colour_is_the_damaged_look_not_the_missing_one()
    {
        Assert.Equal(new DrawnLook(Look.Damaged, Hidden: false), Classify(0.9f, 0.1f, 0.1f, 1f, FillPattern.Solid));
    }

    [Fact]
    public void An_outline_with_the_pulses_own_fill_is_a_screw_in_plain_view()
    {
        Assert.Equal(new DrawnLook(Look.Screw, Hidden: false), Classify(1f, 1f, 0f, 1f, FillPattern.Pulse));
    }

    [Fact]
    public void A_thin_dim_outline_without_fill_is_a_screw_in_the_hidden_style()
    {
        Assert.Equal(new DrawnLook(Look.Screw, Hidden: true), Classify(0.3f, 0.3f, 0f, 0.5f, FillPattern.Solid));
    }

    [Fact]
    public void An_outline_no_look_draws_is_none()
    {
        Assert.Null(Classify(0f, 0f, 1f, 1f, FillPattern.Solid));
    }
}
