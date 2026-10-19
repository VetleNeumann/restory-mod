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

/// <summary>
/// The game's unused PS5 button sprites, loaded from its own image bundle. Its glyph lists draw
/// PlayStation pads with the generic A/B art; <see cref="PlayStationArtPatch"/> swaps these in.
/// </summary>
internal static class PlayStationArt
{
    private const float RetrySeconds = 1f;

    private static readonly Dictionary<string, Sprite> Loaded = new();
    private static float nextAttempt;
    private static bool complete;

    /// <summary>The game's PlayStation ControllerId, taken from its dependency map by <see cref="ButtonPromptsPatch"/>.</summary>
    public static ControllerId? SonyId { get; set; }

    public static Sprite? For(int templateElementId)
    {
        var name = PlayStationGlyphs.SpriteFor(templateElementId);
        if (name is null) return null;
        EnsureLoaded();
        return Loaded.TryGetValue(name, out var sprite) && sprite ? sprite : null;
    }

    private static void EnsureLoaded()
    {
        // A sprite goes missing if the game unloads the bundle; look again then.
        if (complete && Loaded.Values.All(s => s)) return;
        if (Time.unscaledTime < nextAttempt) return;
        nextAttempt = Time.unscaledTime + RetrySeconds;

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
        if (paths.Count == 0) return;

        Loaded.Clear();
        var missing = new List<string>();
        foreach (var name in PlayStationGlyphs.AllSprites)
        {
            var sprite = paths.TryGetValue(name, out var at) ? at.Bundle.LoadAsset<Sprite>(at.Path) : null;
            if (sprite) Loaded[name] = sprite!;
            else missing.Add(name);
        }
        complete = true;
        // Prompts drawn while the sprites weren't loaded yet still show the generic art.
        if (Loaded.Count > 0 && ButtonPromptsPoller.Running) ButtonPromptsPoller.RefreshPrompts();
        Plugin.Logger.LogInfo(missing.Count == 0
            ? $"PlayStation prompts: {Loaded.Count} sprites loaded."
            : $"PlayStation prompts: {Loaded.Count} sprites loaded; missing [{string.Join(", ", missing)}] keep the game's art.");
    }
}

/// <summary>After a prompt image draws, swaps the PS5 sprite in when the prompt is for a PlayStation pad.</summary>
[HarmonyPatch]
internal static class PlayStationArtPatch
{
    private static bool Prepare() => ButtonPromptsFeature.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.PromptImageUpdateView,
        PatchTargets.PromptImageTarget,
        PatchTargets.PromptViewControllerId,
        PatchTargets.PromptViewElementId);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.PromptImageUpdateView)!;

    private static void Postfix(Image ___targetImage, ControllerId ___controllerId, int ___elementId)
    {
        try
        {
            if (!___targetImage || !___controllerId || !PlayStationArt.SonyId) return;
            if (___controllerId.ID != PlayStationArt.SonyId!.ID || !___targetImage.overrideSprite) return;

            var sprite = PlayStationArt.For(___elementId);
            if (sprite) ___targetImage.overrideSprite = sprite;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"PlayStation prompts: {e}");
        }
    }
}
