using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using Rewired;
using PlayerMouse = Rewired.Components.PlayerMouse;
using UnityEngine;

namespace RestoryMod.Features.Diagnostics;

/// <summary>
/// Logs the gamepad's UI bindings (Rewired joystick maps) and the virtual cursor's button
/// bindings when the virtual cursor first resolves its controls type. Rewired's maps live in
/// serialized data, not code, so this is the way to see which physical button drives submit,
/// click and navigation.
/// </summary>
[HarmonyPatch]
internal static class GamepadBindingsDump
{
    public static ConfigEntry<bool> Enabled = null!;
    private static bool dumped;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Debug", "DumpGamepadBindings", false,
            "Log the gamepad's UI bindings and the virtual cursor's button bindings once per session.");
    }

    // Dev aid only: not patched at all unless switched on at startup.
    private static bool Prepare() => Enabled.Value && GameTargets.AllPresent(FeatureManifests.GamepadBindingsDump);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.CursorControlsTypeChanged)!;

    private static void Postfix()
    {
        if (dumped || !ReInput.isReady) return;
        dumped = true;
        var mice = Object.FindObjectsByType<PlayerMouse>(FindObjectsSortMode.None);

        var log = new StringBuilder("Gamepad bindings:");
        foreach (var player in ReInput.players.GetPlayers())
        {
            log.Append($"\n  Player {player.id} '{player.name}', {player.controllers.joystickCount} joystick(s)");
            foreach (var map in player.controllers.maps.GetAllMaps(ControllerType.Joystick))
            {
                var category = ReInput.mapping.GetMapCategory(map.categoryId)?.name;
                log.Append($"\n    Map '{category}' layout {map.layoutId} enabled={map.enabled}");
                foreach (var element in map.AllMaps.OrderBy(e => e.actionId))
                {
                    var action = ReInput.mapping.GetAction(element.actionId)?.name;
                    log.Append($"\n      {action} ({element.actionId}) <- {element.elementIdentifierName} " +
                               $"[{element.elementType}, {element.axisContribution}, range {element.axisRange}]");
                }
            }
        }

        foreach (var mouse in mice)
        {
            log.Append($"\n  PlayerMouse '{mouse.name}' player {mouse.playerId} enabled={mouse.enabled}: " +
                       $"x={mouse.xAxis.actionName} y={mouse.yAxis.actionName} " +
                       $"left={mouse.leftButton.actionName} right={mouse.rightButton.actionName} " +
                       $"middle={mouse.middleButton.actionName}");
        }

        try
        {
            AppendKeyboardOnly(log);
        }
        catch (System.Exception e)
        {
            log.Append($"\n  Keyboard/mouse only: failed, {e.Message}");
        }
        Plugin.Logger.LogInfo(log.ToString());
    }

    /// <summary>
    /// Every action bound on keyboard or mouse but on no gamepad button, across all map categories
    /// (not only those the player has loaded now), from Rewired's map definitions.
    /// </summary>
    private static void AppendKeyboardOnly(StringBuilder log)
    {
        var joystick = ReInput.players.GetPlayer(GamePlayer.Id)?.controllers.Joysticks.FirstOrDefault();
        log.Append($"\n  Keyboard/mouse only (gamepad: {joystick?.name ?? "none connected"}):");
        foreach (var category in ReInput.mapping.MapCategories)
        {
            foreach (var layout in ReInput.mapping.KeyboardLayouts.Select(l => l.id).DefaultIfEmpty(0).Take(1))
            {
                var keyboard = ReInput.mapping.GetKeyboardMapInstance(category.id, layout);
                var mouse = ReInput.mapping.GetMouseMapInstance(category.id, layout);
                var pad = joystick is null ? null : ReInput.mapping.GetJoystickMapInstance(joystick, category.id, layout);
                var padActions = pad?.AllMaps.Select(e => e.actionId).ToHashSet() ?? new HashSet<int>();

                var desktop = (keyboard?.AllMaps ?? Enumerable.Empty<ActionElementMap>())
                    .Concat(mouse?.AllMaps ?? Enumerable.Empty<ActionElementMap>())
                    .Where(e => !padActions.Contains(e.actionId))
                    .GroupBy(e => e.actionId);
                foreach (var group in desktop)
                {
                    var action = ReInput.mapping.GetAction(group.Key)?.name;
                    var keys = string.Join(", ", group.Select(e => e.elementIdentifierName).Distinct());
                    log.Append($"\n    [{category.name}] {action} ({group.Key}) <- {keys}");
                }
            }
        }
    }
}
