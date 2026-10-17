using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class MailSelectionTests
{
    [Fact]
    public void Down_selects_the_mail_below()
    {
        Assert.Equal(2, MailSelection.Step(count: 4, current: 1, step: 1));
    }

    [Fact]
    public void Down_on_the_bottom_mail_stays_there()
    {
        Assert.Equal(3, MailSelection.Step(count: 4, current: 3, step: 1));
    }

    [Fact]
    public void Up_on_the_top_mail_stays_there()
    {
        Assert.Equal(0, MailSelection.Step(count: 4, current: 0, step: -1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void With_none_selected_either_way_selects_the_top_mail(int step)
    {
        Assert.Equal(0, MailSelection.Step(count: 4, current: -1, step));
    }

    [Fact]
    public void An_empty_folder_has_nothing_to_select()
    {
        Assert.Equal(-1, MailSelection.Step(count: 0, current: -1, step: 1));
    }

    [Fact]
    public void The_selected_mail_stays_selected_when_new_mail_arrives_above_it()
    {
        var before = new[] { "b", "c" };
        var after = new[] { "a", "b", "c" };
        Assert.Equal(1, MailSelection.AfterChange(before, selected: 0, after));
    }

    [Fact]
    public void A_taken_order_hands_the_selection_to_the_mail_that_takes_its_place()
    {
        var before = new[] { "a", "b", "c" };
        var after = new[] { "a", "c" };
        Assert.Equal(1, MailSelection.AfterChange(before, selected: 1, after));
    }

    [Fact]
    public void The_selected_mail_stays_selected_when_mail_above_it_leaves()
    {
        var before = new[] { "a", "b", "c" };
        var after = new[] { "b", "c" };
        Assert.Equal(1, MailSelection.AfterChange(before, selected: 2, after));
    }

    [Fact]
    public void Taking_the_bottom_order_selects_the_mail_above_it()
    {
        var before = new[] { "a", "b", "c" };
        var after = new[] { "a", "b" };
        Assert.Equal(1, MailSelection.AfterChange(before, selected: 2, after));
    }

    [Fact]
    public void Taking_the_only_order_leaves_nothing_selected()
    {
        Assert.Equal(-1, MailSelection.AfterChange(new[] { "a" }, selected: 0, after: new string[0]));
    }

    [Fact]
    public void Mail_arriving_in_an_empty_folder_is_selected()
    {
        Assert.Equal(0, MailSelection.AfterChange(new string[0], selected: -1, after: new[] { "a" }));
    }
}
