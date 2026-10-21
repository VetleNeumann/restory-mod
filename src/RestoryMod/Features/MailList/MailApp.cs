using System.Reflection;
using HarmonyLib;
using Restory.UI.Presenters;
using RestoryMod.Core;
using RestoryMod.Features.ShoulderButtons;

namespace RestoryMod.Features.MailList;

/// <summary>
/// The running Mail app, captured between its launch and stop, for the D-pad in
/// <see cref="MailListPoller"/> and L1/R1 in <see cref="ShoulderButtonsPoller"/>.
/// </summary>
internal static class MailApp
{
    public static GUI_MailClient? Client;

    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.MailList,
        PatchTargets.MailLaunchProcess,
        PatchTargets.MailStopProcess);

    /// <summary>The Mail app when it runs and the pause menu is not over it.</summary>
    public static GUI_MailClient? Running()
    {
        var client = Client;
        if (!client || !client!.isActiveAndEnabled || ShoulderButtonsFeature.IsPaused()) return null;
        return client;
    }
}

/// <summary>Each launch of the Mail app starts with nothing kept selected.</summary>
[HarmonyPatch]
internal static class MailLaunchPatch
{
    private static bool Prepare() => MailApp.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.MailLaunchProcess)!;

    private static void Postfix(GUI_MailClient __instance)
    {
        MailApp.Client = __instance;
        MailListFeature.StopKeepingSelected();
    }
}

[HarmonyPatch]
internal static class MailStopPatch
{
    private static bool Prepare() => MailApp.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.MailStopProcess)!;

    private static void Prefix(GUI_MailClient __instance)
    {
        if (MailApp.Client != __instance) return;
        MailApp.Client = null;
        MailListFeature.StopKeepingSelected();
    }
}
