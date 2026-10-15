using System.Collections.Generic;

namespace RestoryMod.Core;

/// <summary>
/// A game type (and optionally a member on it) that the mod depends on.
/// </summary>
public sealed record PatchTarget(string TypeName, string? MemberName = null)
{
    public override string ToString() => MemberName is null ? TypeName : $"{TypeName}::{MemberName}";
}

/// <summary>
/// Every game type/member the mod hooks into. The plugin resolves these at runtime with AccessTools;
/// the TargetCheck tests resolve them against the installed game DLL after each game update.
/// Add an entry here whenever a patch starts depending on something new.
/// </summary>
public static class PatchTargets
{
    public const string GameAssembly = "Restory.Assembly";

    public static IReadOnlyList<PatchTarget> All { get; } = new[]
    {
        new PatchTarget("UnityEngine.EventSystems.CustomRewiredStandaloneInputModule"),
        new PatchTarget("Restory.Infrastructure.SceneInstallers.GameMechanics.GameCursorInstaller"),
        new PatchTarget("Restory.Infrastructure.CommonInstallers.ControllersInstaller"),
        new PatchTarget("Restory.Gameplay.PlayerInput.GameplayRewiredContextSwitcher"),
        new PatchTarget("Restory.Gameplay.GameSettings.ActiveControllerTypeManager"),
        new PatchTarget("Restory.Gameplay.GameCursor.CursorDetectorService"),
        new PatchTarget("Restory.Gameplay.Common.CameraRotationController"),
    };
}
