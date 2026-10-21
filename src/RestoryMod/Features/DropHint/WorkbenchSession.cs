using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.DropHint;

internal sealed class WorkbenchSessionFeature : IFeature
{
    public FeatureManifest Manifest => FeatureManifests.WorkbenchSession;

    public void Load(FeatureContext ctx)
    {
        ctx.Patch(typeof(WorkbenchSessionStartPatch), typeof(WorkbenchSessionEndPatch));
        WorkbenchSession.Resume(ctx.Handoff);
        ctx.OnUnload(() => WorkbenchSession.Unload(ctx.Handoff));
    }
}

/// <summary>
/// The game's tooltip service for the workbench session: it runs the hint, holds the pulse's
/// highlighter and knows the device on the bench and whether the player is taking it apart or putting
/// it back together. The hint and the R2 button both read it here.
/// </summary>
internal static class WorkbenchSession
{
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, SmallElementTooltipHandler> SmallHandler =
        AccessTools.FieldRefAccess<DisassembleTooltipService, SmallElementTooltipHandler>("smallElementTooltipHandler");
    private static readonly AccessTools.FieldRef<SmallElementTooltipHandler, TooltipProjectionHighlighter> HandlerHighlighter =
        AccessTools.FieldRefAccess<SmallElementTooltipHandler, TooltipProjectionHighlighter>("tooltipProjectionHighlighter");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, Device> TargetDevice =
        AccessTools.FieldRefAccess<DisassembleTooltipService, Device>("targetDevice");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, bool> Disassembling =
        AccessTools.FieldRefAccess<DisassembleTooltipService, bool>("isDisassembling");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, DisassembleStateMachine> StateMachine =
        AccessTools.FieldRefAccess<DisassembleTooltipService, DisassembleStateMachine>("disassembleStateMachine");

    /// <summary>What keeping the service and reaching its highlighter needs.</summary>
    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.WorkbenchSession,
        PatchTargets.TooltipServiceInitialize,
        PatchTargets.TooltipServiceDispose,
        PatchTargets.TooltipServiceSmallHandler,
        PatchTargets.DropHintHighlighter);

    private static bool? statePresent;

    /// <summary>What <see cref="Device"/>, <see cref="TakingApart"/> and <see cref="Detecting"/> need.</summary>
    public static bool StatePresent => statePresent ??= TargetsPresent() && GameTargets.AllPresent(FeatureManifests.WorkbenchSession,
        PatchTargets.TooltipServiceTargetDevice,
        PatchTargets.TooltipServiceIsDisassembling,
        PatchTargets.TooltipServiceIsActive,
        PatchTargets.TooltipServiceStateMachine);

    /// <summary>The service while a scene has one, else null.</summary>
    public static DisassembleTooltipService? Service { get; set; }

    /// <summary>The highlighter the game's hints pulse with, or null.</summary>
    public static TooltipProjectionHighlighter? Highlighter(DisassembleTooltipService service)
    {
        var handler = SmallHandler(service);
        return handler is null ? null : HandlerHighlighter(handler);
    }

    /// <summary>The device on the bench, or null.</summary>
    public static Device? Device(DisassembleTooltipService service) => TargetDevice(service);

    public static bool TakingApart(DisassembleTooltipService service) => Disassembling(service);

    /// <summary>What the player is doing at the workbench (taking things apart, Cleaning, painting...).</summary>
    public static object ActiveState(DisassembleTooltipService service) => StateMachine(service).ActiveState;

    /// <summary>At the workbench with nothing else going on (not cleaning, not in a dialogue).</summary>
    public static bool Detecting(DisassembleTooltipService service) =>
        service.IsActive && StateMachine(service).ActiveState is DetectionDisassembleState;

    /// <summary>On unload: leaves the service for the next copy, which won't see Initialize again this scene.</summary>
    internal static void Unload(Handoff handoff)
    {
        if (Service is not null) handoff.Leave(Service);
    }

    /// <summary>Picks up the service the previous copy kept on a hot reload; nothing on a normal start.</summary>
    internal static void Resume(Handoff handoff)
    {
        if (!TargetsPresent()) return;
        if (handoff.Take() is not [DisassembleTooltipService service]) return;
        Service = service;
        Plugin.Logger.LogInfo("Workbench session taken over from the previous plugin copy.");
    }
}

/// <summary>Keeps the game's tooltip service when a scene sets it up.</summary>
[HarmonyPatch]
internal static class WorkbenchSessionStartPatch
{
    private static bool Prepare() => WorkbenchSession.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceInitialize)!;

    private static void Postfix(DisassembleTooltipService __instance) => WorkbenchSession.Service = __instance;
}

/// <summary>Forgets the tooltip service when its scene unloads.</summary>
[HarmonyPatch]
internal static class WorkbenchSessionEndPatch
{
    private static bool Prepare() => WorkbenchSession.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceDispose)!;

    private static void Postfix(DisassembleTooltipService __instance)
    {
        if (WorkbenchSession.Service == __instance) WorkbenchSession.Service = null;
    }
}
