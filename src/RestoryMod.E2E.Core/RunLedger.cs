using System.Collections.Generic;

namespace RestoryMod.E2E;

public enum RunVerdict
{
    Passed,

    /// <summary>At least one check found marks other than expected.</summary>
    Failed,

    /// <summary>Halted on purpose just before the check asked for with --stop-at.</summary>
    Stopped,

    /// <summary>A step could not be carried out, so the run ended there.</summary>
    Aborted,
}

/// <summary>
/// One scenario run's checks: numbers them in order from 1, collects the failed ones while the run goes
/// on, halts before the --stop-at check, and reports the outcome as log lines.
/// </summary>
public sealed class RunLedger
{
    private readonly int? stopAt;
    private readonly List<string> report = new();
    private int number;
    private string label = "";
    private int failed;

    public RunLedger(int? stopAt) => this.stopAt = stopAt;

    public RunVerdict Verdict { get; private set; } = RunVerdict.Passed;

    /// <summary>Numbers the next check; false when the run halts here instead of making it.</summary>
    public bool BeginCheck(string label)
    {
        if (Verdict is RunVerdict.Stopped or RunVerdict.Aborted) return false;
        number++;
        this.label = label;
        if (number != stopAt) return true;
        Verdict = RunVerdict.Stopped;
        report.Add($"[E2E] STOPPED before #{number} {label}");
        return false;
    }

    /// <summary>Records that the check begun last found these mismatches.</summary>
    public void FailCheck(IReadOnlyList<MarkMismatch> mismatches, string? screenshot)
    {
        failed++;
        Verdict = RunVerdict.Failed;
        report.Add($"[E2E] FAIL #{number} {label}");
        foreach (var m in mismatches)
        {
            var actual = m.Actual is { } mark ? $"got {mark}" : "not at the workbench";
            report.Add($"[E2E]   {m.Thing}: expected {m.Expected}, {actual}");
        }
        if (screenshot is not null) report.Add($"[E2E]   screenshot: {screenshot}");
    }

    /// <summary>Ends the run at a step that could not be carried out; every later step is noise.</summary>
    public void Abort(string reason)
    {
        Verdict = RunVerdict.Aborted;
        report.Add($"[E2E] RESULT ABORTED after #{number}: {reason} ({failed} of {number} checks failed)");
    }

    /// <summary>Ends a run that got through all its steps.</summary>
    public void Finish() =>
        report.Add(failed == 0
            ? $"[E2E] RESULT PASSED: {number} checks"
            : $"[E2E] RESULT FAILED: {failed} of {number} checks failed");

    public IReadOnlyList<string> Report() => report;
}
