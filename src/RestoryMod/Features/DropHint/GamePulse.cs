using System.Collections.Generic;
using EPOOutline;
using HarmonyLib;
using Restory.Constants;
using Restory.Data.Outline;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>The game's hint pulse behind the ghost ledger.</summary>
internal sealed class GamePulse : IGhostPulse<Transform, ElementProjection>
{
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, List<ElementProjection>> Ghosts =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, List<ElementProjection>>("highlightedProjections");
    private static readonly AccessTools.FieldRef<TooltipProjectionHighlighter, ElementProjectionFactory> Factory =
        AccessTools.FieldRefAccess<TooltipProjectionHighlighter, ElementProjectionFactory>("elementProjectionFactory");

    /// <summary>
    /// Copies of the pulse's outline preset, one per hint look. They add a fill, so
    /// a ghost shows the whole part (overlapping fills add up and look darker), and draw front and
    /// back apart, so the part of a ghost behind something takes the hidden style. Each look needs its
    /// own copy because a preset's colors are shared by every ghost wearing it.
    /// </summary>
    private static readonly OutlineSettingsPreset?[] Presets =
        new OutlineSettingsPreset?[System.Enum.GetValues(typeof(Look)).Length];

    private readonly TooltipProjectionHighlighter highlighter;

    /// <summary>The preset each painted ghost wears now.</summary>
    private readonly Dictionary<ElementProjection, OutlineSettingsPreset> wearing = new();

    /// <summary>The preset each painted ghost had before, to give back when it is unpainted.</summary>
    private readonly Dictionary<ElementProjection, OutlineSettingsPreset> own = new();

    /// <summary>The outline layer each claimed ghost had, to give back when it is released.</summary>
    private readonly Dictionary<ElementProjection, int> claimed = new();

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

        /// <summary>The flat see-through fill the game's own part ghosts use; null if the build lacks it.</summary>
        public static readonly Shader? FillShader = Shader.Find("Hidden/EPO/Fill/Basic/Color fill");

        /// <summary>EPO's drifting dot grid, for the missing look; null if the build lacks it (then a flat fill).</summary>
        public static readonly Shader? DotsShader = Shader.Find("Hidden/EPO/Fill/Basic/Dots");

        public static readonly AccessTools.FieldRef<OutlineSettingsPreset, RenderStyle> Style =
            AccessTools.FieldRefAccess<OutlineSettingsPreset, RenderStyle>("renderStyle");
        public static readonly AccessTools.FieldRef<OutlineSettingsPreset, Outlinable.OutlineProperties> Common =
            AccessTools.FieldRefAccess<OutlineSettingsPreset, Outlinable.OutlineProperties>("commonSettings");
        public static readonly AccessTools.FieldRef<OutlineSettingsPreset, Outlinable.OutlineProperties> PresetFront =
            AccessTools.FieldRefAccess<OutlineSettingsPreset, Outlinable.OutlineProperties>("frontSettings");
        public static readonly AccessTools.FieldRef<OutlineSettingsPreset, Outlinable.OutlineProperties> PresetBack =
            AccessTools.FieldRefAccess<OutlineSettingsPreset, Outlinable.OutlineProperties>("backSettings");
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
    /// Gives a ghost its look at the pulse's current brightness, on its outline and fill: in view on
    /// the front, in the hidden style on the back (where something stands in front of it).
    /// </summary>
    public void Paint(ElementProjection ghost, Look look)
    {
        var front = Outline.Front(highlighter);
        if (front is null) return;
        var preset = Copy(look);
        if (!wearing.TryGetValue(ghost, out var worn) || worn != preset)
        {
            if (!own.ContainsKey(ghost)) own[ghost] = ghost.OutlinableAdapter.OverridePreset;
            ghost.OutlinableAdapter.OverridePreset = preset;
            wearing[ghost] = preset;
        }
        var outline = ghost.OutlinableAdapter.Outlinable;
        var pulse = ToRgba(front.Color);
        if (look == Look.Screw)
        {
            // A screw slot's ghost sits down in its hole, so EPO's per-pixel test would call it hidden
            // whenever the rim is in the way; a screw is too small to be half hidden, so one ray decides.
            var screw = Looks.Paint(look, pulse, hidden: Occluded(ghost.transform));
            Paint(outline.FrontParameters, screw, front);
            Paint(outline.BackParameters, screw, front);
            return;
        }
        var condition = look.IsCondition() ? ConditionColor(look) : null;
        Paint(outline.FrontParameters, Looks.Paint(look, pulse, hidden: false, condition), front);
        Paint(outline.BackParameters, Looks.Paint(look, pulse, hidden: true, condition), front);
    }

    private static readonly AccessTools.FieldRef<ElementView, ElementOutlineSettings> ViewSettings =
        AccessTools.FieldRefAccess<ElementView, ElementOutlineSettings>(PatchTargets.ElementViewOutlineSettings.MemberName);

    private static ElementOutlineSettings? settings;

    /// <summary>
    /// The colour the game's hover outline gives this condition, so the hint and the hover agree; null
    /// (the look's own stand-in) until some part's view is found.
    /// </summary>
    private static Rgba? ConditionColor(Look look)
    {
        if (!settings)
        {
            var view = UnityEngine.Object.FindAnyObjectByType<ElementView>();
            if (!view) return null;
            settings = ViewSettings(view!);
            if (!settings) return null;
        }
        if (look.Condition() is not { } condition) return null;
        var preset = ConditionLook.Preset(settings!, condition);
        if (!preset) return null;
        var drawn = Outline.Style(preset) == RenderStyle.FrontBack ? Outline.PresetFront(preset) : Outline.Common(preset);
        return drawn is null ? null : ToRgba(drawn.Color);
    }

    /// <summary>How far before a screw a hit still counts as its own hole, in metres.</summary>
    private const float OwnHoleDepth = 0.015f;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    /// <summary>
    /// The device or one of its parts stands between the camera and a screw's ghost, other than its own
    /// hole. Ghosts, a held part and the bench don't count, nor the screw in the ghost's socket.
    /// </summary>
    private static bool Occluded(Transform ghost)
    {
        var camera = Camera.main;
        if (!camera) return false;
        var from = camera.transform.position;
        var toPoint = ghost.position - from;
        var reach = toPoint.magnitude - OwnHoleDepth;
        if (reach <= 0f) return false;
        var mask = ProjectConstants.Layers.DeviceMask | ProjectConstants.Layers.ElementsMask;
        var count = Physics.RaycastNonAlloc(from, toPoint, Hits, reach, mask, QueryTriggerInteraction.Ignore);
        var socket = ghost.parent;
        for (var i = 0; i < count; i++)
        {
            if (!socket || !Hits[i].transform.IsChildOf(socket)) return true;
        }
        return false;
    }

    public void Take(ElementProjection ghost, Look look)
    {
        Claim(ghost);
        Paint(ghost, look);
    }

    // Even a ghost the game has put back in its pool: the next one made from it is the game's.
    public void LetGo(ElementProjection ghost)
    {
        Unpaint(ghost);
        Release(ghost);
    }

    private void Unpaint(ElementProjection ghost)
    {
        wearing.Remove(ghost);
        if (!own.TryGetValue(ghost, out var preset)) return;
        own.Remove(ghost);
        if (ghost) ghost.OutlinableAdapter.OverridePreset = preset;
    }

    private void Claim(ElementProjection ghost)
    {
        if (!ghost || claimed.ContainsKey(ghost) || !HintOutliner.Ensure()) return;
        var outline = ghost.OutlinableAdapter.Outlinable;
        claimed[ghost] = outline.OutlineLayer;
        outline.OutlineLayer = HintOutliner.Layer;
    }

    private void Release(ElementProjection ghost)
    {
        if (!claimed.TryGetValue(ghost, out var layer)) return;
        claimed.Remove(ghost);
        if (ghost) ghost.OutlinableAdapter.Outlinable.OutlineLayer = layer;
    }

    internal static void DestroyPresets()
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i]) UnityEngine.Object.Destroy(Presets[i]);
            Presets[i] = null;
        }
    }

    private OutlineSettingsPreset Copy(Look look)
    {
        var i = (int)look;
        if (!Presets[i]) Presets[i] = FrontBack(Outline.Preset(highlighter));
        return Presets[i]!;
    }

    /// <summary>A copy of the preset that draws front and back apart, each with its own settings.</summary>
    private static OutlineSettingsPreset FrontBack(OutlineSettingsPreset preset)
    {
        var copy = UnityEngine.Object.Instantiate(preset);
        if (Outline.Style(copy) == RenderStyle.FrontBack) return copy;
        var spare = UnityEngine.Object.Instantiate(preset);
        Outline.PresetFront(copy) = Outline.Common(copy);
        Outline.PresetBack(copy) = Outline.Common(spare);
        Outline.Style(copy) = RenderStyle.FrontBack;
        UnityEngine.Object.Destroy(spare);
        return copy;
    }

    /// <param name="pulse">The pulse's own outline now: its width, and its fill for the screw look.</param>
    private static void Paint(Outlinable.OutlineProperties properties, LookPaint paint, Outlinable.OutlineProperties pulse)
    {
        // A front/back preset of the game's may leave its back half off.
        properties.Enabled = true;
        properties.Color = ToColor(paint.Outline);
        properties.DilateShift = pulse.DilateShift * paint.Width;
        if (paint.Pattern == FillPattern.Pulse)
        {
            // The pulse's fill as the game animates it (a fresnel glow on screws).
            var own = pulse.FillPass;
            if (properties.FillPass.Shader != own.Shader) properties.FillPass.Shader = own.Shader;
            if (own.HasProperty("_PublicColor")) properties.FillPass.SetColor("_PublicColor", own.GetColor("_PublicColor"));
            return;
        }
        var dotted = paint.Pattern == FillPattern.Dots && Outline.DotsShader;
        var fill = dotted ? Outline.DotsShader : Outline.FillShader;
        if (!fill) return;
        var pass = properties.FillPass;
        if (pass.Shader != fill)
        {
            pass.Shader = fill;
            // Empty gaps between the dots, drifting slowly so the gap reads as alive but calm.
            if (dotted)
            {
                pass.SetFloat("_PublicSecondaryAlpha", 0f);
                pass.SetFloat("_PublicHorizontalSpeed", DotSpeed);
                pass.SetFloat("_PublicVerticalSpeed", DotSpeed);
            }
        }
        pass.SetColor("_PublicColor", ToColor(paint.Fill));
    }

    // The dots' drift; the shader's own default (1) runs busily.
    private const float DotSpeed = 0.25f;

    private static Rgba ToRgba(Color c) => new(c.r, c.g, c.b, c.a);

    private static Color ToColor(Rgba c) => new(c.R, c.G, c.B, c.A);
}
