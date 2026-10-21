using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace RestoryMod.E2E;

/// <summary>
/// Collects every error logged while it is open, the mod's and the game's alike (BepInEx passes Unity's
/// log on), for a check that a step logged none.
/// </summary>
internal sealed class ErrorWatch : ILogListener
{
    private readonly List<string> errors = new();

    public ErrorWatch() => Logger.Listeners.Add(this);

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if ((eventArgs.Level & (LogLevel.Error | LogLevel.Fatal)) != 0)
            errors.Add($"{eventArgs.Source.SourceName}: {eventArgs.Data}");
    }

    /// <summary>"none", or the first error logged and how many there were.</summary>
    public string Errors() => errors.Count == 0 ? "none" : $"{errors.Count}, first: {errors[0]}";

    public void Dispose() => Logger.Listeners.Remove(this);
}
