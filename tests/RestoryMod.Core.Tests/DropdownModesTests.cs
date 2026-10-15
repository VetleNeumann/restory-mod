using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class DropdownModesTests
{
    private const int RowA = 1;
    private const int RowB = 2;

    [Fact]
    public void Opened_by_cursor_click_starts_in_pointer_mode()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Pointer, rowUnderCursor: RowA);

        Assert.Equal(DropdownMode.Pointer, modes.Mode);
        Assert.False(modes.SuppressClick);
        Assert.True(modes.SuppressSubmit);
        Assert.False(modes.CursorHidden);
    }

    [Fact]
    public void Opened_by_submit_starts_in_nav_mode_with_cursor_hidden()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        Assert.Equal(DropdownMode.Nav, modes.Mode);
        Assert.True(modes.SuppressClick);
        Assert.False(modes.SuppressSubmit);
        Assert.True(modes.CursorHidden);
    }

    [Fact]
    public void Dpad_press_in_pointer_mode_switches_to_nav_mode()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Pointer, rowUnderCursor: RowA);

        modes.DpadPressed(rowUnderCursor: RowA);

        Assert.Equal(DropdownMode.Nav, modes.Mode);
        Assert.True(modes.CursorHidden);
    }

    [Fact]
    public void Cursor_moving_within_its_starting_row_shows_it_but_stays_in_nav_mode()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        modes.CursorMoved(rowUnderCursor: RowA);

        Assert.Equal(DropdownMode.Nav, modes.Mode);
        Assert.False(modes.CursorHidden);
        Assert.True(modes.SuppressClick);
    }

    [Fact]
    public void Cursor_reaching_another_row_switches_to_pointer_mode()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        modes.CursorMoved(rowUnderCursor: RowB);

        Assert.Equal(DropdownMode.Pointer, modes.Mode);
        Assert.False(modes.SuppressClick);
        Assert.True(modes.SuppressSubmit);
    }

    [Fact]
    public void Cursor_leaving_the_list_stays_in_nav_mode()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        modes.CursorMoved(rowUnderCursor: null);

        Assert.Equal(DropdownMode.Nav, modes.Mode);
        Assert.False(modes.CursorHidden);
    }

    [Fact]
    public void Nav_mode_begun_off_the_list_ends_on_reaching_any_row()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: null);

        modes.CursorMoved(rowUnderCursor: null);
        Assert.Equal(DropdownMode.Nav, modes.Mode);

        modes.CursorMoved(rowUnderCursor: RowA);
        Assert.Equal(DropdownMode.Pointer, modes.Mode);
    }

    [Fact]
    public void Dpad_press_starts_nav_mode_from_the_row_the_cursor_is_on_now()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);
        modes.CursorMoved(rowUnderCursor: RowB);

        modes.DpadPressed(rowUnderCursor: RowB);
        modes.CursorMoved(rowUnderCursor: RowB);
        Assert.Equal(DropdownMode.Nav, modes.Mode);

        modes.CursorMoved(rowUnderCursor: RowA);
        Assert.Equal(DropdownMode.Pointer, modes.Mode);
    }

    [Fact]
    public void A_pressed_in_nav_mode_never_clicks_even_after_its_pick_closes_the_list()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        modes.SubmitPressed();
        modes.Close();
        Assert.True(modes.SuppressClick);

        modes.SubmitReleased();
        Assert.False(modes.SuppressClick);
    }

    [Fact]
    public void A_pressed_in_pointer_mode_clicks_normally()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Pointer, rowUnderCursor: RowA);

        modes.SubmitPressed();
        modes.Close();

        Assert.False(modes.SuppressClick);
    }

    [Fact]
    public void Closed_header_takes_A_as_submit_until_the_cursor_moves()
    {
        var modes = new DropdownModes();
        Assert.False(modes.SuppressHeaderSubmit);

        modes.CursorMoved(rowUnderCursor: null);
        Assert.True(modes.SuppressHeaderSubmit);

        modes.DpadPressed(rowUnderCursor: null);
        Assert.False(modes.SuppressHeaderSubmit);
    }

    [Fact]
    public void Header_submit_rule_carries_over_from_inside_the_list()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);
        modes.CursorMoved(rowUnderCursor: RowB);
        modes.Close();

        Assert.True(modes.SuppressHeaderSubmit);
    }

    [Fact]
    public void Only_a_held_dpad_moves_the_highlight_in_an_open_list()
    {
        var modes = new DropdownModes();
        Assert.False(modes.SuppressStickMove);

        modes.Open(DropdownMode.Pointer, rowUnderCursor: RowA);
        Assert.True(modes.SuppressStickMove);

        modes.DpadHeld(true, rowUnderCursor: RowA);
        Assert.False(modes.SuppressStickMove);
        Assert.Equal(DropdownMode.Nav, modes.Mode);

        modes.DpadHeld(false, rowUnderCursor: RowA);
        Assert.True(modes.SuppressStickMove);
    }

    [Fact]
    public void Holding_the_dpad_is_one_press_not_one_per_frame()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Pointer, rowUnderCursor: RowA);
        modes.DpadHeld(true, rowUnderCursor: RowA);
        modes.CursorMoved(rowUnderCursor: RowB);

        modes.DpadHeld(true, rowUnderCursor: RowB);

        Assert.Equal(DropdownMode.Pointer, modes.Mode);
    }

    [Fact]
    public void Closing_suppresses_nothing_and_shows_the_cursor()
    {
        var modes = new DropdownModes();
        modes.Open(DropdownMode.Nav, rowUnderCursor: RowA);

        modes.Close();

        Assert.False(modes.IsOpen);
        Assert.False(modes.SuppressClick);
        Assert.False(modes.SuppressSubmit);
        Assert.False(modes.CursorHidden);
    }
}
