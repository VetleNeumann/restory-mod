using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using RestoryMod.Features.DropHint;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.Fade;

/// <summary>
/// Fades the device on the bench: every part of it is drawn as one flat, see-through gray, and its
/// colliders are switched off, so the cursor falls through to whatever lies behind it (a loose part
/// dropped out of reach) and a part dropped on it lands on the bench. L1 fades it and brings it back
/// (see <see cref="ShoulderButtons.ShoulderButtonsPoller"/>, where screens that use L1 come first), as
/// does H on a keyboard. Leaving the workbench or changing device brings it back.
/// </summary>
internal static class DeviceFadeFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Workbench", "L1FadesDevice", true,
            "At the workbench, L1 (H on a keyboard) fades the device to a gray see-through shape that the cursor " +
            "passes through, so parts behind it can be picked up. Press again to bring it back. Screens that use " +
            "L1 (the inventory, the Tool window) take it first.");
    }

    public static bool TargetsPresent() => WorkbenchSession.StatePresent && GameTargets.AllPresent(FeatureManifests.DeviceFade);

    private static bool? targetsPresent;

    /// <summary><see cref="TargetsPresent"/>, checked once: it is read on every L1 press.</summary>
    public static bool TargetsPresentCached => targetsPresent ??= TargetsPresent();

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Device fade failed (further errors suppressed): {e}");
    }
}

/// <summary>The faded device, if any, and what fading it changed, so bringing it back undoes exactly that.</summary>
internal static class DeviceFade
{
    private static readonly Color Gray = new(0.62f, 0.62f, 0.62f, 0.22f);

    /// <summary>How often a faded device is checked for parts that changed look or came into it.</summary>
    private const float RescanSeconds = 0.25f;

    private static Device? faded;
    private static Material? fadeMaterial;
    private static readonly Dictionary<Renderer, Material[]> Looks = new();
    private static readonly HashSet<Collider> Switched = new();
    private static readonly List<Renderer> RendererBuffer = new();
    private static readonly List<Collider> ColliderBuffer = new();
    private static float nextRescan;

    public static bool IsFaded => faded;

    /// <summary>
    /// The device L1 would fade now: at the workbench, taking apart or putting back (a part may be held),
    /// with a device on the bench. Null otherwise.
    /// </summary>
    public static Device? Fadeable()
    {
        if (!DeviceFadeFeature.Enabled.Value) return null;
        var service = WorkbenchSession.Service;
        if (service is null || !service.IsActive) return null;
        if (WorkbenchSession.ActiveState(service) is not (DetectionDisassembleState or DraggingDisassembleState)) return null;
        var device = WorkbenchSession.Device(service);
        return device ? device : null;
    }

    public static void Toggle()
    {
        if (faded)
        {
            Restore();
            return;
        }
        var device = Fadeable();
        if (!device) return;
        faded = device;
        nextRescan = 0f;
        Apply();
    }

    /// <summary>Each frame: keeps the device faded, or brings it back once the session or device changes.</summary>
    public static void Update()
    {
        if (!faded)
        {
            if (faded is not null) Restore();
            return;
        }
        var service = WorkbenchSession.Service;
        if (service is null || !service.IsActive || WorkbenchSession.Device(service) != faded)
        {
            Restore();
            return;
        }
        Apply();
    }

    /// <summary>
    /// Switches off every collider the game switched on since (it does as parts free up), each frame so
    /// none catches the cursor; recolours parts every <see cref="RescanSeconds"/>.
    /// </summary>
    private static void Apply()
    {
        faded!.GetComponentsInChildren(true, ColliderBuffer);
        foreach (var collider in ColliderBuffer)
        {
            if (!collider.enabled) continue;
            collider.enabled = false;
            Switched.Add(collider);
        }

        if (Time.unscaledTime < nextRescan) return;
        nextRescan = Time.unscaledTime + RescanSeconds;
        var material = FadeMaterial();
        if (!material) return;
        faded.GetComponentsInChildren(true, RendererBuffer);
        foreach (var renderer in RendererBuffer)
        {
            if (renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
            // Part ghosts are already see-through and belong to the hint.
            if (renderer.GetComponentInParent<ElementProjection>(true)) continue;
            var current = renderer.sharedMaterials;
            if (current.Length > 0 && Array.TrueForAll(current, m => m == material)) continue;
            Looks[renderer] = current;
            var gray = new Material[current.Length];
            for (var i = 0; i < gray.Length; i++) gray[i] = material!;
            renderer.sharedMaterials = gray;
        }
    }

    public static void Restore()
    {
        foreach (var pair in Looks)
        {
            if (pair.Key) pair.Key.sharedMaterials = pair.Value;
        }
        foreach (var collider in Switched)
        {
            if (collider) collider.enabled = true;
        }
        Looks.Clear();
        Switched.Clear();
        faded = null;
    }

    /// <summary>On unload: brings the device back and drops the material, so a hot reload starts clean.</summary>
    internal static void Unload()
    {
        Restore();
        if (fadeMaterial) Object.Destroy(fadeMaterial);
        fadeMaterial = null;
    }

    /// <summary>URP's unlit shader set up as transparent: a flat colour with no lighting, not writing depth.</summary>
    private static Material? FadeMaterial()
    {
        if (fadeMaterial) return fadeMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
        {
            Plugin.Logger.LogWarning("Device fade: URP unlit shader not found; only the cursor falls through.");
            return null;
        }
        var material = new Material(shader) { name = "RestoryMod_DeviceFade", renderQueue = (int)RenderQueue.Transparent };
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetColor("_BaseColor", Gray);
        fadeMaterial = material;
        return material;
    }
}

/// <summary>Keeps the faded device faded, and takes H on a keyboard.</summary>
internal sealed class DeviceFadePoller : MonoBehaviour
{
    private void Update()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.H) && (DeviceFade.IsFaded || DeviceFade.Fadeable())) DeviceFade.Toggle();
            DeviceFade.Update();
        }
        catch (Exception e)
        {
            DeviceFadeFeature.LogOnce(e);
        }
    }
}
