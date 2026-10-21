using HarmonyLib;
using Restory.Gameplay.GameCursor;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>
/// Harness only: while a step aims at a thing, the game's cursor ray finds it even through whatever lies
/// in front (a screw underneath the device), so scenarios needn't turn the device to the right angle.
/// The ray still has to pass through the thing's own colliders, and turning the device is not under test.
/// </summary>
internal static class SeeThrough
{
    /// <summary>The thing the cursor aims at: a part or a socket, or null for the game's own detection.</summary>
    public static Transform? Target { get; set; }

    public static void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(GameCursorDetector), PatchTargets.CursorDetectorTryToDetect.MemberName,
            new[] { typeof(Vector3), typeof(LayerMask), typeof(RaycastHit[]), typeof(int).MakeByRefType() });
        if (target is null) throw new StepFailed("GameCursorDetector.TryToDetect moved; the harness can't aim through things");
        harmony.Patch(target, postfix: new HarmonyMethod(typeof(SeeThrough), nameof(Postfix)) { priority = Priority.Last });
    }

    private static void Postfix(Vector3 screenPoint, RaycastHit[] raycastHits, ref int hitCount, ref bool __result,
        Camera ___gameCamera)
    {
        if (!Target || raycastHits.Length == 0) return;
        var ray = ___gameCamera.ScreenPointToRay(screenPoint);
        foreach (var collider in Target!.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || !collider.Raycast(ray, out var hit, 4f)) continue;
            raycastHits[0] = hit;
            hitCount = 1;
            __result = true;
            return;
        }
    }
}
