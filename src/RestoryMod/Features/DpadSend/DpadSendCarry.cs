using System;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.InteractiveObjects;
using Restory.Gameplay.Inventory;
using Restory.Gameplay.PlayerInput;
using Restory.Gameplay.Recycle;
using Restory.Gameplay.Shredders;
using Restory.Gameplay.Work.Dragging;
using Restory.Gameplay.Work.StateMachine;
using RestoryMod.Core;
using Rewired;

namespace RestoryMod.Features.DpadSend;

/// <summary>
/// Away from the workbench: D-pad left stores carried bought parts in the parts box, D-pad down throws
/// carried rubbish away. The game's drop runs a chain led by whatever the cursor is over (furniture,
/// payment, licenses), so instead of the whole drop only its matching branch runs: the destination's
/// own drop call with the destination lent as under the cursor, then on success the steps the game
/// takes after it (finish the drag, back to the detection state). A refusal changes nothing.
/// </summary>
[HarmonyPatch]
internal static class DpadSendCarryPatch
{
    private static Action<DragResolver>? completeDrag;
    private static AccessTools.FieldRef<DragResolver, RecycleService>? recycleServiceOf;
    private static AccessTools.FieldRef<DragResolver, ShredderService>? shredderServiceOf;
    private static AccessTools.FieldRef<DragResolver, InteractiveObjectsToObjectConsumersDragHandlingService>? consumersOf;
    private static AccessTools.FieldRef<InteractiveObjectsToObjectConsumersDragHandlingService, InventoryBoxDetector>? inventoryDetectorOf;
    private static Action<InventoryBoxDetector, bool>? setInventoryDetected;

    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.DpadSend,
        PatchTargets.DraggingWorkStateOnUpdate,
        PatchTargets.DraggingWorkStatePlayerInput,
        PatchTargets.WorkDraggingSelectedObject,
        PatchTargets.WorkDraggingIsWaitingForConfirmation,
        PatchTargets.WorkDraggingStateMachine,
        PatchTargets.WorkDraggingResolver,
        PatchTargets.ResolverCompleteDrag,
        PatchTargets.ResolverRecycleService,
        PatchTargets.ResolverShredderService,
        PatchTargets.ResolverConsumers,
        PatchTargets.ConsumersInventoryDetector,
        PatchTargets.InventoryDetectorIsDetected,
        PatchTargets.RecycleTrashCan,
        PatchTargets.ShredderServiceShredder);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DraggingWorkStateOnUpdate)!;

    private static void Postfix(
        IPlayerInput ___playerInput,
        InteractiveObject ___selectedObject,
        bool ___isWaitingForConfirmation,
        WorkStateMachine ___stateMachine,
        DragResolver ___dragResolver)
    {
        try
        {
            if (!DpadSendFeature.Enabled.Value) return;
            if (!___selectedObject || ___isWaitingForConfirmation) return;
            if (DpadSendPatch.PressedDirection(ReInput.players.GetPlayer(___playerInput.Id)) is not { } direction) return;

            Bind();
            var shredderService = shredderServiceOf!(___dragResolver);
            var shredder = DpadSendPatch.shredderOf!(shredderService);
            if (PartSend.CarriedDestinationFor(direction,
                    shredderOwned: shredder && shredder.gameObject.activeInHierarchy) is not { } destination) return;

            var carried = ___selectedObject;
            var described = new CarriedObject(
                Rubbish: carried.TryGetComponent<TrashObject>(out _),
                HoldsParts: carried.TryGetComponent<ElementsContainer>(out _) || carried.TryGetComponent<ElementsBox>(out _));
            if (!PartSend.Accepts(destination, described)) return;

            var sent = false;
            switch (destination)
            {
                case Destination.PartsBox:
                    var consumers = consumersOf!(___dragResolver);
                    var detector = inventoryDetectorOf!(consumers);
                    if (detector is null) return;
                    var wasDetected = detector.IsDetected;
                    setInventoryDetected!(detector, true);
                    try
                    {
                        sent = consumers.TryToDropDraggedObjectIntoInventory(carried);
                    }
                    finally
                    {
                        setInventoryDetected(detector, wasDetected);
                    }
                    break;

                case Destination.TrashCan:
                    var recycleService = recycleServiceOf!(___dragResolver);
                    var trashCan = DpadSendPatch.trashCanOf!(recycleService);
                    if (!trashCan || !trashCan.gameObject.activeInHierarchy || !trashCan.IsActive) return;
                    DpadSendPatch.WhilePointerOver(trashCan.IsDetected, trashCan.OnPointerEnter, trashCan.OnPointerExit,
                        () => sent = recycleService.TryToRecycleInteractiveObject(carried));
                    break;

                case Destination.Shredder:
                    if (!shredder || !shredder.gameObject.activeInHierarchy || !shredder.IsActive) return;
                    DpadSendPatch.WhilePointerOver(shredder.IsDetected, shredder.OnPointerEnter, shredder.OnPointerExit,
                        () => sent = shredderService.TryToShredInteractiveObject(carried));
                    break;
            }
            if (!sent) return;

            // What the game's drop does after any of these destinations takes the object.
            completeDrag!(___dragResolver);
            carried.CompleteDrag();
            ___stateMachine.Enter<DetectionWorkState>();
        }
        catch (Exception e)
        {
            DpadSendFeature.LogOnce(e);
        }
    }

    private static void Bind()
    {
        DpadSendPatch.Bind();
        completeDrag ??= AccessTools.MethodDelegate<Action<DragResolver>>(GameTargets.Method(PatchTargets.ResolverCompleteDrag)!);
        recycleServiceOf ??= AccessTools.FieldRefAccess<DragResolver, RecycleService>(PatchTargets.ResolverRecycleService.MemberName!);
        shredderServiceOf ??= AccessTools.FieldRefAccess<DragResolver, ShredderService>(PatchTargets.ResolverShredderService.MemberName!);
        consumersOf ??= AccessTools.FieldRefAccess<DragResolver, InteractiveObjectsToObjectConsumersDragHandlingService>(
            PatchTargets.ResolverConsumers.MemberName!);
        inventoryDetectorOf ??= AccessTools.FieldRefAccess<InteractiveObjectsToObjectConsumersDragHandlingService, InventoryBoxDetector>(
            PatchTargets.ConsumersInventoryDetector.MemberName!);
        setInventoryDetected ??= AccessTools.MethodDelegate<Action<InventoryBoxDetector, bool>>(
            AccessTools.PropertySetter(typeof(InventoryBoxDetector), PatchTargets.InventoryDetectorIsDetected.MemberName!));
    }
}
