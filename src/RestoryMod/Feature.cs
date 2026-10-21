using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod;

/// <summary>
/// One feature of the mod: the manifest of game targets it uses, and the setup it does when the plugin loads.
/// </summary>
internal interface IFeature
{
    FeatureManifest Manifest { get; }

    /// <summary>
    /// Binds config, applies patches, adds pollers and registers undo steps. Runs once per plugin copy: on a
    /// normal start, and after a hot reload (scripts/dev.sh hot), when the scene is already set up and the
    /// feature picks up what scene-load hooks would have done (finding live objects, or taking the old copy's
    /// references from <see cref="FeatureContext.Handoff"/>). The plugin calls it whatever the manifest's
    /// targets: gate pollers here and patches in their Prepare().
    /// </summary>
    void Load(FeatureContext ctx);
}

/// <summary>What one feature's Load can set up.</summary>
internal sealed class FeatureContext
{
    private readonly FeatureLoader loader;

    internal FeatureContext(FeatureManifest manifest, ConfigFile config, FeatureLoader loader)
    {
        this.loader = loader;
        Config = config;
        Handoff = new Handoff(manifest.Name);
    }

    public ConfigFile Config { get; }

    /// <summary>What this feature leaves its next copy across a hot reload, and takes from the last one.</summary>
    public Handoff Handoff { get; }

    /// <summary>
    /// Applies each patch class on its own, so one broken target (e.g. after a game update) only disables
    /// that patch. Patch classes implement a static Prepare() that returns false when their targets are missing.
    /// </summary>
    public void Patch(params Type[] patchClasses)
    {
        foreach (var patchClass in patchClasses) loader.Patch(patchClass);
    }

    public T AddPoller<T>() where T : Component => loader.Host.AddComponent<T>();

    /// <summary>
    /// An undo step for what the feature leaves in the game, run on unload (newest first). Under ScriptEngine
    /// unloading is a reload, and the new copy should find the game as it was before the mod.
    /// </summary>
    public void OnUnload(Action undo) => Plugin.Teardown.Add(undo);
}

/// <summary>
/// Loads every feature, then checks that none was left out: a patch class no feature registers never runs,
/// and a manifest missing from <see cref="FeatureManifests.All"/> is never checked by TargetCheck.
/// </summary>
internal sealed class FeatureLoader(Harmony harmony, ConfigFile config, GameObject host, ManualLogSource log)
{
    private readonly HashSet<Type> registered = new();
    private readonly List<string> applied = new();
    private readonly List<string> skipped = new();

    public GameObject Host => host;

    public void Load(IReadOnlyList<IFeature> features, IEnumerable<FeatureManifest> loadedElsewhere)
    {
        foreach (var feature in features)
        {
            try
            {
                feature.Load(new FeatureContext(feature.Manifest, config, this));
            }
            catch (Exception e)
            {
                log.LogError($"Feature {feature.Manifest} failed to load: {e}");
            }
        }

        log.LogInfo($"Patches applied: [{string.Join(", ", applied)}]; skipped: [{string.Join(", ", skipped)}]");
        CheckNothingLeftOut(features, loadedElsewhere);
    }

    internal void Patch(Type patchClass)
    {
        registered.Add(patchClass);
        try
        {
            var patched = harmony.CreateClassProcessor(patchClass).Patch();
            if (patched is { Count: > 0 })
                applied.Add(patchClass.Name);
            else
                skipped.Add(patchClass.Name);
        }
        catch (Exception e)
        {
            skipped.Add(patchClass.Name);
            log.LogError($"Patch {patchClass.Name} failed: {e}");
        }
    }

    private void CheckNothingLeftOut(IReadOnlyList<IFeature> features, IEnumerable<FeatureManifest> loadedElsewhere)
    {
        var unregistered = AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly)
            .Where(t => t.IsDefined(typeof(HarmonyPatch), inherit: false) && !registered.Contains(t))
            .Select(t => t.Name)
            .ToList();
        if (unregistered.Count > 0)
            log.LogError($"Patch classes no feature registers, so they never run: [{string.Join(", ", unregistered)}]");

        var shared = features.GroupBy(f => f.Manifest).Where(g => g.Count() > 1).Select(g => g.Key.Name).ToList();
        if (shared.Count > 0)
            log.LogError($"Manifests loaded by more than one feature (their hot-reload handoffs collide): [{string.Join(", ", shared)}]");

        var carried = features.Select(f => f.Manifest).Concat(loadedElsewhere).ToHashSet();
        var unloaded = FeatureManifests.All.Where(m => !carried.Contains(m)).Select(m => m.Name).ToList();
        if (unloaded.Count > 0)
            log.LogError($"Manifests no feature loads: [{string.Join(", ", unloaded)}]");
        var notChecked = carried.Where(m => !FeatureManifests.All.Contains(m)).Select(m => m.Name).ToList();
        if (notChecked.Count > 0)
            log.LogError($"Manifests missing from FeatureManifests.All, so TargetCheck skips them: [{string.Join(", ", notChecked)}]");
    }
}
