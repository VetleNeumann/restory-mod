using System;

namespace RestoryMod;

/// <summary>
/// Game references one plugin copy leaves for the next across a hot reload (scripts/dev.sh hot), for
/// setup the game does once per scene that the new copy would otherwise miss until the next scene.
/// Kept as AppDomain data because the two copies share no types: only game and Unity objects go in.
/// </summary>
internal static class Handoff
{
    private const string Prefix = "RestoryMod.Handoff.";

    public static void Leave(string key, params object?[] values) =>
        AppDomain.CurrentDomain.SetData(Prefix + key, values);

    /// <summary>What the previous copy left under this key, once; null on a normal start.</summary>
    public static object?[]? Take(string key)
    {
        var values = AppDomain.CurrentDomain.GetData(Prefix + key) as object?[];
        AppDomain.CurrentDomain.SetData(Prefix + key, null);
        return values;
    }
}
