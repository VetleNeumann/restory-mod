using System.Collections.Generic;
using EPOOutline;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The hint's own outline renderer, beside the game's on the same camera. The game's draws every
/// outline into one mask, so overlapping ghosts merge into a single shape; this one draws the hint's
/// ghosts one by one (EPO's per-object strategy) on an outline layer of their own, which the game's
/// renderer leaves out. Made on first use, as the camera can change with the scene.
/// </summary>
internal static class HintOutliner
{
    /// <summary>The outline layer the hint's ghosts move to; the game uses 0 and 1.</summary>
    public const int Layer = 42;

    private const long LayerBit = 1L << Layer;

    private static Outliner? ours;
    private static Outliner? game;

    /// <summary>Every renderer of the game's, kept off the hint's layer so none draws its ghosts merged.</summary>
    private static readonly List<Outliner> games = new();

    /// <summary>Makes sure the hint's renderer stands beside the game's; false when the game has none.</summary>
    public static bool Ensure()
    {
        if (ours && game && ours!.gameObject == game!.gameObject) return true;
        Remove();
        foreach (var outliner in Object.FindObjectsByType<Outliner>(FindObjectsSortMode.None))
        {
            // One a copy of the mod left behind draws only the hint's layer.
            if (outliner.OutlineLayerMask == LayerBit)
            {
                Object.DestroyImmediate(outliner);
                continue;
            }
            games.Add(outliner);
            if (!game || outliner.gameObject == MainCamera()) game = outliner;
        }
        if (!game) return false;

        foreach (var outliner in games) outliner.OutlineLayerMask &= ~LayerBit;
        ours = game.gameObject.AddComponent<Outliner>();
        ours.RenderStage = game.RenderStage;
        ours.RenderingMode = game.RenderingMode;
        ours.PrimaryBufferSizeMode = game.PrimaryBufferSizeMode;
        ours.PrimaryRendererScale = game.PrimaryRendererScale;
        ours.PrimarySizeReference = game.PrimarySizeReference;
        ours.BlurShift = game.BlurShift;
        ours.DilateShift = game.DilateShift;
        ours.DilateIterations = game.DilateIterations;
        ours.DilateQuality = game.DilateQuality;
        ours.BlurIterations = game.BlurIterations;
        ours.BlurType = game.BlurType;
        ours.RenderingStrategy = OutlineRenderingStrategy.PerObject;
        ours.OutlineLayerMask = LayerBit;
        return true;
    }

    private static GameObject? MainCamera()
    {
        var main = Camera.main;
        return main ? main.gameObject : null;
    }

    /// <summary>On unload: the hint's renderer goes and the game's draws every layer again.</summary>
    public static void Unload() => Remove();

    private static void Remove()
    {
        // At once, so a reloaded copy looking for the game's renderer can't find this one.
        if (ours) Object.DestroyImmediate(ours);
        ours = null;
        foreach (var outliner in games)
        {
            if (outliner) outliner.OutlineLayerMask |= LayerBit;
        }
        games.Clear();
        game = null;
    }
}
