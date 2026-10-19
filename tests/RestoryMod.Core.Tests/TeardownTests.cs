using System;
using System.Collections.Generic;
using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class TeardownTests
{
    [Fact]
    public void Undoes_newest_first()
    {
        var order = new List<string>();
        var teardown = new Teardown();
        teardown.Add(() => order.Add("clone row"));
        teardown.Add(() => order.Add("splice navigation"));

        teardown.Run();

        Assert.Equal(new[] { "splice navigation", "clone row" }, order);
    }

    [Fact]
    public void A_failing_undo_does_not_skip_the_others_and_is_reported()
    {
        var ran = new List<string>();
        var teardown = new Teardown();
        teardown.Add(() => ran.Add("clone row"));
        teardown.Add(() => throw new InvalidOperationException("row already gone"));
        teardown.Add(() => ran.Add("listener"));

        var failures = teardown.Run();

        Assert.Equal(new[] { "listener", "clone row" }, ran);
        Assert.Equal("row already gone", Assert.Single(failures).Message);
    }

    [Fact]
    public void Each_undo_runs_once()
    {
        var runs = 0;
        var teardown = new Teardown();
        teardown.Add(() => runs++);

        teardown.Run();
        teardown.Run();

        Assert.Equal(1, runs);
    }
}
