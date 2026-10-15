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

    private const string VirtualCursorView = "Restory.UserInterface.VirtualCursorView";
    private const string SettingsMenu = "Restory.UI.Presenters.SettingsMenu.GUI_SettingsMenu";
    private const string SettingsMenuSoundView = "Restory.UI.Views.SettingsMenu.GUI_SettingsMenuSoundView";

    // Cursor speed: the game resets PlayerMouse.pointerSpeed in this handler on every controls-type change.
    public static readonly PatchTarget CursorControlsTypeChanged = new(VirtualCursorView, "ResolveControlsManagerOnControlsTypeChanged");
    public static readonly PatchTarget CursorPlayerMouse = new(VirtualCursorView, "playerMouse");
    public static readonly PatchTarget CursorControlsManager = new(VirtualCursorView, "controlsManager");

    // Settings menu: rows are prefab-wired, so new rows are cloned from an existing slider on Show().
    public static readonly PatchTarget SettingsMenuShow = new(SettingsMenu, "Show");
    public static readonly PatchTarget SettingsMenuSoundSettingsView = new(SettingsMenu, "soundSettingsView");
    public static readonly PatchTarget SoundViewDialogueVolumeSlider = new(SettingsMenuSoundView, "dialogueVolumeSlider");

    public static IReadOnlyList<PatchTarget> All { get; } = new[]
    {
        new PatchTarget("UnityEngine.EventSystems.CustomRewiredStandaloneInputModule"),
        new PatchTarget("Restory.Infrastructure.SceneInstallers.GameMechanics.GameCursorInstaller"),
        new PatchTarget("Restory.Infrastructure.CommonInstallers.ControllersInstaller"),
        new PatchTarget("Restory.Gameplay.PlayerInput.GameplayRewiredContextSwitcher"),
        new PatchTarget("Restory.Gameplay.GameSettings.ActiveControllerTypeManager"),
        new PatchTarget("Restory.Gameplay.GameCursor.CursorDetectorService"),
        new PatchTarget("Restory.Gameplay.Common.CameraRotationController"),
        new PatchTarget("Restory.Infrastructure.CommonServices.ControlsManager", "ControlType"),
        CursorControlsTypeChanged,
        CursorPlayerMouse,
        CursorControlsManager,
        SettingsMenuShow,
        SettingsMenuSoundSettingsView,
        SoundViewDialogueVolumeSlider,
    };
}
