using System;
using System.Collections.Generic;
using System.Reflection;
using EPOOutline;
using HarmonyLib;
using Restory.Data.Outline;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The ghost ledger for the game's hint pulse: every ghost joins the pulse through it. The hint's own
/// ghosts leave as soon as their part stops being ready (picked up, put in, its socket filled or
/// blocked), are filled in rather than only outlined, and pulse red in missing sockets. They share the
/// game's pulse, so it still sets their timing.
/// </summary>
internal static class HintGhosts
{
    /// <summary>How often the hint checks whether its ghosts are still wanted.</summary>
    private const float RecheckSeconds = 0.2f;

    private static GhostLedger<Transform, ElementProjection>? ledger;
    private static TooltipProjectionHighlighter? owner;
    private static float nextRecheck;

    /// <summary>The ledger for this highlighter's pulse (the game has one).</summary>
    public static GhostLedger<Transform, ElementProjection> For(TooltipProjectionHighlighter highlighter)
    {
        if (ledger is not null && owner == highlighter) return ledger;
        Release();
        ledger = new GhostLedger<Transform, ElementProjection>(new GamePulse(highlighter), () => HintPulse.Tidy.Value);
        owner = highlighter;
        return ledger;
    }

    /// <summary>Every frame: keeps the ghosts in step with the pulse, and now and then with the plan.</summary>
    public static void Tick()
    {
        if (ledger is null) return;
        ledger.Tick();
        if (!ledger.Any || Time.unscaledTime < nextRecheck) return;
        nextRecheck = Time.unscaledTime + RecheckSeconds;
        Recheck();
    }

    /// <summary>Drops the hint's ghosts that a fresh plan no longer wants.</summary>
    public static void Recheck()
    {
        if (ledger is not { Any: true }) return;
        var plan = EveryPartHintFeature.PlanNow();
        if (plan is not null) ledger.Reconcile(plan);
    }

    /// <summary>A part went in or out of this socket: its ghosts leave, and the others are checked again.</summary>
    public static void SocketChanged(TooltipProjectionHighlighter highlighter, Transform socket)
    {
        For(highlighter).Drop(socket);
        Recheck();
    }

    public static void PulseEnding(TooltipProjectionHighlighter highlighter)
    {
        if (ledger is not null && owner == highlighter) ledger.PulseEnded();
    }

    /// <summary>
    /// After a hot reload, while a pulse still runs: takes over the ghosts the old copy handed back
    /// plain, so they get the hint's look again and still leave once their part stops being ready.
    /// Needs the workbench session resumed first; nothing on a normal start.
    /// </summary>
    internal static void Resume()
    {
        var service = WorkbenchSession.Service;
        if (service is null || !EveryPartHintFeature.TargetsPresent()) return;
        var highlighter = WorkbenchSession.Highlighter(service);
        if (!highlighter || HintPulse.Running(highlighter!) is null) return;
        var plan = EveryPartHintFeature.PlanNow();
        if (plan is null) return;
        For(highlighter!).Adopt(plan);
        Plugin.Logger.LogInfo("Hint ghosts taken over from the previous plugin copy.");
    }

    /// <summary>On unload: hands every ghost back to the plain pulse and drops the copied presets.</summary>
    internal static void Unload()
    {
        Release();
        GamePulse.DestroyPresets();
    }

    private static void Release()
    {
        try
        {
            ledger?.Unload();
        }
        catch (Exception e)
        {
            HintPulse.LogOnce(e); // the old pulse went with its scene
        }
        ledger = null;
        owner = null;
    }
}

/// <summary>The game's hint pulse behind the ghost ledger.</summary>
internal sealed class GamePulse : IGhostPulse<Transform, ElementProjection>
{
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, List<ElementProjection>> Ghosts =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, List<ElementProjection>>("highlightedProjections");
    private static readonly AccessTools.FieldRef<TooltipProjectionHighlighter, ElementProjectionFactory> Factory =
        AccessTools.FieldRefAccess<TooltipProjectionHighlighter, ElementProjectionFactory>("elementProjectionFactory");

    /// <summary>
    /// Copies of the pulse's outline preset for the hint's ghosts: yellow, red, and a faint copy of
    /// each for waiting parts. They add a fill, so a ghost shows the whole part rather than an outline
    /// that merges with its neighbours' (overlapping fills add up and look darker). Each look needs its
    /// own copy because a preset's colors are shared by every ghost wearing it.
    /// </summary>
    private static readonly OutlineSettingsPreset?[] Presets = new OutlineSettingsPreset?[4];

    private readonly TooltipProjectionHighlighter highlighter;

    /// <summary>The preset each painted ghost wears now.</summary>
    private readonly Dictionary<ElementProjection, OutlineSettingsPreset> wearing = new();

    public GamePulse(TooltipProjectionHighlighter highlighter) => this.highlighter = highlighter;

    /// <summary>
    /// The pulse's outline members, which only the hint's own ghosts need (EveryPartHintFeature checks
    /// them): kept apart so a game update that moves them can't break the game's own ghosts.
    /// </summary>
    private static class Outline
    {
        public static readonly AccessTools.FieldRef<ProjectionHighlighterBase, OutlineSettingsPreset> Preset =
            AccessTools.FieldRefAccess<ProjectionHighlighterBase, OutlineSettingsPreset>("outlinePresetInstance");
        public static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Outlinable.OutlineProperties?> Front =
            AccessTools.FieldRefAccess<ProjectionHighlighterBase, Outlinable.OutlineProperties?>("frontOutlineProperties");
        public static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Outlinable.OutlineProperties?> Back =
            AccessTools.FieldRefAccess<ProjectionHighlighterBase, Outlinable.OutlineProperties?>("backOutlineProperties");

        /// <summary>The flat see-through fill the game's own part ghosts use; null if the build lacks it.</summary>
        public static readonly Shader? FillShader = Shader.Find("Hidden/EPO/Fill/Basic/Color fill");
    }

    public ElementProjection? GhostUnder(Transform spot)
    {
        var ghosts = Ghosts(highlighter);
        for (var i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i] && ghosts[i].transform.parent == spot) return ghosts[i];
        }
        return null;
    }

    public ElementProjection? Create(Transform spot, HintKind kind)
    {
        var look = EveryPartHintFeature.Look(spot, kind);
        if (look is null) return null;
        highlighter.CreateAndHighlightProjection(look, spot);
        var ghost = GhostUnder(spot);
        if (!ghost) return null;
        // A pooled ghost keeps the size another highlighter left it at.
        if (HintPulse.Tidy.Value) ghost!.transform.localScale = Vector3.one;
        // The pool is shared with the game's screw-slot markers, whose outline the hint may have muted.
        ghost!.OutlinableAdapter.Outlinable.enabled = true;
        return ghost;
    }

    public bool Alive(ElementProjection ghost) => ghost && Ghosts(highlighter).Contains(ghost);

    public bool Destroy(ElementProjection ghost)
    {
        if (!Ghosts(highlighter).Remove(ghost)) return false;
        Factory(highlighter).DestroySmallElementProjection(ghost);
        return true;
    }

    public void RiseAgain() => HintPulse.RiseAgain(highlighter);

    /// <summary>The game's marker on a screw slot would draw in no set order with the ghost, so it could hide it.</summary>
    public ElementProjection? MuteMarker(Transform spot)
    {
        var marker = EveryPartHintFeature.Marker(spot);
        if (marker) marker!.OutlinableAdapter.Outlinable.enabled = false;
        return marker;
    }

    // Even a marker the game has put back in its pool: the next one made from it must draw.
    public void Unmute(ElementProjection marker)
    {
        if (marker) marker.OutlinableAdapter.Outlinable.enabled = true;
    }

    /// <summary>
    /// Gives a ghost the pulse's current brightness, in yellow or red, on its outline and fill; a
    /// waiting part gets a faint steady look instead.
    /// </summary>
    public void Paint(ElementProjection ghost, bool missing, bool waiting)
    {
        var front = Outline.Front(highlighter);
        if (front is null) return;
        var preset = Copy(missing, waiting);
        if (!wearing.TryGetValue(ghost, out var worn) || worn != preset)
        {
            ghost.OutlinableAdapter.OverridePreset = preset;
            wearing[ghost] = preset;
        }
        var outline = ghost.OutlinableAdapter.Outlinable;
        if (outline.RenderStyle == RenderStyle.Single)
        {
            Paint(outline.OutlineParameters, front.Color, missing, waiting);
            return;
        }
        Paint(outline.FrontParameters, front.Color, missing, waiting);
        Paint(outline.BackParameters, (Outline.Back(highlighter) ?? front).Color, missing, waiting);
    }

    public void Unpaint(ElementProjection ghost)
    {
        wearing.Remove(ghost);
        if (ghost && highlighter) ghost.OutlinableAdapter.OverridePreset = Outline.Preset(highlighter);
    }

    internal static void DestroyPresets()
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i]) UnityEngine.Object.Destroy(Presets[i]);
            Presets[i] = null;
        }
    }

    private OutlineSettingsPreset Copy(bool missing, bool waiting)
    {
        var i = (missing ? 2 : 0) + (waiting ? 1 : 0);
        if (!Presets[i]) Presets[i] = UnityEngine.Object.Instantiate(Outline.Preset(highlighter));
        return Presets[i]!;
    }

    private static void Paint(Outlinable.OutlineProperties properties, Color pulse, bool missing, bool waiting)
    {
        // Waiting: the pulse's peak color held still and darkened (the outline ignores alpha).
        if (waiting) pulse = new Color(0.45f, 0.45f, 0f, pulse.a);
        var color = missing ? Red(pulse) : pulse;
        properties.Color = color;
        var fill = Outline.FillShader;
        if (!fill) return;
        if (properties.FillPass.Shader != fill) properties.FillPass.Shader = fill;
        // The pulse runs white (blue 1) to yellow (blue 0): the fill thickens as it brightens.
        var alpha = waiting ? 0.08f : Mathf.Lerp(0.15f, 0.35f, 1f - pulse.b);
        properties.FillPass.SetColor("_PublicColor", new Color(color.r, color.g, color.b, alpha));
    }

    // The pulse runs white to yellow; this keeps its brightness and turns it red.
    private static Color Red(Color pulse) => new(pulse.r, pulse.g * 0.2f, pulse.b * 0.2f, pulse.a);
}

/// <summary>Runs the hint ghosts' per-frame check.</summary>
internal sealed class HintGhostWatcher : MonoBehaviour
{
    private static bool errorLogged;

    private void Update()
    {
        try
        {
            HintGhosts.Tick();
        }
        catch (Exception e)
        {
            if (errorLogged) return;
            errorLogged = true;
            Plugin.Logger.LogError($"Every-part hint ghosts failed (further errors suppressed): {e}");
        }
    }
}

/// <summary>The pulse ending: red ghosts turn back to yellow before the game pools them.</summary>
[HarmonyPatch]
internal static class HintGhostsPulseEndPatch
{
    private static bool Prepare() => EveryPartHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipHighlighterComplete)!;

    private static void Prefix(TooltipProjectionHighlighter __instance) => HintGhosts.PulseEnding(__instance);
}
