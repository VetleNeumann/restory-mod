using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HoverGraceTests
{
    private const float Window = 0.15f;
    private const float FrameTime = 0.01f;

    private readonly HoverGrace<string> grace = new();

    /// <summary>Feeds the frame that falls at <paramref name="now"/> at 100 fps.</summary>
    private string? At(float now, HoverKind kind, string? hovered = null, float window = Window) =>
        grace.Next((int)System.MathF.Round(now / FrameTime), now, kind, hovered, window);

    [Fact]
    public void Part_stays_hovered_just_after_the_cursor_leaves_it()
    {
        At(1.00f, HoverKind.Eligible, "screw");
        At(1.01f, HoverKind.Nothing);

        Assert.Equal("screw", At(1.02f, HoverKind.Nothing));
    }

    [Fact]
    public void Grace_ends_once_the_window_has_passed()
    {
        At(1.00f, HoverKind.Eligible, "screw");
        for (var now = 1.01f; now < 1.15f; now += FrameTime) At(now, HoverKind.Nothing);

        Assert.Null(At(1.16f, HoverKind.Nothing));
    }

    [Fact]
    public void Window_counts_from_the_last_frame_the_cursor_was_on_it()
    {
        At(1.00f, HoverKind.Eligible, "screw");
        At(1.01f, HoverKind.Eligible, "screw");
        At(1.10f, HoverKind.Eligible, "screw");

        Assert.Equal("screw", At(1.11f, HoverKind.Nothing));
    }

    [Fact]
    public void Something_else_under_the_cursor_ends_grace_for_good()
    {
        // e.g. the cursor crosses a panel or an attached part, then lands on empty bench.
        At(1.00f, HoverKind.Eligible, "screw");

        Assert.Null(At(1.01f, HoverKind.Other));
        Assert.Null(At(1.02f, HoverKind.Nothing));
    }

    [Fact]
    public void Another_part_under_the_cursor_wins_and_becomes_the_one_that_lingers()
    {
        At(1.00f, HoverKind.Eligible, "screw");

        Assert.Null(At(1.01f, HoverKind.Eligible, "fan"));
        Assert.Equal("fan", At(1.02f, HoverKind.Nothing));
    }

    [Fact]
    public void A_zero_window_turns_grace_off()
    {
        // Also how mouse play gets none.
        At(1.00f, HoverKind.Eligible, "screw", window: 0f);

        Assert.Null(At(1.01f, HoverKind.Nothing, window: 0f));
    }

    [Fact]
    public void Forgetting_drops_the_lingering_part()
    {
        // e.g. the player pressed the button: grace is used up.
        At(1.00f, HoverKind.Eligible, "screw");
        grace.Forget();

        Assert.Null(At(1.01f, HoverKind.Nothing));
    }

    [Fact]
    public void A_skipped_frame_drops_the_lingering_part()
    {
        // The hover state was left for a frame (e.g. a drag): what it hovered before must not come back.
        At(1.00f, HoverKind.Eligible, "screw");

        Assert.Null(At(1.02f, HoverKind.Nothing));
    }
}
