using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class NotepadToggleTests
{
    private static readonly NotepadKeyState Idle = new(KeyOpen: false, HoverIgnored: false);
    private static readonly NotepadKeyState KeyOpen = new(KeyOpen: true, HoverIgnored: false);

    private static NotepadKeyState Press(NotepadKeyState state, bool pointerInside = false) =>
        NotepadToggle.AfterKeyPress(visible: true, slidingMode: true, pinned: false, pointerInside, state);

    [Fact]
    public void Key_opens_a_peeking_notepad_and_closes_it_again()
    {
        var open = Press(Idle);
        Assert.Equal(KeyOpen, open);
        Assert.Equal(Idle, Press(open));
    }

    [Fact]
    public void Key_closes_a_notepad_the_pointer_holds_open()
    {
        // Hovering opened it; the key must still send it back to Peeking.
        var closed = Press(Idle, pointerInside: true);
        Assert.False(closed.KeyOpen);
        Assert.True(closed.HoverIgnored);
    }

    [Fact]
    public void Key_closes_a_key_open_notepad_while_hovered()
    {
        var closed = Press(KeyOpen, pointerInside: true);
        Assert.Equal(new NotepadKeyState(KeyOpen: false, HoverIgnored: true), closed);
    }

    [Fact]
    public void Key_reopens_a_notepad_closed_under_the_pointer()
    {
        var closed = Press(Idle, pointerInside: true);
        Assert.Equal(KeyOpen, Press(closed, pointerInside: true));
    }

    [Fact]
    public void Pointer_leaving_ends_ignored_hover()
    {
        Assert.False(NotepadToggle.KeepHoverIgnored(slidingMode: true, pinned: false, pointerInside: false,
            hoverIgnored: true));
        Assert.True(NotepadToggle.KeepHoverIgnored(slidingMode: true, pinned: false, pointerInside: true,
            hoverIgnored: true));
    }

    [Fact]
    public void Pinning_ends_ignored_hover_so_unpinning_under_the_pointer_stays_open()
    {
        // The pin button sits inside the Notepad, so the pointer is always inside when unpinning.
        Assert.False(NotepadToggle.KeepHoverIgnored(slidingMode: true, pinned: true, pointerInside: true,
            hoverIgnored: true));
    }

    [Fact]
    public void Key_is_ignored_while_hidden()
    {
        Assert.Equal(Idle, NotepadToggle.AfterKeyPress(visible: false, slidingMode: true, pinned: false,
            pointerInside: false, Idle));
    }

    [Fact]
    public void Key_is_ignored_away_from_the_sliding_workbench_panel()
    {
        // Outside sliding mode the game's own Notepad key handling applies.
        Assert.Equal(Idle, NotepadToggle.AfterKeyPress(visible: true, slidingMode: false, pinned: false,
            pointerInside: false, Idle));
    }

    [Fact]
    public void Key_is_ignored_while_pinned()
    {
        Assert.Equal(Idle, NotepadToggle.AfterKeyPress(visible: true, slidingMode: true, pinned: true,
            pointerInside: true, Idle));
    }

    [Fact]
    public void Pinning_clears_key_open_so_unpinning_returns_to_peeking()
    {
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: true, keyOpen: true));
    }

    [Fact]
    public void Leaving_the_workbench_clears_both()
    {
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: false, pinned: false, keyOpen: true));
        Assert.False(NotepadToggle.KeepHoverIgnored(slidingMode: false, pinned: false, pointerInside: true,
            hoverIgnored: true));
    }

    [Fact]
    public void Key_open_survives_other_state_changes_at_the_workbench()
    {
        // e.g. hidden for cleaning and shown again: still the same workbench session.
        Assert.True(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: false, keyOpen: true));
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: false, keyOpen: false));
    }
}
