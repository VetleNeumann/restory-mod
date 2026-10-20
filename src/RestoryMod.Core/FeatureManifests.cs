using System.Collections.Generic;
using static RestoryMod.Core.PatchTargets;

namespace RestoryMod.Core;

/// <summary>
/// A feature and every game type/member its code touches: the targets it patches or reads by name
/// (the <see cref="PatchTargets"/> fields) and the ones it is compiled against. A target several
/// features use is in each of their manifests, so a failing TargetCheck case names every feature
/// a game update broke.
/// </summary>
public sealed record FeatureManifest(string Name, IReadOnlyList<PatchTarget> Targets)
{
    public FeatureManifest(string name, params PatchTarget[] targets) : this(name, (IReadOnlyList<PatchTarget>)targets) { }

    public override string ToString() => Name;
}

/// <summary>
/// One manifest per feature. A feature whose patches share one gate checks its whole manifest in
/// Prepare(); features gated patch by patch check subsets of it. Compiled-against members are written
/// inline in their feature's manifest, or as a private field here when several features share them.
/// Add a target to every manifest whose code starts depending on it.
/// </summary>
public static class FeatureManifests
{
    private const string ElementBase = "Restory.Gameplay.Elements.ElementBase";
    private const string ElementProjection = "Restory.Gameplay.Elements.ElementProjection";
    private const string IPlayerInput = "Restory.Gameplay.PlayerInput.IPlayerInput";
    private const string TrashCan = "Restory.Gameplay.Equipment.TrashCan";
    private const string Shredder = "Restory.Gameplay.Equipment.Shredder";

    // Compiled against by several features.
    private static readonly PatchTarget ControlsManagerControlType = new("Restory.Infrastructure.CommonServices.ControlsManager", "ControlType");
    private static readonly PatchTarget VirtualCursorViewType = new(VirtualCursorView);
    private static readonly PatchTarget ElementInfo = new(ElementBase, "Info");
    private static readonly PatchTarget ElementInSocket = new(ElementBase, "InSocket");
    private static readonly PatchTarget ElementIsBlocked = new(ElementBase, "IsBlocked");
    private static readonly PatchTarget ElementIsActivated = new(ElementBase, "IsActivated");
    private static readonly PatchTarget ElementConditionHandler = new(ElementBase, "ConditionHandler");
    private static readonly PatchTarget ConditionHandlerData = new("Restory.Gameplay.Elements.ElementConditionHandler", "ElementData");
    private static readonly PatchTarget ElementDataCondition = new("Restory.Gameplay.Elements.ElementData", "Condition");
    private static readonly PatchTarget DamagedCondition = new("Restory.Data.Elements.Condition.DamagedElementCondition");
    private static readonly PatchTarget InfoCategory = new("Restory.Data.Elements.ElementInfo", "Category");
    private static readonly PatchTarget CategorySmall = new("Restory.Data.Elements.ElementCategory", "Small");
    private static readonly PatchTarget ProjectionType = new(ElementProjection);
    private static readonly PatchTarget ProjectionOutlinable = new(ElementProjection, "OutlinableAdapter");
    private static readonly PatchTarget OutlinableOutlinable = new(OutlinableAdapter, "Outlinable");
    private static readonly PatchTarget OutlinableOverridePreset = new(OutlinableAdapter, "OverridePreset");
    private static readonly PatchTarget SwitchableElement = new("Restory.Gameplay.Elements.ISwitchableElement");
    private static readonly PatchTarget ElementButton = new("Restory.Gameplay.Elements.ElementButton");
    private static readonly PatchTarget QuestItem = new("Restory.Gameplay.Quests.QuestItem");
    private static readonly PatchTarget SonicBath = new("Restory.Gameplay.Equipment.Ultrasonic.SonicBath");
    private static readonly PatchTarget DeviceServicePlacedDevice = new("Restory.Gameplay.Devices.DeviceService", "PlacedDeviceContainer");
    private static readonly PatchTarget DeviceContainer = new("Restory.Gameplay.Devices.DeviceContainer");
    private static readonly PatchTarget DeviceContainerDevice = new("Restory.Gameplay.Devices.DeviceContainer", "Device");
    private static readonly PatchTarget WorkSurfacePlacedElements = new("Restory.Gameplay.Workplace.WorkSurface", "PlacedElements");
    private static readonly PatchTarget InventoryPanelIsVisible = new(InventoryPanel, "IsVisible");
    private static readonly PatchTarget PauseMenuIsShown = new("Restory.UI.Presenters.PauseMenu.GUI_PauseMenu", "IsShown");
    private static readonly PatchTarget DetectionWorkStateType = new(DetectionWorkState);
    private static readonly PatchTarget PlayerInputGetButton = new(IPlayerInput, "GetButton");
    private static readonly PatchTarget SelectionDetectedObject = new("Restory.Gameplay.GameCursor.CursorSelectionService", "DetectedGameObject");
    private static readonly PatchTarget RotationButtonPressed = new(RotationController, "IsRotationButtonPressed");
    private static readonly PatchTarget ActivateAction = new("RewiredConsts.Action+MiniGame", "Activate");

    /// <summary>The workbench listing both hitbox features read (WorkbenchHitboxes).</summary>
    private static readonly PatchTarget[] WorkbenchListing =
    {
        SocketScrewSlot,
        DeviceServiceWorkSurface,
        SocketCompatibleElementInfo,
        DeviceServicePlacedDevice,
        DeviceContainer,
        DeviceContainerDevice,
        WorkSurfacePlacedElements,
        ElementInfo,
        ElementInSocket,
        ElementIsBlocked,
        ElementIsActivated,
        InfoCategory,
        CategorySmall,
        ProjectionType,
        SwitchableElement,
        ElementButton,
        SonicBath,
        new("Restory.Constants.ProjectConstants+Layers", "Projections"),
        new("Restory.Constants.ProjectConstants+Layers", "Elements"),
        new("Restory.Constants.ProjectConstants+Layers", "Device"),
        new("Restory.Data.Base.RestoryEntityInfoBase", "ID"),
    };

    public static readonly FeatureManifest ButtonPrompts = new(nameof(ButtonPrompts),
        PromptGetControllerId,
        PromptUpdateData,
        PromptControllerIdsList,
        PromptControllerIdsMap,
        PromptGameSettings,
        ControllerIdsMapLookup,
        ControllerIdsLookup,
        ControllerIdsDefaultGamepad,
        GameSettingsGamepadScheme);

    public static readonly FeatureManifest PlayStationArt = new(nameof(PlayStationArt),
        PromptImageUpdateView,
        PromptImageTarget,
        PromptViewControllerId,
        PromptViewElementId);

    public static readonly FeatureManifest CursorSpeed = new(nameof(CursorSpeed),
        CursorControlsTypeChanged,
        CursorPlayerMouse,
        CursorControlsManager,
        ControlsManagerControlType,
        VirtualCursorViewType);

    public static readonly FeatureManifest RotationSpeed = new(nameof(RotationSpeed),
        RotationApply,
        RotationXAxis,
        RotationYAxis,
        RotationControlsManager,
        ControlsManagerControlType);

    public static readonly FeatureManifest DropdownModes = new(nameof(DropdownModes),
        UiInputProcess,
        UiInputSendMove,
        UiInputSendSubmit,
        UiInputProcessMouseEvent,
        CursorUpdateImageSetActive,
        CursorImage,
        CursorPlayerMouse,
        DropdownShow,
        DropdownHide,
        DropdownOnSubmit,
        DropdownOnPointerClick,
        DropdownList,
        VirtualCursorViewType,
        new(UiInputModule, "RewiredPlayerIds"),
        new(UiInputModule, "HorizontalActionId"),
        new(UiInputModule, "VerticalActionId"),
        new(UiInputModule, "SubmitActionId"));

    public static readonly FeatureManifest SettingsRows = new(nameof(SettingsRows),
        SettingsMenuShow,
        SettingsMenuSoundSettingsView,
        SoundViewDialogueVolumeSlider,
        SettingsMenuMainView,
        MainViewMonitorDropdown,
        RestoryDropdownContainer);

    public static readonly FeatureManifest PartsBox = new(nameof(PartsBox),
        InventoryPanelConstruct,
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
        SocketNestedElement,
        SocketCompatibleElementInfo,
        InventoryPanelIsVisible,
        DeviceServicePlacedDevice,
        DeviceContainer,
        DeviceContainerDevice,
        WorkSurfacePlacedElements,
        ElementInfo,
        ElementDataCondition,
        InfoCategory,
        CategorySmall,
        DamagedCondition,
        QuestItem,
        new("Restory.UI.Presenters.Inventory.InventoryPanelFilter", "Sort"),
        new("Restory.Gameplay.Devices.Device", "SortedSockets"),
        new("Restory.Gameplay.Devices.DismantledDevicePack", "PlacedElements"),
        new("Restory.Gameplay.Elements.PlacedElements", "ElementsOnSurface"),
        new("Restory.Gameplay.Elements.ElementTransformRecord", "Element"),
        new("Restory.Gameplay.Elements.ElementData", "Info"),
        new("Restory.Data.Elements.Condition.BurntElementCondition"),
        new("Restory.Data.Elements.Condition.DirtyElementCondition"),
        new("Restory.Data.Elements.Condition.PerfectElementCondition"),
        new("Restory.StorageSystem.StorageElements.StorageItemElement", "ElementData"),
        new("Restory.StorageSystem.IReadOnlyStorageSlot", "Item"),
        new("Restory.StorageSystem.IReadOnlyStorageSlot", "Index"),
        new("Restory.StorageSystem.IReadOnlyStorageSlot", "Count"));

    public static readonly FeatureManifest PartsStore = new(nameof(PartsStore),
        ShopFilterSortFilteredItems,
        ShopFilterElementItems,
        ShopFilterFilteredElementItems,
        ShopFilterView,
        ShopFilterDevices,
        ShopFilterDeviceModels,
        ShopFilterDeviceDatabase,
        InfoCategory,
        CategorySmall,
        new("Restory.UI.Views.Shops.Elements.GUI_ElementsShopProductsPanelFilterView", "IsSortToggleOn"),
        new("Restory.UI.Views.Shops.Elements.GUI_ElementsShopProductsPanelFilterView", "SelectedModelIndex"),
        new("Restory.Data.Shops.Elements.ElementsShopItemData", "Element"),
        new("Restory.Data.Devices.DeviceInfoDatabase", "TryGetDeviceInfo"),
        new("Restory.Data.Devices.IDeviceInfo", "NameLocalizationKey"),
        new("Restory.Data.Devices.IDeviceInfo", "Elements"));

    public static readonly FeatureManifest PartsBoxDiagnostics = new(nameof(PartsBoxDiagnostics),
        InventoryPanelShow);

    public static readonly FeatureManifest NotepadKey = new(nameof(NotepadKey),
        NotepadActivatorInitialize,
        NotepadActivatorDispose,
        NotepadActivatorPlayerInput,
        NotepadActivatorWindow,
        NotepadActivatorItem,
        NotepadActivatorPauseMenu,
        NotepadWindowUpdateViewState,
        NotepadWindowHotkeyPressed,
        NotepadWindowPointerInside,
        NotepadWindowIsPointerInside,
        NotepadWindowIsSlidingMode,
        NotepadWindowItem,
        PauseMenuIsShown,
        new(NotepadWindow, "IsVisible"),
        new(NotepadWindow, "IsSlidingMode"),
        new("Restory.Gameplay.Equipment.NotepadInteractiveWorkplaceItem", "WindowIsPinned"),
        new(IPlayerInput, "AddInputEventDelegate"),
        new(IPlayerInput, "RemoveInputEventDelegate"));

    public static readonly FeatureManifest StickScroll = new(nameof(StickScroll),
        ScrollbarInputResolveScroll,
        CursorPlayerMouse,
        VirtualCursorViewType,
        new("RewiredConsts.Action+UI", "UIGamepadWheelHorizontal"),
        new("RewiredConsts.Action+UI", "UIGamepadWheelVertical"));

    public static readonly FeatureManifest ShoulderButtons = new(nameof(ShoulderButtons),
        DetectionWorkStateOnUpdate,
        DetectionWorkStatePlayerInput,
        DetectionWorkStateCameraDirectionSwitcher,
        DraggingWorkStateOnUpdate,
        DraggingWorkStatePlayerInput,
        DraggingWorkStateCameraDirectionSwitcher,
        CameraDirectionSwitcherBlockers,
        ToolWindowShow,
        ToolWindowIsShown,
        ToolWindowSelectionService,
        InventoryPanelShow,
        InventoryFilterCategoryBar,
        BrowserLaunchProcess,
        MarketplaceFilterCategoryBar,
        HomeDepotFilterCategoryBar,
        PartsFilterCategoryBar,
        MailLaunchProcess,
        MailStopProcess,
        MailClientFolders,
        MailFolderButtons,
        MailFolderButtonButton,
        InventoryPanelIsVisible,
        PauseMenuIsShown,
        new(IPlayerInput, "GetButtonDown"),
        new("Restory.Gameplay.GameView.CameraDirectionSwitcher", "CurrentDirection"),
        new("Restory.Gameplay.GameView.CameraDirectionSwitcher", "ApplyMainCameraDirection"),
        new("Restory.Gameplay.GameView.CameraDirection", "Left"),
        new("Restory.Gameplay.GameView.CameraDirection", "Main"),
        new("Restory.Gameplay.GameView.CameraDirection", "Right"),
        new("Restory.Gameplay.Common.ActiveStateSwitcher", "ShouldSystemBeActive"),
        new(ToolWindow, "CleaningTools"),
        new("Restory.UI.Presenters.CleaningToolsSelectionWindow.GUI_CleaningTool", "ToolInfo"),
        new("Restory.Gameplay.Equipment.CleaningToolSelectionService", "CurrentlySelectedTool"),
        new("Restory.Gameplay.Equipment.CleaningToolSelectionService", "TryToSelectTool"),
        new("Restory.UI.Presenters.GUI_WebBrowser", "IsVisible"),
        new("Restory.UI.Presenters.GUI_WebBrowser", "PageSwitcher"),
        new("Restory.UI.Presenters.GUI_WebBrowserPageSwitcher", "Tabs"),
        new("Restory.UI.Presenters.GUI_WebBrowserPageSwitcher", "CurrentTab"),
        new("Restory.UI.Presenters.GUI_WebBrowserPageSwitcher", "ResolveTabClick"),
        new("Restory.UI.Views.GUI_WebBrowserTabView", "BrowserPage"),
        new("Restory.UI.Presenters.GUI_MailClientFoldersPanel", "CurrentlySelectedFolder"),
        new("Restory.UI.Presenters.GUI_MailClientFolderButton", "EmailFolder"),
        new("Restory.UI.Views.ToggleButtonGroup", "Buttons"),
        new("Restory.UI.Views.ToggleButton", "IsSelected"));

    public static readonly FeatureManifest MailList = new(nameof(MailList),
        MailLaunchProcess,
        MailStopProcess,
        MailFolderChanged,
        MailContentsChanged,
        MailClientList,
        MailListEntries,
        MailListClick,
        MailListPairButton,
        MailListPairLetter,
        new(PatchTargets.MailList, "CurrentlySelectedLetter"),
        new("Restory.UI.Presenters.GUI_EmailMessageButton"));

    public static readonly FeatureManifest AppSwitch = new(nameof(AppSwitch),
        PcToolbarAppButtons,
        HackingAppState,
        PauseMenuIsShown,
        new(HackingApp + "+State", "Hacking"),
        new(HackingApp + "+State", "Delay"),
        new(HackingApp + "+State", "Decision"),
        new("Restory.UI.Presenters.GUI_PcWindowsXpScreen", "IsVisible"),
        new("Restory.UI.Presenters.GUI_PcWindowsXpScreen", "Toolbar"),
        new("Restory.UI.Presenters.GUI_PcWindowsXpScreen", "CurrentState"),
        new("Restory.UI.Presenters.PcScreenStates", "Desktop"),
        new("Restory.UI.Presenters.PcScreenStates", "InStartMenu"),
        new("Restory.UI.Presenters.GUI_PcAppToolbarButton", "AppInfo"),
        new("Restory.UI.Presenters.PC.Apps.GUI_PcAppBase", "AppInfo"),
        new("Restory.Gameplay.PC.PcAppManager", "LaunchedApp"),
        new("Restory.Gameplay.PC.PcAppManager", "LaunchPcApp"));

    public static readonly FeatureManifest PcClickSound = new(nameof(PcClickSound),
        PatchTargets.PcClickSound,
        PcClickAudioPlayer,
        new("Restory.Audio.IAudioPlayerService", "PlaySoundEventOneShot"));

    public static readonly FeatureManifest DpadSend = new(nameof(DpadSend),
        DraggingOnUpdate,
        DraggingRelease,
        DraggingCompleteDrag,
        DraggingPlayerInput,
        DraggingSelectedElement,
        DraggingIsRecycling,
        DraggingIsShredding,
        DraggingIsOverInventory,
        DraggingIsOverSonicBath,
        DraggingIsOverCleaner,
        DraggingGameMode,
        DraggingElementCleaner,
        DraggingUltrasonicService,
        DraggingRecycleService,
        DraggingShredderService,
        DraggingStorageDragService,
        DraggingWarningService,
        DraggingWarningDatabase,
        DragServiceState,
        UltrasonicSonicBath,
        RecycleTrashCan,
        ShredderServiceShredder,
        SlotOnPointerDown,
        SlotOnPointerMove,
        SlotIsHolding,
        DraggingWorkStateOnUpdate,
        DraggingWorkStatePlayerInput,
        WorkDraggingSelectedObject,
        WorkDraggingIsWaitingForConfirmation,
        WorkDraggingStateMachine,
        WorkDraggingResolver,
        ResolverCompleteDrag,
        ResolverRecycleService,
        ResolverShredderService,
        ResolverConsumers,
        ConsumersInventoryDetector,
        InventoryDetectorIsDetected,
        ElementConditionHandler,
        ConditionHandlerData,
        ElementDataCondition,
        DamagedCondition,
        QuestItem,
        SonicBath,
        DetectionWorkStateType,
        new(IPlayerInput, "Id"),
        new("Restory.Gameplay.Inventory.StorageElasticElementsDragService", "IsPointerOverInventory"),
        new("Restory.Gameplay.InteractiveObjects.InteractiveObject", "CompleteDrag"),
        new("Restory.Gameplay.GameDialogues.GameWarningService", "ShowWarning"),
        new("Restory.Data.GameWarnings.GameWarningDatabase", "BrokenElementWarning"),
        new("Restory.Gameplay.InteractiveObjects.TrashObject"),
        new("Restory.Gameplay.Elements.ElementsContainer"),
        new("Restory.Gameplay.Elements.ElementsBox"),
        new("Restory.Gameplay.Recycle.RecycleService", "TryToRecycleInteractiveObject"),
        new("Restory.Gameplay.Shredders.ShredderService", "TryToShredInteractiveObject"),
        new("Restory.Gameplay.Inventory.InteractiveObjectsToObjectConsumersDragHandlingService", "TryToDropDraggedObjectIntoInventory"),
        new("Restory.Infrastructure.StateMachines.StateMachineMonoBase", "Enter"),
        new("Restory.Gameplay.Disassemble.DisassembleGameMode", "IsInCompetition"),
        new("Restory.Gameplay.Equipment.ElementCleaner", "DraggingElementInitialCleaningData"),
        new("Restory.Gameplay.Equipment.Ultrasonic.UltrasonicService", "TryFitElementToSonicBath"),
        new("Restory.Gameplay.Equipment.Ultrasonic.SonicBath", "ElementFitter"),
        new(TrashCan, "IsActive"),
        new(TrashCan, "IsDetected"),
        new(TrashCan, "OnPointerEnter"),
        new(TrashCan, "OnPointerExit"),
        new(Shredder, "IsActive"),
        new(Shredder, "IsDetected"),
        new(Shredder, "OnPointerEnter"),
        new(Shredder, "OnPointerExit"));

    public static readonly FeatureManifest HoverGrace = new(nameof(HoverGrace),
        DisassembleDetectionOnUpdate,
        DisassembleDetectionPlayerInput,
        DisassembleDetectionSelection,
        DisassembleDetectionRotation,
        DisassembleDetectionWaiting,
        DisassembleDetectionElement,
        DisassembleDetectionProjection,
        DisassembleDetectionButton,
        DisassembleDetectionResetElement,
        DisassembleDetectionSetElement,
        DetectionWorkStateOnUpdate,
        DetectionWorkStatePlayerInput,
        WorkDetectionSelection,
        WorkDetectionObject,
        WorkDetectionSelectObject,
        WorkDetectionResetObject,
        WorkDetectionVisitsBlock,
        DetectionWorkStateType,
        ElementInfo,
        ElementInSocket,
        ElementIsBlocked,
        ElementIsActivated,
        InfoCategory,
        CategorySmall,
        ProjectionType,
        SwitchableElement,
        ElementButton,
        PlayerInputGetButton,
        SelectionDetectedObject,
        RotationButtonPressed,
        ActivateAction,
        new(IPlayerInput, "GetButtonUp"),
        new("Restory.Gameplay.GameCursor.CursorSelectionService", "SetDetection"),
        new(ElementProjection, "MakeBright"),
        new("Restory.Gameplay.InteractiveObjects.InteractiveObject", "IsInteractable"));

    public static readonly FeatureManifest HitboxOverlay = new(nameof(HitboxOverlay), [
        DisassembleDetectionOnUpdate,
        DisassembleDetectionPlayerInput,
        DisassembleDetectionSelection,
        DisassembleDetectionRotation,
        DisassembleDetectionWaiting,
        DisassembleDetectionElement,
        DisassembleDetectionProjection,
        DisassembleDetectionButton,
        DisassembleDetectionDetector,
        DisassembleDetectionDeviceService,
        DisassembleDetectionMask,
        DisassembleDetectionHits,
        EmptyDetectionOnUpdate,
        EmptyDetectionPlayerInput,
        EmptyDetectionDetector,
        EmptyDetectionSelection,
        EmptyDetectionDeviceService,
        EmptyDetectionMask,
        EmptyDetectionHits,
        EmptyDetectionElement,
        CursorDetectorCamera,
        PlayerInputGetButton,
        SelectionDetectedObject,
        RotationButtonPressed,
        ActivateAction,
        new("Restory.Gameplay.GameCursor.CursorDetectorService", "GameDetector"),
        new(IPlayerInput, "GetMousePosition"),
        .. WorkbenchListing,
    ]);

    public static readonly FeatureManifest HitboxEnlarging = new(nameof(HitboxEnlarging), [
        CursorDetectorTryToDetect,
        CursorDetectorCamera,
        DisassembleDetectionOnUpdate,
        DisassembleDetectionDeviceService,
        DisassembleDetectionMask,
        EmptyDetectionOnUpdate,
        EmptyDetectionDeviceService,
        EmptyDetectionMask,
        .. WorkbenchListing,
    ]);

    public static readonly FeatureManifest HintPulse = new(nameof(HintPulse),
        HighlighterProjections,
        HighlighterSequence,
        HighlighterToPeak,
        HighlighterToBase,
        HighlighterPeakScale,
        HighlighterSetScale,
        TooltipHighlighterCreate,
        TooltipHighlighterFactory,
        ProjectionFactoryDestroySmall,
        TooltipServiceSmallHandler,
        TooltipServiceSocketChanged,
        DropHintHighlighter,
        ProjectionType,
        ProjectionOutlinable,
        OutlinableOutlinable);

    public static readonly FeatureManifest DropHint = new(nameof(DropHint),
        DropHintHandleBlocked,
        DropHintDetachedSocket,
        DropHintHighlighter,
        TooltipHighlighterCreate,
        ElementProjectionData,
        SocketBlockedSockets,
        SocketIsBlocked,
        SocketNestedElement,
        SocketCompatibleElementInfo,
        ElementInfo,
        InfoCategory,
        CategorySmall);

    /// <summary>
    /// The game's tooltip service, kept for the hint and the R2 button. Shared, so it checks its own
    /// subsets: keeping the service, and reading the session's state.
    /// </summary>
    public static readonly FeatureManifest WorkbenchSession = new(nameof(WorkbenchSession),
        TooltipServiceInitialize,
        TooltipServiceDispose,
        TooltipServiceSmallHandler,
        DropHintHighlighter,
        TooltipServiceTargetDevice,
        TooltipServiceIsDisassembling,
        TooltipServiceIsActive,
        TooltipServiceStateMachine,
        new(DisassembleDetection));

    public static readonly FeatureManifest ScrewHintButton = new(nameof(ScrewHintButton),
        TooltipServiceShow);

    public static readonly FeatureManifest EveryPartHint = new(nameof(EveryPartHint),
        HintShowDisassembling,
        HintShowAssembling,
        HintHighlighter,
        ElementProjectionData,
        DeviceElementSockets,
        SocketIsBlocked,
        SocketNestedElement,
        SocketCompatibleElementInfo,
        SocketBlockers,
        SocketAdditionalBlockers,
        SocketBlockedSockets,
        SocketIsAvailable,
        SocketLastNestedElement,
        SocketScrewSlot,
        SerialSubordinates,
        SubordinateSocket,
        ElementDataIsInspected,
        ElementIsDragging,
        ElementIsOnSurface,
        ElementProgress,
        ElementInfoPrefab,
        FlexibleAttachedModel,
        FlexiblePlacementModel,
        BehaviorSwitcherCastCollider,
        HighlighterPreset,
        HighlighterFrontOutline,
        PresetRenderStyle,
        PresetCommon,
        PresetFront,
        PresetBack,
        TooltipHighlighterComplete,
        ElementInfo,
        ElementConditionHandler,
        ConditionHandlerData,
        ElementDataCondition,
        InfoCategory,
        CategorySmall,
        ProjectionType,
        ProjectionOutlinable,
        OutlinableOutlinable,
        OutlinableOverridePreset,
        new(ElementBase, "BehaviorSwitcher"),
        new("Restory.Constants.ProjectConstants+Layers", "DeviceMask"),
        new("Restory.Constants.ProjectConstants+Layers", "ElementsMask"));

    public static readonly FeatureManifest ZoomToggle = new(nameof(ZoomToggle),
        ZoomUpdateTarget,
        ZoomAxisId,
        ZoomTarget,
        PlayerInputGetAxisRaw);

    public static readonly FeatureManifest WorkbenchAdvice = new(nameof(WorkbenchAdvice),
        WorkSurfaceToggleAdvices,
        WorkSurfaceAdviceSign,
        new(InputActionElement, "ActionId"),
        new(InputActionElement, "AxisRange"),
        new(InputActionElement, "SetActionIdAndAxisRange"),
        new("Restory.UserInterface.GUI_LocalisedText"),
        new("Restory.UserInterface.CommonElements.GUI_ControlsTypeGameObjectsSwitcher"),
        new("Restory.Infrastructure.CommonServices.ControlsManager", "ControlType"));

    public static readonly FeatureManifest R2Tool = new(nameof(R2Tool),
        CleaningExecutePressed,
        CleaningExecuteReleased,
        PaintingExecutePressed,
        PaintingExecuteReleased,
        PaintingOnUpdate,
        PlayerInputGetButtonRaw,
        ActivateAction,
        TooltipServiceIsActive,
        TooltipServiceStateMachine,
        StateMachineActiveState,
        new(CleaningState),
        new(PaintingState));

    public static readonly FeatureManifest DeviceFade = new(nameof(DeviceFade),
        TooltipServiceTargetDevice,
        TooltipServiceIsActive,
        TooltipServiceStateMachine,
        StateMachineActiveState,
        ProjectionType,
        new(DisassembleDetection),
        new(DraggingState),
        new("Restory.Gameplay.Devices.Device"));

    public static readonly FeatureManifest SettingsMenuDump = new(nameof(SettingsMenuDump),
        SettingsMenuShow);

    public static readonly FeatureManifest GamepadBindingsDump = new(nameof(GamepadBindingsDump),
        CursorControlsTypeChanged);

    public static readonly FeatureManifest OutlineDiagnostics = new(nameof(OutlineDiagnostics),
        OutlineAdapterSetActive,
        OutlineAdapterSetPreset,
        ProjectionHighlighterPlay,
        DropHintHandleBlocked,
        DropHintDetachedSocket,
        SocketAdditionalBlockers,
        SocketNestedElement,
        SocketCompatibleElementInfo,
        ElementInSocket,
        ElementIsBlocked,
        ElementIsActivated,
        InfoCategory,
        CategorySmall,
        ProjectionType,
        OutlinableOverridePreset,
        new(ElementBase, "IsSelected"),
        new(ElementBase, "IsHighlighted"),
        new(OutlinableAdapter, "IsActive"));

    public static readonly FeatureManifest BlockedHover = new(nameof(BlockedHover),
        ElementViewResolveSelection,
        ElementViewElement,
        ElementViewOutline,
        ElementIsBlocked,
        SwitchableElement,
        new(ElementBase, "IsInstalling"),
        new(OutlinableAdapter, "IsActive"));

    public static IReadOnlyList<FeatureManifest> All { get; } = new[]
    {
        ButtonPrompts,
        PlayStationArt,
        CursorSpeed,
        RotationSpeed,
        DropdownModes,
        SettingsRows,
        PartsBox,
        PartsStore,
        PartsBoxDiagnostics,
        NotepadKey,
        StickScroll,
        ShoulderButtons,
        MailList,
        AppSwitch,
        PcClickSound,
        DpadSend,
        HoverGrace,
        HitboxOverlay,
        HitboxEnlarging,
        HintPulse,
        DropHint,
        WorkbenchSession,
        ScrewHintButton,
        EveryPartHint,
        ZoomToggle,
        WorkbenchAdvice,
        R2Tool,
        DeviceFade,
        SettingsMenuDump,
        GamepadBindingsDump,
        OutlineDiagnostics,
        BlockedHover,
    };
}
