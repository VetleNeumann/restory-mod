using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class NotepadToggleTests
{
    [Fact]
    public void Key_opens_a_peeking_notepad_and_closes_it_again()
    {
        var open = NotepadToggle.AfterKeyPress(visible: true, slidingMode: true, pinned: false, keyOpen: false);
        Assert.True(open);
        Assert.False(NotepadToggle.AfterKeyPress(visible: true, slidingMode: true, pinned: false, keyOpen: open));
    }

    [Fact]
    public void Key_is_ignored_while_hidden()
    {
        Assert.False(NotepadToggle.AfterKeyPress(visible: false, slidingMode: true, pinned: false, keyOpen: false));
    }

    [Fact]
    public void Key_is_ignored_away_from_the_sliding_workbench_panel()
    {
        // Outside sliding mode the game's own Notepad key handling applies.
        Assert.False(NotepadToggle.AfterKeyPress(visible: true, slidingMode: false, pinned: false, keyOpen: false));
    }

    [Fact]
    public void Key_is_ignored_while_pinned()
    {
        Assert.False(NotepadToggle.AfterKeyPress(visible: true, slidingMode: true, pinned: true, keyOpen: false));
    }

    [Fact]
    public void Pinning_clears_key_open_so_unpinning_returns_to_peeking()
    {
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: true, keyOpen: true));
    }

    [Fact]
    public void Leaving_the_workbench_clears_key_open()
    {
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: false, pinned: false, keyOpen: true));
    }

    [Fact]
    public void Key_open_survives_other_state_changes_at_the_workbench()
    {
        // e.g. hidden for cleaning and shown again: still the same workbench session.
        Assert.True(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: false, keyOpen: true));
        Assert.False(NotepadToggle.KeepKeyOpen(slidingMode: true, pinned: false, keyOpen: false));
    }
}
