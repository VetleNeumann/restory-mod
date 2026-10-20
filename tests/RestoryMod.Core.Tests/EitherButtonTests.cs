using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class EitherButtonTests
{
    private const bool Cross = false;
    private const bool R2 = true;

    [Fact]
    public void Either_button_alone_presses_and_releases()
    {
        var button = new EitherButton();
        Assert.True(button.Press(R2));
        Assert.True(button.Release(R2));
        Assert.True(button.Press(Cross));
        Assert.True(button.Release(Cross));
    }

    [Fact]
    public void Pressing_the_second_while_the_first_is_held_does_not_press_again()
    {
        var button = new EitherButton();
        button.Press(Cross);
        Assert.False(button.Press(R2));
    }

    [Fact]
    public void Letting_go_of_one_keeps_it_pressed_until_both_are_up()
    {
        var button = new EitherButton();
        button.Press(Cross);
        button.Press(R2);
        Assert.False(button.Release(Cross));
        Assert.True(button.Release(R2));
    }

    [Fact]
    public void A_button_held_since_before_still_releases()
    {
        // Cross went down before Cleaning began listening, so its press was never seen.
        var button = new EitherButton();
        Assert.True(button.Release(Cross));
    }

    [Fact]
    public void Reset_forgets_held_buttons()
    {
        var button = new EitherButton();
        button.Press(R2);
        button.Reset();
        Assert.True(button.Press(Cross));
    }
}
