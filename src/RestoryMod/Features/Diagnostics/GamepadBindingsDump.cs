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
    private static bool Prepare() => Enabled.Value && GameTargets.AllPresent(PatchTargets.CursorControlsTypeChanged);

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

        Plugin.Logger.LogInfo(log.ToString());
    }
}
