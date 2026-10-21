using System;
using System.Reflection;
using HarmonyLib;
using Restory.UI.Presenters.Inventory.StorageSlotElements;
using Restory.UI.Views.StorageSlotElements;
using RestoryMod.Core;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.DpadSend;

/// <summary>
/// Sends from a parts box slot while grab is held on it, before the cursor has moved. The game only
/// starts dragging a held slot on the first cursor move, and a tap without moving selects the slot
/// (for placing several parts at once), so the tap is left alone: a D-pad press starts the drag the
/// way a move would, and the send itself follows in <see cref="DpadSendPatch"/> once the game's drag
/// state is running.
/// </summary>
internal static class DpadSendSlotLift
{
    /// <summary>The slot grab was last pressed on; it is still held while its isHolding is set.</summary>
    internal static StorageSlotElement? PressedSlot;

    private static DpadDirection? pending;
    private static int pendingFrame;

    private static AccessTools.FieldRef<StorageSlotElement, bool>? isHolding;
    private static Action<StorageSlotElement, StorageSlotElementView>? pointerMove;

    /// <summary>
    /// The direction pressed on a held slot, for the drag state to send with. The drag state may run
    /// its update before or after the press in a frame, so the direction keeps for one more frame.
    /// </summary>
    internal static DpadDirection? TakePending()
    {
        if (pending is not { } direction || Time.frameCount - pendingFrame > 1) return null;
        pending = null;
        return direction;
    }

    internal static void Poll(Player player)
    {
        var slot = PressedSlot;
        if (!slot) return;
        isHolding ??= AccessTools.FieldRefAccess<StorageSlotElement, bool>(PatchTargets.SlotIsHolding.MemberName!);
        if (!isHolding(slot!)) return;
        if (DpadSendPatch.PressedDirection(player) is not { } direction) return;
        if (!PartSend.SendsFromHeldSlot(direction)) return;

        pointerMove ??= AccessTools.MethodDelegate<Action<StorageSlotElement, StorageSlotElementView>>(
            GameTargets.Method(PatchTargets.SlotOnPointerMove)!);
        pending = direction;
        pendingFrame = Time.frameCount;
        // The game's own reaction to the first move of a held slot; its view argument is unused.
        pointerMove(slot!, null!);
    }
}

/// <summary>Remembers the slot grab was pressed on.</summary>
[HarmonyPatch]
internal static class DpadSendSlotPressPatch
{
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.DpadSend,
        PatchTargets.SlotOnPointerDown,
        PatchTargets.SlotOnPointerMove,
        PatchTargets.SlotIsHolding);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.SlotOnPointerDown)!;

    private static void Postfix(StorageSlotElement __instance) => DpadSendSlotLift.PressedSlot = __instance;
}

/// <summary>Polls the D-pad each frame for a held slot.</summary>
internal sealed class DpadSendPoller : MonoBehaviour
{
    private void Update()
    {
        try
        {
            if (!DpadSendFeature.Enabled.Value || !ReInput.isReady) return;
            var player = ReInput.players.GetPlayer(GamePlayer.Id);
            if (player is null) return;
            DpadSendSlotLift.Poll(player);
        }
        catch (Exception e)
        {
            DpadSendFeature.LogOnce(e);
        }
    }
}
