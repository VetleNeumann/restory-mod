using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class RunLedgerTests
{
    [Fact]
    public void Stop_at_halts_the_run_just_before_that_check()
    {
        var run = new RunLedger(stopAt: 2);

        Assert.True(run.BeginCheck("before taking anything off"));
        Assert.False(run.BeginCheck("after removing back cover"));

        Assert.Equal(RunVerdict.Stopped, run.Verdict);
        Assert.Equal(new[] { "[E2E] STOPPED before #2 after removing back cover" }, run.Report());
    }

    [Fact]
    public void A_failed_check_is_recorded_and_the_run_goes_on_to_a_failed_result()
    {
        var run = new RunLedger(stopAt: null);

        run.BeginCheck("before taking anything off");
        run.BeginCheck("after removing back cover");
        run.FailCheck(new[]
        {
            new MarkMismatch("Screw 1", Mark.None, Mark.Pulse),
            new MarkMismatch("Battery", Mark.MissingGhost, null),
        }, screenshot: "e2e/fail-2.png");
        Assert.True(run.BeginCheck("after R2 released"));
        run.Finish();

        Assert.Equal(RunVerdict.Failed, run.Verdict);
        Assert.Equal(new[]
        {
            "[E2E] FAIL #2 after removing back cover",
            "[E2E]   Screw 1: expected None, got Pulse",
            "[E2E]   Battery: expected MissingGhost, not at the workbench",
            "[E2E]   screenshot: e2e/fail-2.png",
            "[E2E] RESULT FAILED: 1 of 3 checks failed",
        }, run.Report());
    }

    [Fact]
    public void A_failed_step_aborts_the_run_and_keeps_the_failures_before_it()
    {
        var run = new RunLedger(stopAt: null);

        run.BeginCheck("before taking anything off");
        run.FailCheck(new[] { new MarkMismatch("Back cover", Mark.Pulse, Mark.None) }, screenshot: null);
        run.Abort("Back cover never came off within 10s");

        Assert.Equal(RunVerdict.Aborted, run.Verdict);
        Assert.False(run.BeginCheck("after removing back cover"));
        Assert.Equal(new[]
        {
            "[E2E] FAIL #1 before taking anything off",
            "[E2E]   Back cover: expected Pulse, got None",
            "[E2E] RESULT ABORTED after #1: Back cover never came off within 10s (1 of 1 checks failed)",
        }, run.Report());
    }

    [Fact]
    public void A_run_whose_checks_all_pass_passes()
    {
        var run = new RunLedger(stopAt: null);

        run.BeginCheck("before taking anything off");
        run.BeginCheck("after removing back cover");
        run.Finish();

        Assert.Equal(RunVerdict.Passed, run.Verdict);
        Assert.Equal(new[] { "[E2E] RESULT PASSED: 2 checks" }, run.Report());
    }
}
