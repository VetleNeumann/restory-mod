using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class TubeModeTests
{
    [Fact]
    public void A_part_taken_out_of_a_tube_means_taking_apart_even_as_the_next_slides_forward()
    {
        Assert.True(TubeMode.TakingApart(wasTakingApart: false, partsBefore: 6, partsNow: 5, gameSays: false));
    }

    [Fact]
    public void A_part_pushed_into_a_tube_means_putting_back()
    {
        Assert.False(TubeMode.TakingApart(wasTakingApart: true, partsBefore: 4, partsNow: 5, gameSays: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parts_moving_inside_a_tube_keep_the_mode(bool was)
    {
        Assert.Equal(was, TubeMode.TakingApart(wasTakingApart: was, partsBefore: 5, partsNow: 5, gameSays: !was));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_tube_not_seen_before_follows_the_game(bool game)
    {
        Assert.Equal(game, TubeMode.TakingApart(wasTakingApart: !game, partsBefore: null, partsNow: 5, gameSays: game));
    }
}
