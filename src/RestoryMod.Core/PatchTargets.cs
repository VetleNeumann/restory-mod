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
    private const string InventoryPanel = "Restory.UI.Presenters.Inventory.InventoryPanel";
    private const string InventoryItemsView = "Restory.UI.Views.Inventory.InventoryItemsView";

    // Cursor speed: the game resets PlayerMouse.pointerSpeed in this handler on every controls-type change.
    public static readonly PatchTarget CursorControlsTypeChanged = new(VirtualCursorView, "ResolveControlsManagerOnControlsTypeChanged");
    public static readonly PatchTarget CursorPlayerMouse = new(VirtualCursorView, "playerMouse");
    public static readonly PatchTarget CursorControlsManager = new(VirtualCursorView, "controlsManager");

    // Settings menu: rows are prefab-wired, so new rows are cloned from an existing slider on Show().
    public static readonly PatchTarget SettingsMenuShow = new(SettingsMenu, "Show");
    public static readonly PatchTarget SettingsMenuSoundSettingsView = new(SettingsMenu, "soundSettingsView");
    public static readonly PatchTarget SoundViewDialogueVolumeSlider = new(SettingsMenuSoundView, "dialogueVolumeSlider");

    // Parts box: re-sorted after the game's sort; headers placed after the slot views are parented.
    public static readonly PatchTarget InventoryPanelConstruct = new(InventoryPanel, "Construct");
    public static readonly PatchTarget InventoryPanelShow = new(InventoryPanel, "Show");
    public static readonly PatchTarget InventoryPanelSortItems = new(InventoryPanel, "SortItems");
    public static readonly PatchTarget InventoryPanelUpdateItemsView = new(InventoryPanel, "UpdateItemsView");
    public static readonly PatchTarget InventoryPanelFilteredItems = new(InventoryPanel, "filteredItems");
    public static readonly PatchTarget InventoryPanelFilter = new(InventoryPanel, "filter");
    public static readonly PatchTarget InventoryPanelDeviceService = new(InventoryPanel, "deviceService");
    public static readonly PatchTarget InventoryPanelView = new(InventoryPanel, "view");
    public static readonly PatchTarget InventoryItemsViewContainer = new(InventoryItemsView, "container");

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
        InventoryPanelConstruct,
        InventoryPanelShow,
        InventoryPanelSortItems,
        InventoryPanelUpdateItemsView,
        InventoryPanelFilteredItems,
        InventoryPanelFilter,
        InventoryPanelDeviceService,
        InventoryPanelView,
        InventoryItemsViewContainer,
        // Read directly (compiled against) by BenchState; listed so updates that move them are caught.
        new PatchTarget("Restory.UI.Presenters.Inventory.InventoryPanelFilter", "Sort"),
        new PatchTarget("Restory.Gameplay.Devices.DeviceService", "PlacedDeviceContainer"),
        new PatchTarget("Restory.Gameplay.Devices.Device", "SortedSockets"),
        new PatchTarget("Restory.Gameplay.Elements.ElementSocket", "NestedElement"),
        new PatchTarget("Restory.Gameplay.Elements.ElementSocket", "CompatibleElementInfo"),
        new PatchTarget("Restory.Gameplay.Workplace.WorkSurface", "PlacedElements"),
        new PatchTarget("Restory.Gameplay.Devices.DismantledDevicePack", "PlacedElements"),
    };
}
