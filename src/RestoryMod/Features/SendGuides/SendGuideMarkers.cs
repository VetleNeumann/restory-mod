using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using IAmFuture.Data.Controllers;
using Restory.Gameplay.Tooltips;
using Restory.UI.Presenters.Controllers;
using RestoryMod.Core;
using RestoryMod.Features.ButtonPrompts;
using Rewired;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.SendGuides;

/// <summary>
/// Draws the guides the patches ask for this frame: turns the destination's circle on if the game left
/// it off, and shows the D-pad direction under it. A circle the game turned on stays the game's: only
/// the ones turned on here are turned off again when their guide goes.
/// </summary>
internal static class SendGuideMarkers
{
    private sealed class Marker
    {
        public GameObject Badge = null!;
        public Image Image = null!;
        public RectTransform Circle = null!;
        public bool TurnedOn;
    }

    /// <summary>Badge size and gap under the circle, as fractions of the circle's height.</summary>
    private const float BadgeScale = 0.6f;
    private const float Gap = 0.05f;

    private static readonly Dictionary<TooltipIndicator, Marker> markers = new();
    private static readonly Dictionary<TooltipIndicator, DpadDirection> wanted = new();
    private static int wantedFrame = -1;
    private static bool shownLastFrame;

    private static AccessTools.FieldRef<TooltipIndicator, Image>? imageOf;

    public const string BadgeName = "RestoryMod_SendGuide_";

    /// <summary>Asks for a guide on this circle for the current frame.</summary>
    public static void Want(TooltipIndicator? indicator, DpadDirection direction)
    {
        if (!indicator) return;
        if (wantedFrame != Time.frameCount)
        {
            wanted.Clear();
            wantedFrame = Time.frameCount;
        }
        wanted[indicator!] = direction;
    }

    /// <summary>The game shows this circle by itself, not because a guide turned it on.</summary>
    public static bool CircledByGame(TooltipIndicator? indicator) =>
        indicator && indicator!.gameObject.activeSelf &&
        !(markers.TryGetValue(indicator, out var marker) && marker.TurnedOn);

    /// <summary>Runs after every Update, once the drag states have asked for this frame's guides.</summary>
    public static void Apply()
    {
        var any = wantedFrame == Time.frameCount && wanted.Count > 0;
        // A new hold may come with a different pad or prompt setting; the art is looked up once per hold.
        if (any && !shownLastFrame)
        {
            GuideGlyphs.Refresh();
            // Circles of a scene that has since unloaded.
            foreach (var gone in markers.Keys.Where(indicator => !indicator).ToList()) markers.Remove(gone);
        }
        shownLastFrame = any;

        foreach (var (indicator, marker) in markers)
        {
            if (any && wanted.ContainsKey(indicator)) continue;
            Hide(indicator, marker);
        }
        if (!any) return;

        foreach (var (indicator, direction) in wanted)
        {
            if (!indicator) continue;
            if (!markers.TryGetValue(indicator, out var marker) || !marker.Badge)
            {
                if (Create(indicator) is not { } created) continue;
                markers[indicator] = marker = created;
            }
            if (!indicator.gameObject.activeSelf)
            {
                indicator.gameObject.SetActive(true);
                marker.TurnedOn = true;
            }
            var sprite = GuideGlyphs.For(direction);
            marker.Image.sprite = sprite;
            // Named for its direction, so the end-to-end checks can read which guides show.
            var name = BadgeName + direction;
            if (marker.Badge.name != name) marker.Badge.name = name;
            marker.Badge.SetActive(sprite);
        }
    }

    private static void Hide(TooltipIndicator indicator, Marker marker)
    {
        if (marker.Badge) marker.Badge.SetActive(false);
        if (!marker.TurnedOn) return;
        marker.TurnedOn = false;
        if (indicator) indicator.gameObject.SetActive(false);
    }

    /// <summary>
    /// The badge hangs under the circle's own image, so it draws on the circle's canvas and hides with
    /// it when the game hides the circles for a dialogue. Its place and turn are set in <see cref="Align"/>.
    /// </summary>
    private static Marker? Create(TooltipIndicator indicator)
    {
        imageOf ??= AccessTools.FieldRefAccess<TooltipIndicator, Image>(PatchTargets.IndicatorImage.MemberName!);
        var circle = imageOf(indicator);
        if (!circle) return null;

        var badge = new GameObject(BadgeName, typeof(RectTransform), typeof(Image));
        badge.layer = circle.gameObject.layer;
        var rect = (RectTransform)badge.transform;
        rect.SetParent(circle.rectTransform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = circle.rectTransform.rect.size * BadgeScale;
        var image = badge.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.material = OnTop();
        badge.SetActive(false);
        return new Marker { Badge = badge, Image = image, Circle = circle.rectTransform };
    }

    private static Material? onTop;

    /// <summary>
    /// The badge hangs below the circle, where the equipment's own body can stand closer to the camera
    /// (the washing bin's front does); world UI is depth tested, so it is drawn over everything instead.
    /// </summary>
    private static Material OnTop()
    {
        if (onTop) return onTop!;
        onTop = new Material(Graphic.defaultGraphicMaterial) { name = "RestoryMod_SendGuide" };
        onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        return onTop;
    }

    /// <summary>
    /// Just before the canvases draw, after every LateUpdate (the circles turn to the camera in theirs):
    /// each badge turns upright to the screen, so its arrow points the way the D-pad press goes, and
    /// sits on screen under its circle wherever the camera looks from.
    /// </summary>
    public static void Align()
    {
        try
        {
            if (markers.Count == 0) return;
            var camera = Camera.main;
            if (!camera) return;
            var view = camera.transform;
            foreach (var marker in markers.Values)
            {
                if (!marker.Badge || !marker.Badge.activeInHierarchy || !marker.Circle) continue;
                var height = marker.Circle.rect.height * marker.Circle.lossyScale.y;
                var badge = marker.Badge.transform;
                badge.rotation = view.rotation;
                badge.position = marker.Circle.position - view.up * height * (0.5f + Gap + BadgeScale * 0.5f);
            }
        }
        catch (Exception e)
        {
            SendGuidesFeature.LogOnce(e);
        }
    }

    /// <summary>Hot reload: give back the circles turned on here and remove every badge.</summary>
    public static void Unload()
    {
        Canvas.willRenderCanvases -= Align;
        foreach (var (indicator, marker) in markers)
        {
            Hide(indicator, marker);
            if (marker.Badge) Object.Destroy(marker.Badge);
        }
        markers.Clear();
        wanted.Clear();
        if (onTop) Object.Destroy(onTop);
        onTop = null;
    }
}

/// <summary>Applies the guides each frame after the game's own updates.</summary>
internal sealed class SendGuideMarkersPoller : MonoBehaviour
{
    private void LateUpdate()
    {
        try
        {
            SendGuideMarkers.Apply();
        }
        catch (Exception e)
        {
            SendGuidesFeature.LogOnce(e);
        }
    }
}

/// <summary>
/// The D-pad art for the active pad. The game ships one arrow per D-pad direction for Xbox and PS5 pads
/// but never draws them (its templates leave those elements empty), so they are loaded by name from its
/// bundles. A prompt's own controller lookup decides between them, so the Button prompts setting
/// applies; any pad drawn with other art gets the Xbox arrows.
/// </summary>
internal static class GuideGlyphs
{
    private static readonly Dictionary<string, Sprite> loaded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<DpadDirection, Sprite?> sprites = new();
    private static GUI_InputActionElement? prompt;
    private static MethodInfo? tryGetControllerId;

    private static string SpriteName(DpadDirection direction, bool playStation) =>
        (playStation ? "PS5_DPad_" : "Xbox_DPad_") + direction switch
        {
            DpadDirection.Up => "Top",
            DpadDirection.Down => "Bottom",
            _ => direction.ToString(),
        };

    public static Sprite? For(DpadDirection direction) =>
        sprites.TryGetValue(direction, out var sprite) && sprite ? sprite : null;

    public static void Refresh()
    {
        sprites.Clear();
        if (ReInput.controllers.GetLastActiveController() is not { type: ControllerType.Joystick } joystick) return;

        var playStation = false;
        if (!prompt) prompt = Object.FindAnyObjectByType<GUI_InputActionElement>(FindObjectsInactive.Include);
        if (prompt)
        {
            tryGetControllerId ??= GameTargets.Method(PatchTargets.PromptGetControllerId);
            var args = new object?[] { joystick, null };
            playStation = tryGetControllerId!.Invoke(prompt, args) is true && args[1] is ControllerId id &&
                PlayStationArt.PlayStationId && id == PlayStationArt.PlayStationId;
        }

        // One scan of the bundles at most, and none while every arrow is still loaded.
        var scanned = false;
        foreach (DpadDirection direction in Enum.GetValues(typeof(DpadDirection)))
        {
            var name = SpriteName(direction, playStation);
            if (!scanned && (!loaded.TryGetValue(name, out var cached) || !cached))
            {
                LoadAll();
                scanned = true;
            }
            sprites[direction] = loaded.TryGetValue(name, out var sprite) && sprite ? sprite : null;
            if (!sprites[direction]) WarnOnce($"the game's {name} sprite was not found");
        }
    }

    /// <summary>Loaded bundles carry hashed names, so the sprites are found by asset path.</summary>
    private static void LoadAll()
    {
        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (!bundle || bundle.isStreamedSceneAssetBundle) continue;
            foreach (var path in bundle.GetAllAssetNames())
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Xbox_DPad_", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("PS5_DPad_", StringComparison.OrdinalIgnoreCase)) continue;
                var sprite = bundle.LoadAsset<Sprite>(path);
                if (sprite) loaded[name] = sprite;
            }
        }
    }

    private static readonly HashSet<string> warned = new();

    private static void WarnOnce(string problem)
    {
        if (warned.Add(problem)) Plugin.Logger.LogWarning($"Send guides drawn without the D-pad: {problem}.");
    }
}
