using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HintButtonTests
{
    private static bool Frame(HintButton button, HintButtonMode mode, bool pressed = false, bool down = false,
        bool atWorkbench = true) => button.Update(mode, pressed, down, atWorkbench);

    [Fact]
    public void Hold_shows_the_hint_on_press_and_keeps_it_lit_only_while_held()
    {
        var button = new HintButton();
        Assert.True(Frame(button, HintButtonMode.Hold, pressed: true, down: true));
        Assert.True(button.Lit);
        Assert.False(Frame(button, HintButtonMode.Hold, down: true));
        Assert.True(button.Lit);
        Frame(button, HintButtonMode.Hold);
        Assert.False(button.Lit);
    }

    [Fact]
    public void Toggle_keeps_the_hint_lit_after_letting_go_until_pressed_again()
    {
        var button = new HintButton();
        Assert.True(Frame(button, HintButtonMode.Toggle, pressed: true, down: true));
        Frame(button, HintButtonMode.Toggle);
        Assert.True(button.Lit);

        // The second press turns it off without showing it again.
        Assert.False(Frame(button, HintButtonMode.Toggle, pressed: true, down: true));
        Assert.False(button.Lit);
        Frame(button, HintButtonMode.Toggle);
        Assert.False(button.Lit);
    }

    [Fact]
    public void Leaving_the_workbench_ends_a_toggled_hint()
    {
        var button = new HintButton();
        Frame(button, HintButtonMode.Toggle, pressed: true, down: true);
        Frame(button, HintButtonMode.Toggle, atWorkbench: false);
        Assert.False(button.Lit);

        // Back at the workbench, the next press shows it again rather than switching it off.
        Assert.True(Frame(button, HintButtonMode.Toggle, pressed: true, down: true));
        Assert.True(button.Lit);
    }

    [Fact]
    public void Off_never_shows_the_hint_and_drops_one_already_lit()
    {
        var button = new HintButton();
        Frame(button, HintButtonMode.Toggle, pressed: true, down: true);
        Assert.False(Frame(button, HintButtonMode.Off, pressed: true, down: true));
        Assert.False(button.Lit);
    }

    [Fact]
    public void Switching_from_toggle_to_hold_lets_go_once_R2_is_up()
    {
        var button = new HintButton();
        Frame(button, HintButtonMode.Toggle, pressed: true, down: true);
        Frame(button, HintButtonMode.Toggle);
        Frame(button, HintButtonMode.Hold);
        Assert.False(button.Lit);
    }
}
