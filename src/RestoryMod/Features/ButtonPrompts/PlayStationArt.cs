using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using IAmFuture.Data.Controllers;
using RestoryMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryMod.Features.ButtonPrompts;

internal sealed class PlayStationArtFeature : IFeature
{
    public FeatureManifest Manifest => FeatureManifests.PlayStationArt;

    public void Load(FeatureContext ctx) => ctx.Patch(typeof(PlayStationArtPatch));
}

/// <summary>
/// The game's unused PS5 button sprites, loaded from its own image bundle. Its glyph lists draw
/// PlayStation pads with the generic A/B art; <see cref="PlayStationArtPatch"/> swaps these in.
/// <see cref="ButtonPromptsPoller"/> loads them once a PlayStation pad shows up, so drawing a prompt
/// only reads the cache.
/// </summary>
internal static class PlayStationArt
{
    private static readonly Dictionary<string, Sprite> Loaded = new();
    private static bool loaded;

    /// <summary>The game's ControllerId for PlayStation pads; prompts drawn for it get the PS5 sprites.</summary>
    public static ControllerId? PlayStationId { get; private set; }

    /// <summary>
    /// Loads the sprites and the PlayStation ControllerId if not loaded yet. True when this call loaded
    /// them, so prompts already drawn with the generic art need redrawing.
    /// </summary>
    public static bool Load()
    {
        if (loaded) return false;

        if (!PlayStationId)
        {
            var map = Resources.FindObjectsOfTypeAll<RewiredControllerIdsDependencyMap>().FirstOrDefault();
            if (!map || !map!.TryGetControllerId(PadKinds.DualSense, out var id)) return false;
            PlayStationId = id;
        }

        // Loaded bundles carry hashed names, so find the sprites by asset path ("assets/.../ps5cross.png").
        var wanted = new HashSet<string>(PlayStationGlyphs.AllSprites, StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<string, (AssetBundle Bundle, string Path)>(StringComparer.OrdinalIgnoreCase);
        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (!bundle || bundle.isStreamedSceneAssetBundle) continue;
            foreach (var path in bundle.GetAllAssetNames())
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (wanted.Contains(name)) paths[name] = (bundle, path);
            }
        }
        if (paths.Count == 0) return false;

        Loaded.Clear();
        var missing = new List<string>();
        foreach (var name in PlayStationGlyphs.AllSprites)
        {
            var sprite = paths.TryGetValue(name, out var at) ? at.Bundle.LoadAsset<Sprite>(at.Path) : null;
            if (sprite) Loaded[name] = sprite!;
            else missing.Add(name);
        }
        loaded = true;
        Plugin.Logger.LogInfo(missing.Count == 0
            ? $"PlayStation prompts: {Loaded.Count} sprites loaded."
            : $"PlayStation prompts: {Loaded.Count} sprites loaded; missing [{string.Join(", ", missing)}] keep the game's art.");
        return Loaded.Count > 0;
    }

    /// <summary>The PS5 sprite for a template element, or null to keep the game's art.</summary>
    public static Sprite? For(int templateElementId)
    {
        var name = PlayStationGlyphs.SpriteFor(templateElementId);
        if (name is null || !Loaded.TryGetValue(name, out var sprite)) return null;
        if (sprite) return sprite;

        // The game unloaded the bundle; the poller loads the sprites again.
        loaded = false;
        return null;
    }
}

/// <summary>After a prompt image draws, swaps the PS5 sprite in when the prompt is for a PlayStation pad.</summary>
[HarmonyPatch]
internal static class PlayStationArtPatch
{
    private static bool Prepare() => ButtonPromptsFeature.TargetsPresent() &&
        GameTargets.AllPresent(FeatureManifests.PlayStationArt);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.PromptImageUpdateView)!;

    private static void Postfix(Image ___targetImage, ControllerId ___controllerId, int ___elementId)
    {
        try
        {
            if (!___targetImage || !___targetImage.overrideSprite || !___controllerId) return;
            if (!PlayStationArt.PlayStationId || ___controllerId != PlayStationArt.PlayStationId) return;

            var sprite = PlayStationArt.For(___elementId);
            if (sprite) ___targetImage.overrideSprite = sprite;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"PlayStation prompts: {e}");
        }
    }
}
