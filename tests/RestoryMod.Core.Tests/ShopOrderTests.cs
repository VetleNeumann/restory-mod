using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ShopOrderTests
{
    [Fact]
    public void Steps_follow_the_notepad_which_lists_the_device_parts_last_first()
    {
        var ranks = ShopOrder.StepRanks(new[] { "case", "board", "screen" }, isStep: _ => true);
        Assert.Equal(new Dictionary<string, int> { ["screen"] = 0, ["board"] = 1, ["case"] = 2 }, ranks);
    }

    [Fact]
    public void A_part_listed_twice_takes_the_place_of_its_first_listing()
    {
        // The game indexes the list by first listing, so "knob" sorts as if listed only at the front.
        var ranks = ShopOrder.StepRanks(new[] { "knob", "board", "knob" }, isStep: _ => true);
        Assert.Equal(new Dictionary<string, int> { ["board"] = 0, ["knob"] = 1 }, ranks);
    }

    [Fact]
    public void Parts_the_notepad_leaves_out_get_no_step()
    {
        var ranks = ShopOrder.StepRanks(new[] { "case", "screw", "board" }, isStep: part => part != "screw");
        Assert.Equal(new Dictionary<string, int> { ["board"] = 0, ["case"] = 1 }, ranks);
    }

    // Store rows as "name model step"; NoModel / NoStep spelled as "-".
    private static ShopKey KeyOf(string row)
    {
        var fields = row.Split(' ');
        return new ShopKey(
            fields[1] == "-" ? ShopOrder.NoModel : int.Parse(fields[1]),
            fields[2] == "-" ? ShopOrder.NoStep : int.Parse(fields[2]));
    }

    private static string[] Arrange(bool reverse, params string[] rows) =>
        ShopOrder.Arrange(rows, KeyOf, reverse).Select(row => row.Split(' ')[0]).ToArray();

    [Fact]
    public void Parts_group_by_model_then_follow_its_reassembly_order()
    {
        var order = Arrange(false, "radioCase 1 1", "tvScreen 0 0", "radioBoard 1 0", "tvCase 0 1");
        Assert.Equal(new[] { "tvScreen", "tvCase", "radioBoard", "radioCase" }, order);
    }

    [Fact]
    public void Reversing_flips_steps_within_each_model_but_not_the_models()
    {
        var order = Arrange(true, "tvScreen 0 0", "tvCase 0 1", "radioBoard 1 0", "radioCase 1 1");
        Assert.Equal(new[] { "tvCase", "tvScreen", "radioCase", "radioBoard" }, order);
    }

    [Fact]
    public void Parts_that_are_no_step_stay_at_the_end_of_their_model_either_way()
    {
        var rows = new[] { "tvScrew 0 -", "tvScreen 0 0", "tvCase 0 1", "radioBoard 1 0" };
        Assert.Equal(new[] { "tvScreen", "tvCase", "tvScrew", "radioBoard" }, Arrange(false, rows));
        Assert.Equal(new[] { "tvCase", "tvScreen", "tvScrew", "radioBoard" }, Arrange(true, rows));
    }

    [Fact]
    public void Parts_of_no_model_go_last_in_the_game_order()
    {
        var order = Arrange(true, "glue - -", "tvScreen 0 0", "solder - -", "tvCase 0 1");
        Assert.Equal(new[] { "tvCase", "tvScreen", "glue", "solder" }, order);
    }

    [Fact]
    public void Ties_keep_the_game_order()
    {
        var rows = new[] { "knobB 0 0", "knobA 0 0", "knobC 0 0" };
        Assert.Equal(new[] { "knobB", "knobA", "knobC" }, Arrange(false, rows));
        Assert.Equal(new[] { "knobB", "knobA", "knobC" }, Arrange(true, rows));
    }
}
