using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements.Condition;
using Restory.Gameplay.Disassemble;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.Equipment.Ultrasonic;
using Restory.Gameplay.Inventory;
using Restory.Gameplay.PlayerInput;
using Restory.Gameplay.Quests;
using Restory.Gameplay.Recycle;
using Restory.Gameplay.Shredders;
using RestoryMod.Core;
using Rewired;

namespace RestoryMod.Features.DpadSend;

internal static class DpadSendFeature
{
    /// <summary>Checked on each press, so it can be switched live from the config file.</summary>
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "DpadSendParts", true,
            "At the workbench, while holding a part with the gamepad, the D-pad sends it straight to a " +
            "destination: up to the washing bin, right to the tool cup, left to the parts box, down to the " +
            "trash can (or the shredder once bought). A destination that would refuse the part does nothing.");
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"D-pad part send failed (further errors suppressed): {e}");
    }
}

/// <summary>
/// Sends the held part on a D-pad press by running the game's own drop with the chosen destination
/// as the target: the destination's hover state is set as if the cursor were over it, then the
/// game's release handler runs. Sounds, state changes and the tool cup's cleaning panel follow as
/// on a real drop. Runs after the game's per-frame drag update, which recomputes the hover state.
/// </summary>
[HarmonyPatch]
internal static class DpadSendPatch
{
    /// <summary>The game's UI axes, bound to both the D-pad and the left stick.</summary>
    private const int UiHorizontalActionId = 18;
    private const int UiVerticalActionId = 19;

    /// <summary>
    /// Set while the send runs the game's release. A release that finds no destination ends in
    /// CompleteDrag (nearby socket or bench surface); <see cref="DpadSendFallbackGuardPatch"/> skips
    /// it then, so a refusal the checks could not foresee (e.g. a full parts box) keeps the part held.
    /// </summary>
    internal static bool Sending;

    /// <summary>The drag service's state while a part from the parts box shows as its icon.</summary>
    private const string SlotActiveDragState = "SlotActive";

    private static Action<DraggingDisassembleState, InputActionEventData>? release;
    private static FieldInfo? dragStateField;
    private static AccessTools.FieldRef<UltrasonicService, SonicBath>? sonicBathOf;
    internal static AccessTools.FieldRef<RecycleService, TrashCan>? trashCanOf;
    internal static AccessTools.FieldRef<ShredderService, Shredder>? shredderOf;

    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.DraggingOnUpdate,
        PatchTargets.DraggingRelease,
        PatchTargets.DraggingPlayerInput,
        PatchTargets.DraggingSelectedElement,
        PatchTargets.DraggingIsRecycling,
        PatchTargets.DraggingIsShredding,
        PatchTargets.DraggingIsOverInventory,
        PatchTargets.DraggingIsOverSonicBath,
        PatchTargets.DraggingIsOverCleaner,
        PatchTargets.DraggingGameMode,
        PatchTargets.DraggingElementCleaner,
        PatchTargets.DraggingUltrasonicService,
        PatchTargets.DraggingRecycleService,
        PatchTargets.DraggingShredderService,
        PatchTargets.UltrasonicSonicBath,
        PatchTargets.RecycleTrashCan,
        PatchTargets.ShredderServiceShredder,
        PatchTargets.DraggingStorageDragService,
        PatchTargets.DragServiceState);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DraggingOnUpdate)!;

    private static void Postfix(
        DraggingDisassembleState __instance,
        IPlayerInput ___playerInput,
        ElementBase ___selectedElement,
        bool ___isRecycling,
        bool ___isShredding,
        ref bool ___isOverInventory,
        ref bool ___isOverSonicBath,
        ref bool ___isOverCleaner,
        DisassembleGameMode ___disassembleGameMode,
        ElementCleaner ___elementCleaner,
        UltrasonicService ___ultrasonicService,
        RecycleService ___recycleService,
        ShredderService ___shredderService,
        StorageElasticElementsDragService ___storageElasticElementsDragService)
    {
        try
        {
            if (!DpadSendFeature.Enabled.Value) return;
            if (___isRecycling || ___isShredding) return;
            if (!___selectedElement || !___selectedElement.IsDragging) return;
            // In a competition the game's drop skips every destination; so does the send.
            if (___disassembleGameMode.IsInCompetition) return;
            var pressed = PressedDirection(ReInput.players.GetPlayer(___playerInput.Id));
            if ((pressed ?? DpadSendSlotLift.TakePending()) is not { } direction) return;

            // A part dragged out of the parts box is hidden behind its icon while the cursor is over
            // the box; any other hidden part is not the player's to send.
            Bind();
            var drag = ___storageElasticElementsDragService;
            var shownAsIcon = drag != null && dragStateField!.GetValue(drag)?.ToString() == SlotActiveDragState;
            if (!___selectedElement.isActiveAndEnabled && !shownAsIcon) return;

            if (PartSend.PutsBackInSlot(direction, shownAsIcon))
            {
                Release(__instance);
                return;
            }
            var shredder = shredderOf!(___shredderService);
            var destination = PartSend.DestinationFor(direction,
                shredderOwned: shredder && shredder.gameObject.activeInHierarchy);

            var element = ___selectedElement;
            var part = new HeldPart(
                Damaged: element.ConditionHandler.ElementData.Condition is DamagedElementCondition,
                QuestItem: element is QuestItem,
                NeedsCleaning: ___elementCleaner.DraggingElementInitialCleaningData != null);
            if (!PartSend.Accepts(destination, part)) return;

            // Telling the drag service the cursor left the box swaps the icon for the part itself,
            // as moving out of the box does. The real value is restored after, so the next drag out
            // of the box starts as an icon again.
            var wasOverInventory = shownAsIcon && drag!.IsPointerOverInventory;
            if (shownAsIcon) drag!.IsPointerOverInventory = false;
            try
            {
                SendTo(destination, __instance, element, shredder, ___ultrasonicService, ___recycleService,
                    ref ___isOverInventory, ref ___isOverSonicBath, ref ___isOverCleaner);
            }
            finally
            {
                if (shownAsIcon) drag!.IsPointerOverInventory = wasOverInventory;
            }
        }
        catch (Exception e)
        {
            DpadSendFeature.LogOnce(e);
        }
    }

    private static void SendTo(Destination destination, DraggingDisassembleState state, ElementBase element,
        Shredder? shredder, UltrasonicService ultrasonicService, RecycleService recycleService,
        ref bool isOverInventory, ref bool isOverSonicBath, ref bool isOverCleaner)
    {
        if (destination == Destination.WashingBin)
        {
            // Fitting is how the game seats a part over the bath; it fails while the bath is
            // locked, full or running, and the release then inserts the fitted part.
            var sonicBath = sonicBathOf!(ultrasonicService);
            if (!sonicBath) return;
            var fitPoint = sonicBath.ElementFitter.transform.position;
            if (!ultrasonicService.TryFitElementToSonicBath(element, fitPoint)) return;
        }

        // Only the chosen destination counts as under the cursor during the release.
        isOverInventory = false;
        isOverSonicBath = false;
        isOverCleaner = false;

        switch (destination)
        {
            case Destination.PartsBox:
                isOverInventory = true;
                Release(state);
                break;

            case Destination.WashingBin:
                isOverSonicBath = true;
                Release(state);
                break;

            case Destination.ToolCup:
                isOverCleaner = true;
                Release(state);
                break;

            case Destination.TrashCan:
                var trashCan = trashCanOf!(recycleService);
                if (!trashCan || !trashCan.gameObject.activeInHierarchy || !trashCan.IsActive) return;
                ReleaseOver(state, trashCan.IsDetected, trashCan.OnPointerEnter, trashCan.OnPointerExit);
                break;

            case Destination.Shredder:
                if (!shredder || !shredder.gameObject.activeInHierarchy || !shredder.IsActive) return;
                ReleaseOver(state, shredder.IsDetected, shredder.OnPointerEnter, shredder.OnPointerExit);
                break;
        }
    }

    private static void Release(DraggingDisassembleState state)
    {
        Sending = true;
        try
        {
            release!(state, default);
        }
        finally
        {
            Sending = false;
        }
    }

    /// <summary>A D-pad direction pressed this frame; a stick crossing the axis threshold is not one.</summary>
    internal static DpadDirection? PressedDirection(Player? player)
    {
        if (player is null) return null;
        if (player.GetButtonDown(UiVerticalActionId) && IsDpad(player, UiVerticalActionId)) return DpadDirection.Up;
        if (player.GetNegativeButtonDown(UiVerticalActionId) && IsDpad(player, UiVerticalActionId)) return DpadDirection.Down;
        if (player.GetButtonDown(UiHorizontalActionId) && IsDpad(player, UiHorizontalActionId)) return DpadDirection.Right;
        if (player.GetNegativeButtonDown(UiHorizontalActionId) && IsDpad(player, UiHorizontalActionId)) return DpadDirection.Left;
        return null;
    }

    /// <summary>The action is held by a gamepad button (the D-pad) rather than an axis (the stick).</summary>
    private static bool IsDpad(Player player, int actionId)
    {
        foreach (var source in player.GetCurrentInputSources(actionId))
        {
            if (source.controllerType == ControllerType.Joystick &&
                source.actionElementMap.elementType == ControllerElementType.Button)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The trash can and shredder learn they are under the cursor from pointer events; lend one for
    /// the release and take it back after, unless the cursor really is over them.
    /// </summary>
    private static void ReleaseOver(DraggingDisassembleState state, bool alreadyDetected,
        Action<UnityEngine.EventSystems.PointerEventData> enter,
        Action<UnityEngine.EventSystems.PointerEventData> exit) =>
        WhilePointerOver(alreadyDetected, enter, exit, () => Release(state));

    /// <summary>Runs <paramref name="act"/> with a pointer-detected destination lent as under the cursor.</summary>
    internal static void WhilePointerOver(bool alreadyDetected,
        Action<UnityEngine.EventSystems.PointerEventData> enter,
        Action<UnityEngine.EventSystems.PointerEventData> exit,
        Action act)
    {
        if (alreadyDetected)
        {
            act();
            return;
        }
        enter(null!);
        try
        {
            act();
        }
        finally
        {
            exit(null!);
        }
    }

    internal static void Bind()
    {
        dragStateField ??= GameTargets.Field(PatchTargets.DragServiceState);
        release ??= AccessTools.MethodDelegate<Action<DraggingDisassembleState, InputActionEventData>>(
            GameTargets.Method(PatchTargets.DraggingRelease)!);
        sonicBathOf ??= AccessTools.FieldRefAccess<UltrasonicService, SonicBath>(PatchTargets.UltrasonicSonicBath.MemberName!);
        trashCanOf ??= AccessTools.FieldRefAccess<RecycleService, TrashCan>(PatchTargets.RecycleTrashCan.MemberName!);
        shredderOf ??= AccessTools.FieldRefAccess<ShredderService, Shredder>(PatchTargets.ShredderServiceShredder.MemberName!);
    }
}

/// <summary>Keeps the part held when a send's release finds no destination; see <see cref="DpadSendPatch.Sending"/>.</summary>
[HarmonyPatch]
internal static class DpadSendFallbackGuardPatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.DraggingCompleteDrag,
        PatchTargets.DraggingIsRecycling,
        PatchTargets.DraggingIsShredding);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DraggingCompleteDrag)!;

    /// <summary>
    /// A refused trash or shred request also ends in CompleteDrag, after the drag state has already
    /// locked itself for the request; that one must run, or the drag never ends.
    /// </summary>
    private static bool Prefix(bool ___isRecycling, bool ___isShredding) =>
        !DpadSendPatch.Sending || ___isRecycling || ___isShredding;
}
