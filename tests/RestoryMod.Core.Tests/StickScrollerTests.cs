using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class StickScrollerTests
{
    // Viewport 100 px over 1100 px of content: 1000 px of scroll range, so one view height is 0.1.
    private const float Viewport = 100f;
    private const float Content = 1100f;

    /// <summary>No smoothing, so one frame shows the target speed directly.</summary>
    private static readonly StickScrollSettings Instant = StickScrollSettings.Default with { Smoothing = 0f };

    [Fact]
    public void Stick_inside_deadzone_does_not_scroll()
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(0.5f, 0f, 0.19f, 1f, Viewport, Content, Instant);

        Assert.Equal(0.5f, position);
    }

    [Fact]
    public void Full_tilt_up_scrolls_toward_top_at_configured_view_heights_per_second()
    {
        var scroller = new StickScroller();

        // 1.5 view heights = 150 px of a 1000 px range.
        var position = scroller.Advance(0.5f, 0f, 1f, 1f, Viewport, Content, Instant);

        Assert.Equal(0.65f, position, 4);
    }

    [Theory]
    [InlineData(-0.5625f, 0.4625f)] // halfway between the deadzones: 0.5 squared = quarter speed
    [InlineData(-0.95f, 0.35f)]     // past the outer deadzone: full speed
    [InlineData(-0.21f, 0.49997f)] // just past the inner deadzone: barely moving, no jump
    public void Tilt_is_rescaled_between_deadzones_then_curved(float stickY, float expected)
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(0.5f, 0f, stickY, 1f, Viewport, Content, Instant);

        Assert.Equal(expected, position, 5);
    }

    [Fact]
    public void Mostly_sideways_tilt_does_not_scroll()
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(0.5f, 0.9f, 0.4f, 1f, Viewport, Content, Instant);

        Assert.Equal(0.5f, position);
    }

    [Theory]
    [InlineData(0.95f, 1f, 1f)]
    [InlineData(0.05f, -1f, 0f)]
    public void Stops_at_the_ends_of_the_list(float start, float stickY, float expected)
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(start, 0f, stickY, 1f, Viewport, Content, Instant);

        Assert.Equal(expected, position);
    }

    [Fact]
    public void Speed_eases_in_from_rest()
    {
        var scroller = new StickScroller();

        // Speed rises as 150 px/s * (1 - e^(-t/0.07)); over the first 0.07 s that covers
        // 150 * 0.07 / e = 3.863 px of 1000, against 10.5 px at full speed from the start.
        var position = scroller.Advance(0.5f, 0f, 1f, 0.07f, Viewport, Content, StickScrollSettings.Default);

        Assert.Equal(0.503863f, position, 5);
    }

    [Fact]
    public void Reversing_after_hitting_the_top_starts_from_rest()
    {
        var scroller = new StickScroller();
        var settings = StickScrollSettings.Default;
        var atTop = scroller.Advance(0.999f, 0f, 1f, 1f, Viewport, Content, settings);

        var position = scroller.Advance(atTop, 0f, -1f, 0.07f, Viewport, Content, settings);

        // Same 3.863 px as easing in from rest, now downward.
        Assert.Equal(0.996137f, position, 5);
    }

    [Fact]
    public void Invert_flips_the_direction()
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(0.5f, 0f, 1f, 1f, Viewport, Content, Instant with { Invert = true });

        Assert.Equal(0.35f, position, 4);
    }

    [Fact]
    public void Content_that_fits_the_view_does_not_scroll()
    {
        var scroller = new StickScroller();

        var position = scroller.Advance(1f, 0f, -1f, 1f, Viewport, Viewport, Instant);

        Assert.Equal(1f, position);
    }

    [Fact]
    public void Comes_to_rest_shortly_after_release()
    {
        var scroller = new StickScroller();
        var settings = StickScrollSettings.Default;
        var position = scroller.Advance(0.5f, 0f, 1f, 0.5f, Viewport, Content, settings);
        Assert.True(scroller.IsMoving);

        // Half a second of 60 fps frames with the stick centred: about seven time constants.
        for (var frame = 0; frame < 30; frame++)
            position = scroller.Advance(position, 0f, 0f, 1f / 60f, Viewport, Content, settings);

        Assert.False(scroller.IsMoving);
    }

    [Theory]
    [InlineData(0f, 0.19f, false)] // inside the deadzone
    [InlineData(0.9f, 0.4f, false)] // mostly sideways
    [InlineData(0f, -0.5f, true)]
    public void Push_counts_only_when_it_would_scroll(float stickX, float stickY, bool expected)
    {
        Assert.Equal(expected, StickScroller.IsPushed(stickX, stickY, StickScrollSettings.Default));
    }

    [Fact]
    public void Distance_does_not_depend_on_frame_rate()
    {
        float Scroll(int fps)
        {
            var scroller = new StickScroller();
            var position = 0.9f;
            for (var frame = 0; frame < fps; frame++)
                position = scroller.Advance(position, 0f, -0.7f, 1f / fps, Viewport, Content, StickScrollSettings.Default);
            return position;
        }

        Assert.Equal(Scroll(30), Scroll(144), 4);
    }
}
