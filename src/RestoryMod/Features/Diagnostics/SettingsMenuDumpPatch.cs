using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryMod.Features.Diagnostics;

/// <summary>
/// Dev aid: logs the settings menu's transform hierarchy once per session, so slider rows can be
/// cloned without guessing the prefab layout. Toggle with [Debug] DumpSettingsMenu.
/// </summary>
[HarmonyPatch]
internal static class SettingsMenuDumpPatch
{
    public static ConfigEntry<bool> Enabled = null!;
    private static bool dumped;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Debug", "DumpSettingsMenu", false,
            "Log the settings menu hierarchy the first time it opens each session.");
    }

    private static bool Prepare() => GameTargets.Method(PatchTargets.SettingsMenuShow) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.SettingsMenuShow)!;

    private static void Postfix(MonoBehaviour __instance)
    {
        if (dumped || !Enabled.Value) return;
        dumped = true;

        var sb = new StringBuilder("Settings menu hierarchy:\n");
        Dump(__instance.transform, 0, sb);
        Plugin.Logger.LogInfo(sb.ToString());
    }

    private static void Dump(Transform t, int depth, StringBuilder sb)
    {
        var components = t.GetComponents<Component>()
            .Where(c => c != null && c is not Transform)
            .Select(Describe);
        sb.Append(' ', depth * 2)
          .Append(t.gameObject.activeSelf ? "" : "(inactive) ")
          .Append(t.name)
          .Append("  [").Append(string.Join(", ", components)).Append("]\n");

        for (var i = 0; i < t.childCount; i++)
            Dump(t.GetChild(i), depth + 1, sb);
    }

    private static string Describe(Component c)
    {
        var name = c.GetType().Name;
        if (c is Slider s)
            return $"{name}(min={s.minValue}, max={s.maxValue}, whole={s.wholeNumbers}, value={s.value})";

        // TextMeshPro and other text components expose a string "text" property.
        var text = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
        if (text?.PropertyType == typeof(string))
            return $"{name}(\"{text.GetValue(c)}\")";

        return name;
    }
}
