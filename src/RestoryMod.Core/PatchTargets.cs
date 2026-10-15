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
    private const string InventoryPanelViewType = "Restory.UI.Views.Inventory.InventoryPanelView";
    private const string NotepadActivator = "Restory.Gameplay.OverlayActivators.NotepadActivator";
    private const string NotepadWindow = "Restory.UI.Presenters.Notepad.GUI_NotepadWindow";
    private const string UiInputModule = "UnityEngine.EventSystems.CustomRewiredStandaloneInputModule";

    // Cursor speed: the game resets PlayerMouse.pointerSpeed in this handler on every controls-type change.
    public static readonly PatchTarget CursorControlsTypeChanged = new(VirtualCursorView, "ResolveControlsManagerOnControlsTypeChanged");
    public static readonly PatchTarget CursorPlayerMouse = new(VirtualCursorView, "playerMouse");
    public static readonly PatchTarget CursorControlsManager = new(VirtualCursorView, "controlsManager");

    // Dropdown modes: the UI input module sends gamepad moves, submits and virtual-cursor clicks;
    // each step is gated while a dropdown is open. The cursor image is hidden in nav mode.
    public static readonly PatchTarget UiInputProcess = new(UiInputModule, "Process");
    public static readonly PatchTarget UiInputSendMove = new(UiInputModule, "SendMoveEventToSelectedObject");
    public static readonly PatchTarget UiInputSendSubmit = new(UiInputModule, "SendSubmitEventToSelectedObject");
    public static readonly PatchTarget UiInputProcessMouseEvent = new(UiInputModule, "ProcessMouseEvent");
    public static readonly PatchTarget CursorUpdateImageSetActive = new(VirtualCursorView, "UpdateImageSetActive");
    public static readonly PatchTarget CursorImage = new(VirtualCursorView, "image");

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
    public static readonly PatchTarget InventoryPanelDragService = new(InventoryPanel, "dragService");
    public static readonly PatchTarget InventoryPanelDropService = new(InventoryPanel, "dropService");
    public static readonly PatchTarget DragServiceState = new("Restory.Gameplay.Inventory.StorageElasticElementsDragService", "dragState");
    public static readonly PatchTarget DropServiceIsProcess = new("Restory.Gameplay.Inventory.StorageElasticElementsDropService", "IsProcess");
    public static readonly PatchTarget InventoryPanelViewItems = new(InventoryPanelViewType, "Items");
    public static readonly PatchTarget InventoryItemsViewContainer = new(InventoryItemsView, "container");

    // Notepad key: listened for beside the game's own listener; the game's hotkeyPressed flag holds Open.
    public static readonly PatchTarget NotepadActivatorInitialize = new(NotepadActivator, "Initialize");
    public static readonly PatchTarget NotepadActivatorDispose = new(NotepadActivator, "Dispose");
    public static readonly PatchTarget NotepadActivatorPlayerInput = new(NotepadActivator, "playerInput");
    public static readonly PatchTarget NotepadActivatorWindow = new(NotepadActivator, "notepadWindow");
    public static readonly PatchTarget NotepadActivatorItem = new(NotepadActivator, "notepadInteractiveWorkplaceItem");
    public static readonly PatchTarget NotepadActivatorPauseMenu = new(NotepadActivator, "pauseMenu");
    public static readonly PatchTarget NotepadWindowUpdateViewState = new(NotepadWindow, "UpdateViewState");
    public static readonly PatchTarget NotepadWindowHotkeyPressed = new(NotepadWindow, "hotkeyPressed");
    public static readonly PatchTarget NotepadWindowIsSlidingMode = new(NotepadWindow, "isSlidingMode");
    public static readonly PatchTarget NotepadWindowItem = new(NotepadWindow, "notepadInteractiveWorkplaceItem");

    public static IReadOnlyList<PatchTarget> All { get; } = new[]
    {
        CursorControlsTypeChanged,
        CursorPlayerMouse,
        CursorControlsManager,
        UiInputProcess,
        UiInputSendMove,
        UiInputSendSubmit,
        UiInputProcessMouseEvent,
        CursorUpdateImageSetActive,
        CursorImage,
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
        InventoryPanelDragService,
        InventoryPanelDropService,
        DragServiceState,
        DropServiceIsProcess,
        InventoryPanelViewItems,
        InventoryItemsViewContainer,
        NotepadActivatorInitialize,
        NotepadActivatorDispose,
        NotepadActivatorPlayerInput,
        NotepadActivatorWindow,
        NotepadActivatorItem,
        NotepadActivatorPauseMenu,
        NotepadWindowUpdateViewState,
        NotepadWindowHotkeyPressed,
        NotepadWindowIsSlidingMode,
        NotepadWindowItem,

        // Members the plugin reads directly (compiled against) rather than through the entries above;
        // listed so the TargetCheck tests catch a game update that moves them.
        new PatchTarget("Restory.Infrastructure.CommonServices.ControlsManager", "ControlType"),
        new PatchTarget(UiInputModule, "RewiredPlayerIds"),
        new PatchTarget(UiInputModule, "HorizontalActionId"),
        new PatchTarget(UiInputModule, "VerticalActionId"),
        new PatchTarget(UiInputModule, "SubmitActionId"),
        new PatchTarget("Restory.UI.Presenters.Inventory.InventoryPanel", "IsVisible"),
        new PatchTarget("Restory.UI.Presenters.Inventory.InventoryPanelFilter", "Sort"),
        new PatchTarget("Restory.Gameplay.Devices.DeviceService", "PlacedDeviceContainer"),
        new PatchTarget("Restory.Gameplay.Devices.Device", "SortedSockets"),
        new PatchTarget("Restory.Gameplay.Elements.ElementSocket", "NestedElement"),
        new PatchTarget("Restory.Gameplay.Elements.ElementSocket", "CompatibleElementInfo"),
        new PatchTarget("Restory.Gameplay.Workplace.WorkSurface", "PlacedElements"),
        new PatchTarget("Restory.Gameplay.Devices.DismantledDevicePack", "PlacedElements"),
        new PatchTarget("Restory.Gameplay.Devices.DeviceContainer", "Device"),
        new PatchTarget("Restory.Gameplay.Elements.ElementBase", "Info"),
        new PatchTarget("Restory.Gameplay.Elements.PlacedElements", "ElementsOnSurface"),
        new PatchTarget("Restory.Gameplay.Elements.ElementTransformRecord", "Element"),
        new PatchTarget("Restory.Gameplay.Elements.ElementData", "Info"),
        new PatchTarget("Restory.Gameplay.Elements.ElementData", "Condition"),
        new PatchTarget("Restory.Data.Elements.ElementInfo", "Category"),
        new PatchTarget("Restory.Data.Elements.Condition.BurntElementCondition"),
        new PatchTarget("Restory.Data.Elements.Condition.DamagedElementCondition"),
        new PatchTarget("Restory.Data.Elements.Condition.DirtyElementCondition"),
        new PatchTarget("Restory.Data.Elements.Condition.PerfectElementCondition"),
        new PatchTarget("Restory.StorageSystem.StorageElements.StorageItemElement", "ElementData"),
        new PatchTarget("Restory.StorageSystem.IReadOnlyStorageSlot", "Item"),
        new PatchTarget("Restory.StorageSystem.IReadOnlyStorageSlot", "Index"),
        new PatchTarget("Restory.StorageSystem.IReadOnlyStorageSlot", "Count"),
        new PatchTarget("Restory.UI.Presenters.Notepad.GUI_NotepadWindow", "IsVisible"),
        new PatchTarget("Restory.UI.Presenters.Notepad.GUI_NotepadWindow", "IsSlidingMode"),
        new PatchTarget("Restory.Gameplay.Equipment.NotepadInteractiveWorkplaceItem", "WindowIsPinned"),
        new PatchTarget("Restory.UI.Presenters.PauseMenu.GUI_PauseMenu", "IsShown"),
        new PatchTarget("Restory.Gameplay.PlayerInput.IPlayerInput", "AddInputEventDelegate"),
        new PatchTarget("Restory.Gameplay.PlayerInput.IPlayerInput", "RemoveInputEventDelegate"),
    };
}
