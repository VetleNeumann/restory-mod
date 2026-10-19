using System;
using System.Collections.Generic;

namespace RestoryMod.Core;

/// <summary>
/// Undo steps for what features leave in the game (cloned objects, rewired navigation, listeners),
/// run when the plugin unloads so a hot reload starts from the game's own state.
/// </summary>
public sealed class Teardown
{
    private readonly List<Action> undos = new();

    public void Add(Action undo) => undos.Add(undo);

    /// <summary>
    /// Runs the undo steps newest first, so each one sees the state it was registered against.
    /// A step that throws does not stop the rest; its exception is returned instead. Each step runs once.
    /// </summary>
    public IReadOnlyList<Exception> Run()
    {
        var pending = undos.ToArray();
        undos.Clear();
        var failures = new List<Exception>();
        for (var i = pending.Length - 1; i >= 0; i--)
        {
            try
            {
                pending[i]();
            }
            catch (Exception e)
            {
                failures.Add(e);
            }
        }
        return failures;
    }
}
