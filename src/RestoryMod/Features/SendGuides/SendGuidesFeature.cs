using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.Equipment.Ultrasonic;
using Restory.Gameplay.InteractiveObjects;
using Restory.Gameplay.Recycle;
using Restory.Gameplay.Shredders;
using Restory.Gameplay.Tooltips;
using Restory.Gameplay.Work.Dragging;
using RestoryMod.Core;
using RestoryMod.Features.DpadSend;
using RestoryMod.Features.PartsBox;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.SendGuides;

/// <summary>
/// While the player holds something the D-pad can send, every destination that would take it shows its
/// circle with the D-pad direction under it. The circles are the game's own; the game turns some of them
/// on by itself (the washing bin for a dirty part, the trash can for rubbish), this turns on the rest.
/// </summary>
internal sealed class SendGuidesFeature : IFeature
{
    /// <summary>Checked each frame, so it can be switched live from the config file.</summary>
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.SendGuides;

    public void Load(FeatureContext ctx)
    {
        Enabled = ctx.Config.Bind("Controller", "SendGuides", true,
            "While holding something with the gamepad that the D-pad can send (DpadSendParts), circles each " +
            "destination that would take it and shows the D-pad direction under the circle.");
        ctx.Patch(typeof(SendGuidesBenchPatch), typeof(SendGuidesCarryPatch));
        ctx.AddPoller<SendGuideMarkersPoller>();
        Canvas.willRenderCanvases += SendGuideMarkers.Align;
        ctx.OnUnload(SendGuideMarkers.Unload);
    }

    /// <summary>Guides show only while sends are on and a gamepad drives the cursor.</summary>
    public static bool Active =>
        Enabled.Value && DpadSendFeature.Enabled.Value && Rewired.ReInput.isReady &&
        Rewired.ReInput.controllers.GetLastActiveController()?.type == Rewired.ControllerType.Joystick;

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Send guides failed (further errors suppressed): {e}");
    }

    internal static AccessTools.FieldRef<SonicBath, TooltipIndicator>? sonicBathIndicator;
    internal static AccessTools.FieldRef<ElementCleaner, TooltipIndicator>? cleanerIndicator;
    internal static AccessTools.FieldRef<InventoryBox, TooltipIndicator>? inventoryBoxIndicator;
    internal static AccessTools.FieldRef<TrashCan, TooltipIndicator>? trashCanIndicator;
    internal static AccessTools.FieldRef<Shredder, TooltipIndicator>? shredderIndicator;
    internal static AccessTools.FieldRef<UltrasonicService, SonicBath>? sonicBathOf;
    internal static AccessTools.FieldRef<RecycleService, TrashCan>? trashCanOf;
    internal static AccessTools.FieldRef<ShredderService, Shredder>? shredderOf;

    internal static void Bind()
    {
        sonicBathIndicator ??= AccessTools.FieldRefAccess<SonicBath, TooltipIndicator>(PatchTargets.SonicBathIndicator.MemberName!);
        cleanerIndicator ??= AccessTools.FieldRefAccess<ElementCleaner, TooltipIndicator>(PatchTargets.CleanerIndicator.MemberName!);
        inventoryBoxIndicator ??= AccessTools.FieldRefAccess<InventoryBox, TooltipIndicator>(PatchTargets.InventoryBoxIndicator.MemberName!);
        trashCanIndicator ??= AccessTools.FieldRefAccess<TrashCan, TooltipIndicator>(PatchTargets.TrashCanIndicator.MemberName!);
        shredderIndicator ??= AccessTools.FieldRefAccess<Shredder, TooltipIndicator>(PatchTargets.ShredderIndicator.MemberName!);
        sonicBathOf ??= AccessTools.FieldRefAccess<UltrasonicService, SonicBath>(PatchTargets.UltrasonicSonicBath.MemberName!);
        trashCanOf ??= AccessTools.FieldRefAccess<RecycleService, TrashCan>(PatchTargets.RecycleTrashCan.MemberName!);
        shredderOf ??= AccessTools.FieldRefAccess<ShredderService, Shredder>(PatchTargets.ShredderServiceShredder.MemberName!);
    }

    private static InventoryBox? inventoryBox;

    /// <summary>The parts box's world trigger, which carries its circle; one per workplace scene.</summary>
    internal static InventoryBox? InventoryBox()
    {
        if (!inventoryBox) inventoryBox = Object.FindAnyObjectByType<InventoryBox>();
        return inventoryBox ? inventoryBox : null;
    }

    /// <summary>Owned shredders stand in the trash can's place; an unowned one is switched off.</summary>
    internal static bool Owned(Shredder? shredder) => shredder && shredder!.gameObject.activeInHierarchy;

    /// <summary>The trash can or shredder takes a drop only while the game has it open for the held thing.</summary>
    internal static bool Open(TrashCan? trashCan) => trashCan && trashCan!.gameObject.activeInHierarchy && trashCan.IsActive;

    internal static bool Open(Shredder? shredder) => Owned(shredder) && shredder!.IsActive;
}

/// <summary>At the workbench, while a part is held: guides for the four sends.</summary>
[HarmonyPatch]
internal static class SendGuidesBenchPatch
{
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.SendGuides,
        PatchTargets.DraggingOnUpdate,
        PatchTargets.DraggingSelectedElement,
        PatchTargets.DraggingIsRecycling,
        PatchTargets.DraggingIsShredding,
        PatchTargets.DraggingGameMode,
        PatchTargets.DraggingElementCleaner,
        PatchTargets.DraggingDeviceService,
        PatchTargets.DraggingUltrasonicService,
        PatchTargets.DraggingRecycleService,
        PatchTargets.DraggingShredderService,
        PatchTargets.UltrasonicSonicBath,
        PatchTargets.RecycleTrashCan,
        PatchTargets.ShredderServiceShredder,
        PatchTargets.SonicBathIndicator,
        PatchTargets.CleanerIndicator,
        PatchTargets.InventoryBoxIndicator,
        PatchTargets.TrashCanIndicator,
        PatchTargets.ShredderIndicator);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DraggingOnUpdate)!;

    private static void Postfix(
        ElementBase ___selectedElement,
        bool ___isRecycling,
        bool ___isShredding,
        DisassembleGameMode ___disassembleGameMode,
        ElementCleaner ___elementCleaner,
        DeviceService ___deviceService,
        UltrasonicService ___ultrasonicService,
        RecycleService ___recycleService,
        ShredderService ___shredderService)
    {
        try
        {
            if (!SendGuidesFeature.Active) return;
            if (___isRecycling || ___isShredding) return;
            if (!___selectedElement || !___selectedElement.IsDragging) return;
            // In a competition the game's drop skips every destination, and so does the send.
            if (___disassembleGameMode.IsInCompetition) return;

            SendGuidesFeature.Bind();
            var sonicBath = SendGuidesFeature.sonicBathOf!(___ultrasonicService);
            var trashCan = SendGuidesFeature.trashCanOf!(___recycleService);
            var shredder = SendGuidesFeature.shredderOf!(___shredderService);
            var part = DpadSendPatch.Describe(___selectedElement, ___elementCleaner, ___deviceService);
            part = part with { Surplus = IsSurplus(___selectedElement, ___deviceService) };

            TooltipIndicator? IndicatorOf(Destination destination) => destination switch
            {
                Destination.WashingBin => SendGuidesFeature.sonicBathIndicator!(sonicBath),
                Destination.ToolCup => SendGuidesFeature.cleanerIndicator!(___elementCleaner),
                Destination.TrashCan => SendGuidesFeature.trashCanIndicator!(trashCan),
                Destination.Shredder => SendGuidesFeature.shredderIndicator!(shredder),
                _ => SendGuidesFeature.InventoryBox() is { } box ? SendGuidesFeature.inventoryBoxIndicator!(box) : null,
            };

            var guides = Core.SendGuides.For(part, SendGuidesFeature.Owned(shredder), destination => destination switch
            {
                // The game opens the bath to a drag when it has a tool and room and is not running.
                Destination.WashingBin => sonicBath && sonicBath.ActiveTool && !sonicBath.IsFull &&
                    !sonicBath.Timer.IsCountdown,
                Destination.TrashCan => SendGuidesFeature.Open(trashCan),
                Destination.Shredder => SendGuidesFeature.Open(shredder),
                _ => true,
            }, destination => SendGuideMarkers.CircledByGame(IndicatorOf(destination)));
            foreach (var guide in guides)
                SendGuideMarkers.Want(IndicatorOf(guide.Destination), guide.Direction);
        }
        catch (Exception e)
        {
            SendGuidesFeature.LogOnce(e);
        }
    }

    private static ElementBase? countedFor;
    private static int countedFrame = -1;
    private static bool surplus;

    /// <summary>Counted once per pick-up: the bench does not change while the part is in hand.</summary>
    private static bool IsSurplus(ElementBase held, DeviceService deviceService)
    {
        var sameHold = held == countedFor && countedFrame >= Time.frameCount - 1;
        countedFrame = Time.frameCount;
        if (sameHold) return surplus;
        countedFor = held;
        surplus = true;
        var container = deviceService ? deviceService.PlacedDeviceContainer : null;
        var device = container ? container!.Device : null;
        if (!device) return surplus;

        var info = held.Info;
        var openSockets = device!.SortedSockets.Count(s => !s.NestedElement && s.CompatibleElementInfo == info);
        // The held part has left the bench surface; it counts as one of the loose copies.
        var looseCopies = 1 + BenchState.LooseParts(device).Count(e => e != held && e.Info == info);
        return surplus = PartSend.IsSurplus(looseCopies, openSockets);
    }
}

/// <summary>Away from the workbench, while something is carried: guides for the parts box and trash.</summary>
[HarmonyPatch]
internal static class SendGuidesCarryPatch
{
    private static AccessTools.FieldRef<DragResolver, RecycleService>? recycleServiceOf;
    private static AccessTools.FieldRef<DragResolver, ShredderService>? shredderServiceOf;

    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.SendGuides,
        PatchTargets.DraggingWorkStateOnUpdate,
        PatchTargets.WorkDraggingSelectedObject,
        PatchTargets.WorkDraggingIsWaitingForConfirmation,
        PatchTargets.WorkDraggingResolver,
        PatchTargets.ResolverRecycleService,
        PatchTargets.ResolverShredderService,
        PatchTargets.RecycleTrashCan,
        PatchTargets.ShredderServiceShredder,
        PatchTargets.InventoryBoxIndicator,
        PatchTargets.TrashCanIndicator,
        PatchTargets.ShredderIndicator);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DraggingWorkStateOnUpdate)!;

    private static void Postfix(
        InteractiveObject ___selectedObject,
        bool ___isWaitingForConfirmation,
        DragResolver ___dragResolver)
    {
        try
        {
            if (!SendGuidesFeature.Active) return;
            if (!___selectedObject || ___isWaitingForConfirmation) return;

            SendGuidesFeature.Bind();
            recycleServiceOf ??= AccessTools.FieldRefAccess<DragResolver, RecycleService>(PatchTargets.ResolverRecycleService.MemberName!);
            shredderServiceOf ??= AccessTools.FieldRefAccess<DragResolver, ShredderService>(PatchTargets.ResolverShredderService.MemberName!);
            var trashCan = SendGuidesFeature.trashCanOf!(recycleServiceOf(___dragResolver));
            var shredder = SendGuidesFeature.shredderOf!(shredderServiceOf(___dragResolver));

            var carried = ___selectedObject;
            var described = new CarriedObject(
                Rubbish: carried.TryGetComponent<TrashObject>(out _),
                HoldsParts: carried.TryGetComponent<ElementsContainer>(out _) || carried.TryGetComponent<ElementsBox>(out _));

            var guides = Core.SendGuides.For(described, SendGuidesFeature.Owned(shredder), destination => destination switch
            {
                Destination.TrashCan => SendGuidesFeature.Open(trashCan),
                Destination.Shredder => SendGuidesFeature.Open(shredder),
                _ => true,
            });
            foreach (var guide in guides)
            {
                var indicator = guide.Destination switch
                {
                    Destination.TrashCan => SendGuidesFeature.trashCanIndicator!(trashCan),
                    Destination.Shredder => SendGuidesFeature.shredderIndicator!(shredder),
                    _ => SendGuidesFeature.InventoryBox() is { } box ? SendGuidesFeature.inventoryBoxIndicator!(box) : null,
                };
                SendGuideMarkers.Want(indicator, guide.Direction);
            }
        }
        catch (Exception e)
        {
            SendGuidesFeature.LogOnce(e);
        }
    }
}
