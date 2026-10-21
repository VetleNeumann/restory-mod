using System;

namespace RestoryMod;

/// <summary>
/// Game references one plugin copy leaves for the next across a hot reload (scripts/dev.sh hot), for
/// setup the game does once per scene that the new copy would otherwise miss until the next scene.
/// Kept as AppDomain data because the two copies share no types: only game and Unity objects go in.
/// Each feature gets its own, keyed by its manifest's name (<see cref="FeatureContext.Handoff"/>).
/// </summary>
internal sealed class Handoff(string key)
{
    private const string Prefix = "RestoryMod.Handoff.";

    public void Leave(params object?[] values) =>
        AppDomain.CurrentDomain.SetData(Prefix + key, values);

    /// <summary>What the previous copy left, once; null on a normal start.</summary>
    public object?[]? Take()
    {
        var values = AppDomain.CurrentDomain.GetData(Prefix + key) as object?[];
        AppDomain.CurrentDomain.SetData(Prefix + key, null);
        return values;
    }
}
