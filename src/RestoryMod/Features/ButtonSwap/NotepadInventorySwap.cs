using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using RestoryMod.Core;
using RestoryMod.Features.ButtonPrompts;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.ButtonSwap;

/// <summary>
/// Swaps the gamepad buttons of the Notepad and the inventory: Triangle / Y opens the Notepad and
/// Square / X the inventory. The swap edits the pad's Rewired maps in memory, so everything that reads
/// the two actions follows (opening, closing, the button prompts) and nothing is saved: switched off
/// or unloaded, the game's own binding is back. The keyboard keeps J and Tab.
/// </summary>
internal sealed class NotepadInventorySwapFeature : IFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.NotepadInventorySwap;

    public void Load(FeatureContext ctx)
    {
        Enabled = ctx.Config.Bind("Controller", "SwapNotepadInventoryButtons", true,
            "Triangle / Y opens the Notepad and Square / X the inventory, the other way round from the game.");
        ctx.AddPoller<NotepadInventorySwapPoller>();
        ctx.OnUnload(NotepadInventorySwapPoller.Unload);
    }
}

/// <summary>
/// Keeps the swap applied to every joystick map of the player, including maps Rewired loads again
/// (a pad reconnected), and takes it out when switched off.
/// </summary>
internal sealed class NotepadInventorySwapPoller : MonoBehaviour
{
    private const float IntervalSeconds = 1f;
    private const int NotepadAction = RewiredConsts.Action.Copybook.OpenCopybook;
    private const int InventoryAction = RewiredConsts.Action.Inventory.Inventory0;

    /// <summary>Maps we swapped, with the swap applied to each, so it can be undone.</summary>
    private static readonly Dictionary<ControllerMap, Core.ButtonSwap> Applied = new();

    /// <summary>Per joystick, the swap read from its maps before we touched them.</summary>
    private static readonly Dictionary<int, Core.ButtonSwap> Swaps = new();

    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + IntervalSeconds;
        try
        {
            if (!ReInput.isReady) return;
            var changed = NotepadInventorySwapFeature.Enabled.Value ? Apply() : Revert();
            if (!changed) return;
            Plugin.Logger.LogInfo(Applied.Count > 0
                ? $"Notepad/inventory buttons swapped ({Applied.Count} gamepad maps)."
                : "Notepad/inventory buttons back to the game's.");
            ButtonPromptsFeature.RefreshPrompts();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Notepad/inventory button swap: {e}");
            enabled = false;
        }
    }

    private static bool Apply()
    {
        var player = ReInput.players.GetPlayer(0);
        if (player is null) return false;
        var changed = false;
        foreach (var joystick in player.controllers.Joysticks)
        {
            var maps = player.controllers.maps.GetMaps(ControllerType.Joystick, joystick.id).ToArray();
            var fresh = maps.Where(m => !Applied.ContainsKey(m)).ToArray();
            if (fresh.Length == 0) continue;
            if (!Swaps.TryGetValue(joystick.id, out var swap))
            {
                var notepad = ButtonOf(fresh, NotepadAction);
                var inventory = ButtonOf(fresh, InventoryAction);
                if (notepad is null || inventory is null || notepad == inventory) continue;
                swap = new Core.ButtonSwap(NotepadAction, notepad.Value, InventoryAction, inventory.Value);
                Swaps[joystick.id] = swap;
            }
            foreach (var map in fresh)
            {
                Move(map, swap);
                Applied[map] = swap;
            }
            changed = true;
        }
        return changed;
    }

    private static bool Revert()
    {
        if (Applied.Count == 0) return false;
        foreach (var applied in Applied) Move(applied.Key, applied.Value.Reversed);
        Applied.Clear();
        Swaps.Clear();
        return true;
    }

    /// <summary>On unload: the pad's maps back as the game set them.</summary>
    public static void Unload()
    {
        if (ReInput.isReady && Revert()) ButtonPromptsFeature.RefreshPrompts();
    }

    /// <summary>The button an action is on in these maps, if any.</summary>
    private static int? ButtonOf(IEnumerable<ControllerMap> maps, int actionId) => maps
        .SelectMany(m => m.GetElementMapsWithAction(actionId))
        .Where(e => e.elementType == ControllerElementType.Button)
        .Select(e => (int?)e.elementIdentifierId)
        .FirstOrDefault();

    private static void Move(ControllerMap map, Core.ButtonSwap swap)
    {
        foreach (var e in map.GetElementMaps())
        {
            if (e.elementType != ControllerElementType.Button) continue;
            if (swap.Moved(e.actionId, e.elementIdentifierId) is not { } element) continue;
            map.ReplaceElementMap(e.id, e.actionId, e.axisContribution, element, e.elementType, e.axisRange, e.invert);
        }
    }
}
