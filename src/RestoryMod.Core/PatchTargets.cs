using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>
/// A game type (and optionally a member on it) that the mod depends on.
/// </summary>
public sealed record PatchTarget(string TypeName, string? MemberName = null)
{
    public override string ToString() => MemberName is null ? TypeName : $"{TypeName}::{MemberName}";
}

/// <summary>
/// The game types/members the mod hooks into or reads by name. The plugin resolves these at runtime
/// with AccessTools; the TargetCheck tests resolve them against the installed game DLL after each
/// game update. Add a field here when a patch starts depending on something new by name, and list it
/// in the manifest of every feature that uses it (<see cref="FeatureManifests"/>).
/// </summary>
public static class PatchTargets
{
    public const string GameAssembly = "Restory.Assembly";

    internal const string VirtualCursorView = "Restory.UserInterface.VirtualCursorView";
    internal const string SettingsMenu = "Restory.UI.Presenters.SettingsMenu.GUI_SettingsMenu";
    internal const string SettingsMenuSoundView = "Restory.UI.Views.SettingsMenu.GUI_SettingsMenuSoundView";
    internal const string InventoryPanel = "Restory.UI.Presenters.Inventory.InventoryPanel";
    internal const string InventoryItemsView = "Restory.UI.Views.Inventory.InventoryItemsView";
    internal const string InventoryPanelViewType = "Restory.UI.Views.Inventory.InventoryPanelView";
    internal const string NotepadActivator = "Restory.Gameplay.OverlayActivators.NotepadActivator";
    internal const string NotepadWindow = "Restory.UI.Presenters.Notepad.GUI_NotepadWindow";
    internal const string UiInputModule = "UnityEngine.EventSystems.CustomRewiredStandaloneInputModule";
    internal const string ScrollbarInput = "Restory.UserInterface.CommonElements.GUI_ScrollbarInput";

    // Cursor speed: the game resets PlayerMouse.pointerSpeed in this handler on every controls-type change.
    public static readonly PatchTarget CursorControlsTypeChanged = new(VirtualCursorView, "ResolveControlsManagerOnControlsTypeChanged");
    public static readonly PatchTarget CursorPlayerMouse = new(VirtualCursorView, "playerMouse");
    public static readonly PatchTarget CursorControlsManager = new(VirtualCursorView, "controlsManager");

    // Rotation speed: the device's per-frame spin is scaled just before the game applies it.
    internal const string RotationController = "Restory.Gameplay.Disassemble.DisassembleRotationController";
    public static readonly PatchTarget RotationApply = new(RotationController, "ApplyRotation");
    public static readonly PatchTarget RotationXAxis = new(RotationController, "xAxisRotation");
    public static readonly PatchTarget RotationYAxis = new(RotationController, "yAxisRotation");
    public static readonly PatchTarget RotationControlsManager = new(RotationController, "controlsManager");

    // Dropdown modes: the UI input module sends gamepad moves, submits and virtual-cursor clicks;
    // each step is gated while a dropdown is open. The cursor image is hidden in nav mode.
    public static readonly PatchTarget UiInputProcess = new(UiInputModule, "Process");
    public static readonly PatchTarget UiInputSendMove = new(UiInputModule, "SendMoveEventToSelectedObject");
    public static readonly PatchTarget UiInputSendSubmit = new(UiInputModule, "SendSubmitEventToSelectedObject");
    public static readonly PatchTarget UiInputProcessMouseEvent = new(UiInputModule, "ProcessMouseEvent");
    public static readonly PatchTarget CursorUpdateImageSetActive = new(VirtualCursorView, "UpdateImageSetActive");
    public static readonly PatchTarget CursorImage = new(VirtualCursorView, "image");

    // TextMeshPro ships with the engine, not the game assembly, so these stay out of All (in Unchecked:
    // TargetCheck loads only the game assembly); the patches still check them in Prepare().
    internal const string TmpDropdown = "TMPro.TMP_Dropdown";
    public static readonly PatchTarget DropdownShow = new(TmpDropdown, "Show");
    public static readonly PatchTarget DropdownHide = new(TmpDropdown, "Hide");
    public static readonly PatchTarget DropdownOnSubmit = new(TmpDropdown, "OnSubmit");
    public static readonly PatchTarget DropdownOnPointerClick = new(TmpDropdown, "OnPointerClick");
    public static readonly PatchTarget DropdownList = new(TmpDropdown, "m_Dropdown");

    // Settings menu: rows are prefab-wired, so new rows are cloned from an existing slider on Show().
    public static readonly PatchTarget SettingsMenuShow = new(SettingsMenu, "Show");
    public static readonly PatchTarget SettingsMenuSoundSettingsView = new(SettingsMenu, "soundSettingsView");
    public static readonly PatchTarget SoundViewDialogueVolumeSlider = new(SettingsMenuSoundView, "dialogueVolumeSlider");
    // The "Button prompts" dropdown is cloned from the monitor row; the game's dropdown builds its list via Zenject.
    public static readonly PatchTarget SettingsMenuMainView = new(SettingsMenu, "mainView");
    public static readonly PatchTarget MainViewMonitorDropdown = new("Restory.UI.Views.SettingsMenu.GUI_SettingsMenuMainView", "monitorDropdown");
    public static readonly PatchTarget RestoryDropdownContainer = new("Restory.UI.Presenters.DevicePaintingTool.GUI_RestoryDropdown", "container");

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

    // Parts store: re-sorted after the game's price sort, by model and reassembly order.
    internal const string ShopFilter = "Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopProductsPanelFilter";
    public static readonly PatchTarget ShopFilterSortFilteredItems = new(ShopFilter, "SortFilteredItems");
    public static readonly PatchTarget ShopFilterElementItems = new(ShopFilter, "elementItems");
    public static readonly PatchTarget ShopFilterFilteredElementItems = new(ShopFilter, "filteredElementItems");
    public static readonly PatchTarget ShopFilterView = new(ShopFilter, "view");
    public static readonly PatchTarget ShopFilterDevices = new(ShopFilter, "devices");
    public static readonly PatchTarget ShopFilterDeviceModels = new(ShopFilter, "deviceModels");
    public static readonly PatchTarget ShopFilterDeviceDatabase = new(ShopFilter, "deviceDatabase");

    // Notepad key: listened for beside the game's own listener; the game's hotkeyPressed flag holds Open,
    // and pointerInside is masked after the key closes a hovered Notepad.
    public static readonly PatchTarget NotepadActivatorInitialize = new(NotepadActivator, "Initialize");
    public static readonly PatchTarget NotepadActivatorDispose = new(NotepadActivator, "Dispose");
    public static readonly PatchTarget NotepadActivatorPlayerInput = new(NotepadActivator, "playerInput");
    public static readonly PatchTarget NotepadActivatorWindow = new(NotepadActivator, "notepadWindow");
    public static readonly PatchTarget NotepadActivatorItem = new(NotepadActivator, "notepadInteractiveWorkplaceItem");
    public static readonly PatchTarget NotepadActivatorPauseMenu = new(NotepadActivator, "pauseMenu");
    public static readonly PatchTarget NotepadWindowUpdateViewState = new(NotepadWindow, "UpdateViewState");
    public static readonly PatchTarget NotepadWindowHotkeyPressed = new(NotepadWindow, "hotkeyPressed");
    public static readonly PatchTarget NotepadWindowPointerInside = new(NotepadWindow, "pointerInside");
    public static readonly PatchTarget NotepadWindowIsPointerInside = new(NotepadWindow, "IsPointerInside");
    public static readonly PatchTarget NotepadWindowIsSlidingMode = new(NotepadWindow, "isSlidingMode");
    public static readonly PatchTarget NotepadWindowItem = new(NotepadWindow, "notepadInteractiveWorkplaceItem");

    // Stick scroll: the game's step-and-repeat handler is silenced; the mod glides the view itself.
    public static readonly PatchTarget ScrollbarInputResolveScroll = new(ScrollbarInput, "ResolveActionScrollInputEvent");
    // Shoulder buttons: L2/R2 look from the game's away-from-workbench updates; L1/R1 and L2/R2 drive the
    // Tool window, the inventory's and browser's category bars and the browser tabs, captured as shown.
    internal const string DetectionWorkState = "Restory.Gameplay.Work.StateMachine.DetectionWorkState";
    internal const string DraggingWorkState = "Restory.Gameplay.Work.StateMachine.DraggingWorkState";
    internal const string ToolWindow = "Restory.UI.Presenters.CleaningToolsSelectionWindow.GUI_CleaningToolsSelectionWindow";
    public static readonly PatchTarget DetectionWorkStateOnUpdate = new(DetectionWorkState, "OnUpdate");
    public static readonly PatchTarget DetectionWorkStatePlayerInput = new(DetectionWorkState, "playerInput");
    public static readonly PatchTarget DetectionWorkStateCameraDirectionSwitcher = new(DetectionWorkState, "cameraDirectionSwitcher");
    public static readonly PatchTarget DraggingWorkStateOnUpdate = new(DraggingWorkState, "OnUpdate");
    public static readonly PatchTarget DraggingWorkStatePlayerInput = new(DraggingWorkState, "playerInput");
    public static readonly PatchTarget DraggingWorkStateCameraDirectionSwitcher = new(DraggingWorkState, "cameraDirectionSwitcher");
    public static readonly PatchTarget CameraDirectionSwitcherBlockers = new("Restory.Gameplay.GameView.CameraDirectionSwitcher", "mainActiveStateSwitcher");
    public static readonly PatchTarget ToolWindowShow = new(ToolWindow, "Show");
    public static readonly PatchTarget ToolWindowIsShown = new(ToolWindow, "isShown");
    public static readonly PatchTarget ToolWindowSelectionService = new(ToolWindow, "cleaningToolSelectionService");
    public static readonly PatchTarget InventoryFilterCategoryBar = new("Restory.UI.Views.Inventory.InventoryPanelFilterView", "deviceCategoriesToggleGroup");
    public static readonly PatchTarget BrowserLaunchProcess = new("Restory.UI.Presenters.GUI_WebBrowser", "LaunchProcess");
    public static readonly PatchTarget MarketplaceFilterCategoryBar = new("Restory.UI.Views.Shops.Devices.GUI_DeviceShopFilterView", "deviceCategoriesToggleGroup");
    public static readonly PatchTarget HomeDepotFilterCategoryBar = new("Restory.UI.Views.Shops.HomeDepot.GUI_DecorShopProductsPanelFilterView", "categoriesToggleGroup");
    public static readonly PatchTarget PartsFilterCategoryBar = new("Restory.UI.Views.Shops.Elements.GUI_ElementsShopProductsPanelFilterView", "deviceCategoriesToggleGroup");

    // Mail list: the D-pad steps the Selected mail by running the list's own click handler; the folder
    // switch and mail-service change handlers are followed to keep a mail selected. L1/R1 click the
    // mail folder buttons.
    internal const string MailClient = "Restory.UI.Presenters.GUI_MailClient";
    internal const string MailList = "Restory.UI.Presenters.GUI_MailClientEmailButtonsInFolderPanel";
    internal const string MailListPair = MailList + "+LetterAndButtonPair";
    public static readonly PatchTarget MailLaunchProcess = new(MailClient, "LaunchProcess");
    public static readonly PatchTarget MailStopProcess = new(MailClient, "StopProcess");
    public static readonly PatchTarget MailFolderChanged = new(MailClient, "ResolveFolderSelectionChanged");
    public static readonly PatchTarget MailContentsChanged = new(MailClient, "ResolveChangesInEmailServiceDetected");
    public static readonly PatchTarget MailClientList = new(MailClient, "messages");
    public static readonly PatchTarget MailClientFolders = new(MailClient, "folders");
    public static readonly PatchTarget MailListEntries = new(MailList, "emailLettersAndTheirButtons");
    public static readonly PatchTarget MailListClick = new(MailList, "ResolveEmailButtonClicked");
    public static readonly PatchTarget MailListPairButton = new(MailListPair, "Button");
    public static readonly PatchTarget MailListPairLetter = new(MailListPair, "Letter");
    public static readonly PatchTarget MailFolderButtons = new("Restory.UI.Presenters.GUI_MailClientFoldersPanel", "folderButtons");
    public static readonly PatchTarget MailFolderButtonButton = new("Restory.UI.Presenters.GUI_MailClientFolderButton", "button");

    // App switch: the D-pad opens the neighbouring taskbar button's PC app through the app manager,
    // as a click on that button would. The hacking app's private state says whether a hack runs.
    internal const string HackingApp = "Restory.UI.Presenters.PC.Apps.Hacking.GUI_DeviceHackingApp";
    public static readonly PatchTarget PcToolbarAppButtons = new("Restory.UI.Presenters.GUI_PcWindowsXpToolbar", "appButtons");
    public static readonly PatchTarget HackingAppState = new(HackingApp, "state");

    // PC click sound: a D-pad or shoulder-button step on the PC replays the screen's own mouse-click sound.
    internal const string PcScreenClickSfx = "Restory.UI.SFX.GUI_PcScreenClickSFX";
    public static readonly PatchTarget PcClickSound = new(PcScreenClickSfx, "mouseClickSound");
    public static readonly PatchTarget PcClickAudioPlayer = new(PcScreenClickSfx, "audioPlayer");

    // D-pad part send: after the drag state's per-frame update, the chosen destination's hover state is
    // set and the game's private release handler runs. Trash can and shredder hover via pointer events.
    internal const string DraggingState = "Restory.Gameplay.Disassemble.StateMachine.DraggingDisassembleState";
    public static readonly PatchTarget DraggingOnUpdate = new(DraggingState, "OnUpdate");
    public static readonly PatchTarget DraggingRelease = new(DraggingState, "ResolveButtonJustReleased");
    public static readonly PatchTarget DraggingCompleteDrag = new(DraggingState, "CompleteDrag");
    public static readonly PatchTarget DraggingPlayerInput = new(DraggingState, "playerInput");
    public static readonly PatchTarget DraggingSelectedElement = new(DraggingState, "selectedElement");
    public static readonly PatchTarget DraggingIsRecycling = new(DraggingState, "isRecycling");
    public static readonly PatchTarget DraggingIsShredding = new(DraggingState, "isShredding");
    public static readonly PatchTarget DraggingIsOverInventory = new(DraggingState, "isOverInventory");
    public static readonly PatchTarget DraggingIsOverSonicBath = new(DraggingState, "isOverSonicBath");
    public static readonly PatchTarget DraggingIsOverCleaner = new(DraggingState, "isOverCleaner");
    public static readonly PatchTarget DraggingGameMode = new(DraggingState, "disassembleGameMode");
    public static readonly PatchTarget DraggingElementCleaner = new(DraggingState, "elementCleaner");
    public static readonly PatchTarget DraggingUltrasonicService = new(DraggingState, "ultrasonicService");
    public static readonly PatchTarget DraggingRecycleService = new(DraggingState, "recycleService");
    public static readonly PatchTarget DraggingShredderService = new(DraggingState, "shredderService");
    public static readonly PatchTarget DraggingStorageDragService = new(DraggingState, "storageElasticElementsDragService");
    public static readonly PatchTarget DraggingWarningService = new(DraggingState, "gameWarningService");
    public static readonly PatchTarget DraggingWarningDatabase = new(DraggingState, "gameWarningDatabase");
    // A held parts box slot: the game starts the drag on the first cursor move while it is held.
    internal const string StorageSlot = "Restory.UI.Presenters.Inventory.StorageSlotElements.StorageSlotElement";
    public static readonly PatchTarget SlotOnPointerDown = new(StorageSlot, "OnPointerDown");
    public static readonly PatchTarget SlotOnPointerMove = new(StorageSlot, "OnPointerMove");
    public static readonly PatchTarget SlotIsHolding = new(StorageSlot, "isHolding");
    // D-pad send away from the workbench: after the work drag state's update, the matching branch of
    // the game's drop resolver runs (destination's drop call, then the resolver's private CompleteDrag).
    internal const string WorkDraggingState = "Restory.Gameplay.Work.StateMachine.DraggingWorkState";
    internal const string Resolver = "Restory.Gameplay.Work.Dragging.DragResolver";
    public static readonly PatchTarget WorkDraggingSelectedObject = new(WorkDraggingState, "selectedObject");
    public static readonly PatchTarget WorkDraggingIsWaitingForConfirmation = new(WorkDraggingState, "isWaitingForConfirmation");
    public static readonly PatchTarget WorkDraggingStateMachine = new(WorkDraggingState, "stateMachine");
    public static readonly PatchTarget WorkDraggingResolver = new(WorkDraggingState, "dragResolver");
    public static readonly PatchTarget ResolverCompleteDrag = new(Resolver, "CompleteDrag");
    public static readonly PatchTarget ResolverRecycleService = new(Resolver, "recycleService");
    public static readonly PatchTarget ResolverShredderService = new(Resolver, "shredderService");
    public static readonly PatchTarget ResolverConsumers = new(Resolver, "dragToObjectConsumersHandler");
    public static readonly PatchTarget ConsumersInventoryDetector = new("Restory.Gameplay.Inventory.InteractiveObjectsToObjectConsumersDragHandlingService", "inventoryDetector");
    public static readonly PatchTarget InventoryDetectorIsDetected = new("Restory.Gameplay.Equipment.InventoryBoxDetector", "IsDetected");
    public static readonly PatchTarget UltrasonicSonicBath = new("Restory.Gameplay.Equipment.Ultrasonic.UltrasonicService", "sonicBath");
    public static readonly PatchTarget RecycleTrashCan = new("Restory.Gameplay.Recycle.RecycleService", "trashCan");
    public static readonly PatchTarget ShredderServiceShredder = new("Restory.Gameplay.Shredders.ShredderService", "shredder");

    // Hover grace: after each hover state's per-frame detection, the lingering thing is put back in its
    // stored selection, which the Activate press acts on.
    internal const string DisassembleDetection = "Restory.Gameplay.Disassemble.StateMachine.DetectionDisassembleState";
    public static readonly PatchTarget DisassembleDetectionOnUpdate = new(DisassembleDetection, "OnUpdate");
    public static readonly PatchTarget DisassembleDetectionPlayerInput = new(DisassembleDetection, "playerInput");
    public static readonly PatchTarget DisassembleDetectionSelection = new(DisassembleDetection, "cursorSelectionService");
    public static readonly PatchTarget DisassembleDetectionRotation = new(DisassembleDetection, "rotationController");
    public static readonly PatchTarget DisassembleDetectionWaiting = new(DisassembleDetection, "isWaitingForConfirmation");
    public static readonly PatchTarget DisassembleDetectionElement = new(DisassembleDetection, "selectedElement");
    public static readonly PatchTarget DisassembleDetectionProjection = new(DisassembleDetection, "selectedProjection");
    public static readonly PatchTarget DisassembleDetectionButton = new(DisassembleDetection, "selectedButton");
    public static readonly PatchTarget DisassembleDetectionResetElement = new(DisassembleDetection, "ResetSelectedElement");
    public static readonly PatchTarget DisassembleDetectionSetElement = new(DisassembleDetection, "SetSelectedElement");
    public static readonly PatchTarget WorkDetectionSelection = new(DetectionWorkState, "cursorSelectionService");
    public static readonly PatchTarget WorkDetectionObject = new(DetectionWorkState, "selectedObject");
    public static readonly PatchTarget WorkDetectionSelectObject = new(DetectionWorkState, "SelectDetectedObject");
    public static readonly PatchTarget WorkDetectionResetObject = new(DetectionWorkState, "ResetSelectedObject");
    public static readonly PatchTarget WorkDetectionVisitsBlock = new(DetectionWorkState, "DoesVisitsStateBlockDetection");

    // Drop hint: after the game's pick every screw the dropped part freed gets a ghost. The hint pulse
    // (highlighter) members: one ghost per screw, a fading pulse rises again when screws join it, R2
    // holds it at its peak, and a screw going in or out takes its ghost away.
    internal const string DropHint = "Restory.Gameplay.Disassemble.Tooltips.SmallElementTooltipHandler";
    internal const string TooltipHighlighter = "Restory.Gameplay.Elements.TooltipProjectionHighlighter";
    public static readonly PatchTarget DropHintHandleBlocked = new(DropHint, "HandleBlockedSockets");
    public static readonly PatchTarget DropHintDetachedSocket = new(DropHint, "detachedSocket");
    public static readonly PatchTarget DropHintHighlighter = new(DropHint, "tooltipProjectionHighlighter");
    public static readonly PatchTarget HighlighterProjections = new("Restory.Gameplay.Elements.ProjectionHighlighterBase", "highlightedProjections");
    public static readonly PatchTarget TooltipHighlighterCreate = new(TooltipHighlighter, "CreateAndHighlightProjection");
    internal const string Highlighter = "Restory.Gameplay.Elements.ProjectionHighlighterBase";
    public static readonly PatchTarget HighlighterSequence = new(Highlighter, "transitionSequence");
    public static readonly PatchTarget HighlighterToPeak = new(Highlighter, "toPeakDuration");
    public static readonly PatchTarget HighlighterToBase = new(Highlighter, "toBaseDuration");
    public static readonly PatchTarget HighlighterPeakScale = new(Highlighter, "peakScale");
    public static readonly PatchTarget HighlighterSetScale = new(Highlighter, "SetHighlightedProjectionsScale");
    internal const string Socket = "Restory.Gameplay.Elements.ElementSocket";
    public static readonly PatchTarget SocketBlockedSockets = new(Socket, "BlockedSockets");
    public static readonly PatchTarget SocketIsBlocked = new(Socket, "IsBlocked");
    public static readonly PatchTarget SocketNestedElement = new(Socket, "NestedElement");
    public static readonly PatchTarget SocketCompatibleElementInfo = new(Socket, "CompatibleElementInfo");
    public static readonly PatchTarget ElementProjectionData = new("Restory.Gameplay.Elements.ElementBase", "ProjectionData");

    // Screw hint button: R2 at the workbench runs the game's own hint (its GetTooltip action, which
    // has no gamepad button), reaching it and its highlighter through the game's tooltip service.
    internal const string TooltipService = "Restory.Gameplay.Disassemble.Tooltips.DisassembleTooltipService";
    public static readonly PatchTarget TooltipServiceInitialize = new(TooltipService, "Initialize");
    public static readonly PatchTarget TooltipServiceDispose = new(TooltipService, "Dispose");
    public static readonly PatchTarget TooltipServiceShow = new(TooltipService, "ResolveGetTooltipButtonPressed");
    public static readonly PatchTarget TooltipServiceSmallHandler = new(TooltipService, "smallElementTooltipHandler");
    // A screw going in or out takes its own ghost away, so a hint never lingers on a changed slot.
    public static readonly PatchTarget TooltipServiceSocketChanged = new(TooltipService, "ResolveDeviceSocketChanged");
    public static readonly PatchTarget TooltipHighlighterFactory = new(TooltipHighlighter, "elementProjectionFactory");
    public static readonly PatchTarget ProjectionFactoryDestroySmall = new("Restory.Gameplay.Elements.ElementProjectionFactory", "DestroySmallElementProjection");

    // Every-part hint: the game's hint shows every ready part instead of only the next part's screws.
    // Taking apart walks each target's blockers (the serialized list and a flip lid's runtime set) and
    // never reads an unknown part's condition; putting back ghosts loose parts on the bench and, in red,
    // sockets no loose part fills. Ghosts leave when their part stops being ready.
    internal const string SimpleHint = "Restory.Gameplay.Disassemble.Tooltips.SimpleDisassembleTooltipHandler";
    public static readonly PatchTarget HintShowDisassembling = new(SimpleHint, "ShowDisassemblingTooltip");
    public static readonly PatchTarget HintShowAssembling = new(SimpleHint, "ShowAssemblingTooltip");
    public static readonly PatchTarget HintHighlighter = new(SimpleHint, "projectionHighlighter");
    public static readonly PatchTarget DeviceElementSockets = new("Restory.Gameplay.Devices.Device", "ElementSockets");
    public static readonly PatchTarget SocketIsAvailable = new(Socket, "IsAvailable");
    public static readonly PatchTarget SocketLastNestedElement = new(Socket, "LastNestedElement");
    public static readonly PatchTarget SocketBlockers = new(Socket, "Blockers");
    public static readonly PatchTarget ElementDataIsInspected = new("Restory.Gameplay.Elements.ElementData", "IsInspected");
    public static readonly PatchTarget ElementIsDragging = new("Restory.Gameplay.Elements.ElementBase", "IsDragging");
    public static readonly PatchTarget ElementIsOnSurface = new("Restory.Gameplay.Elements.ElementBase", "IsOnSurface");
    public static readonly PatchTarget ElementInfoPrefab = new("Restory.Data.Elements.ElementInfo", "Prefab");
    internal const string FlexibleElement = "Restory.Gameplay.Elements.FlexibleElement";
    public static readonly PatchTarget FlexibleAttachedModel = new(FlexibleElement, "AttachedModel");
    public static readonly PatchTarget FlexiblePlacementModel = new(FlexibleElement, "PlacementModel");
    public static readonly PatchTarget BehaviorSwitcherCastCollider = new("Restory.Gameplay.Elements.ElementBehaviorSwitcher", "CastCollider");
    public static readonly PatchTarget HighlighterPreset = new(Highlighter, "outlinePresetInstance");
    public static readonly PatchTarget HighlighterFrontOutline = new(Highlighter, "frontOutlineProperties");
    public static readonly PatchTarget HighlighterBackOutline = new(Highlighter, "backOutlineProperties");
    public static readonly PatchTarget TooltipHighlighterComplete = new(TooltipHighlighter, "CompleteTransitionSequence");
    // Tubes (batteries): a serial socket's entry plus the places behind it, which take parts through it.
    public static readonly PatchTarget SerialSubordinates = new("Restory.Gameplay.Elements.SerialElementSocket", "subordinateSockets");
    public static readonly PatchTarget SubordinateSocket = new("Restory.Gameplay.Elements.SubordinateElementSocket");
    public static readonly PatchTarget ElementProgress = new("Restory.Gameplay.Elements.ElementBase", "Progress");
    // Holding R2 checks again for ready parts, as the game's hint button would, without its other hints.
    public static readonly PatchTarget TooltipServiceTargetDevice = new(TooltipService, "targetDevice");
    public static readonly PatchTarget TooltipServiceIsDisassembling = new(TooltipService, "isDisassembling");
    public static readonly PatchTarget TooltipServiceIsActive = new(TooltipService, "IsActive");
    public static readonly PatchTarget TooltipServiceStateMachine = new(TooltipService, "disassembleStateMachine");

    // Zoom toggle: an L2 press at the workbench becomes a full zoom input to the game's zoom update,
    // and the triggers' own zoom input is muted there.
    internal const string ZoomController = "Restory.Gameplay.Devices.DeviceZoomController";
    public static readonly PatchTarget ZoomUpdateTarget = new(ZoomController, "UpdateTargetZoom");
    public static readonly PatchTarget ZoomAxisId = new(ZoomController, "zoomAxisId");
    public static readonly PatchTarget ZoomTarget = new(ZoomController, "targetZoom");
    public static readonly PatchTarget PlayerInputGetAxisRaw = new("Restory.Gameplay.PlayerInput.RewiredPlayerInput", "GetAxisRaw");

    // Workbench advice: the controls sign at the workbench, set up when the game first shows it. Its zoom
    // prompts are found by action and axis range; our rows are clones of its rotation row.
    internal const string WorkSurface = "Restory.Gameplay.Workplace.WorkSurface";
    public static readonly PatchTarget WorkSurfaceToggleAdvices = new(WorkSurface, "ToggleDisassembleControlsAdvices");
    public static readonly PatchTarget WorkSurfaceAdviceSign = new(WorkSurface, "disassembleControlsAdviceSign");

    // R2 works the tool while Cleaning or painting, as Cross does: R2's press and release go to each
    // state's own handlers for Cross, a Cross release waits while R2 is held, and painting's per-frame
    // read of Cross sees it held.
    internal const string CleaningState = "Restory.Gameplay.Disassemble.StateMachine.CleaningDisassembleState";
    internal const string PaintingState = "Restory.Gameplay.Disassemble.StateMachine.PaintingDisassembleState";
    public static readonly PatchTarget CleaningExecutePressed = new(CleaningState, "ResolveButtonJustPressed");
    public static readonly PatchTarget CleaningExecuteReleased = new(CleaningState, "ResolveButtonJustReleased");
    public static readonly PatchTarget PaintingExecutePressed = new(PaintingState, "ResolveButtonJustPressed");
    public static readonly PatchTarget PaintingExecuteReleased = new(PaintingState, "ResolveButtonJustReleased");
    public static readonly PatchTarget PaintingOnUpdate = new(PaintingState, "OnUpdate");
    public static readonly PatchTarget StateMachineActiveState = new("Restory.Infrastructure.StateMachines.StateMachineMonoBase", "ActiveState");
    public static readonly PatchTarget PlayerInputGetButtonRaw = new("Restory.Gameplay.PlayerInput.RewiredPlayerInput", "GetButton");

    // Outline diagnostics ([Debug] OutlineDiagnostics): every outline switch and preset change on
    // parts, screws and slots, the slot pulse, and the socket graph the drop hint reads.
    internal const string OutlinableAdapter = "Restory.Gameplay.Common.OutlinableAdapter";
    public static readonly PatchTarget OutlineAdapterSetActive = new(OutlinableAdapter, "set_IsActive");
    public static readonly PatchTarget OutlineAdapterSetPreset = new(OutlinableAdapter, "set_OverridePreset");
    public static readonly PatchTarget ProjectionHighlighterPlay = new("Restory.Gameplay.Elements.ProjectionHighlighterBase", "PlaySequence");
    public static readonly PatchTarget SocketAdditionalBlockers = new("Restory.Gameplay.Elements.ElementSocket", "additionalBlockers");
    // Hitbox overlay: both workbench hover states are read after their per-frame detection (what the
    // game hovers, its layer mask and cursor); the game camera comes from the cursor detector, and a
    // socket's own screw slot tells it apart from a part ghost.
    internal const string EmptyDetection = "Restory.Gameplay.Disassemble.StateMachine.EmptyDisassembleState";
    public static readonly PatchTarget DisassembleDetectionDetector = new(DisassembleDetection, "cursorDetectorService");
    public static readonly PatchTarget DisassembleDetectionMask = new(DisassembleDetection, "detectionLayerMask");
    public static readonly PatchTarget DisassembleDetectionHits = new(DisassembleDetection, "raycastHits");
    public static readonly PatchTarget EmptyDetectionOnUpdate = new(EmptyDetection, "OnUpdate");
    public static readonly PatchTarget EmptyDetectionPlayerInput = new(EmptyDetection, "playerInput");
    public static readonly PatchTarget EmptyDetectionDetector = new(EmptyDetection, "cursorDetectorService");
    public static readonly PatchTarget EmptyDetectionSelection = new(EmptyDetection, "cursorSelectionService");
    public static readonly PatchTarget EmptyDetectionMask = new(EmptyDetection, "detectionLayerMask");
    public static readonly PatchTarget EmptyDetectionHits = new(EmptyDetection, "raycastHits");
    public static readonly PatchTarget EmptyDetectionElement = new(EmptyDetection, "selectedElement");
    public static readonly PatchTarget CursorDetectorCamera = new("Restory.Gameplay.GameCursor.GameCursorDetector", "gameCamera");
    public static readonly PatchTarget SocketScrewSlot = new(Socket, "smallElementProjection");

    // Minimum hitbox size: during the workbench hover states' detection, the game's hover ray reports
    // the enlarged hitbox that wins instead of what it hit. Things are listed from the placed device
    // and the loose parts on the bench.
    public static readonly PatchTarget CursorDetectorTryToDetect = new("Restory.Gameplay.GameCursor.GameCursorDetector", "TryToDetect");

    // The end-to-end harness keeps every save file as it was: each member here writes, moves or deletes
    // files in the save folder.
    private const string GlobalSaves = "Restory.Data.ReadWriteServices.GlobalReadWriteDataService";
    private const string GameplaySaves = "Restory.Data.ReadWriteServices.GameplayReadWriteCombinedDataService";
    public static readonly IReadOnlyList<PatchTarget> SaveWrites = new[]
    {
        new PatchTarget(GlobalSaves, "WriteToFileAsync"),
        new PatchTarget(GlobalSaves, "SetFileAsCorrupted"),
        new PatchTarget(GlobalSaves, "DeleteAll"),
        new PatchTarget(GlobalSaves, "DeleteFile"),
        new PatchTarget(GlobalSaves, "BackupSaveDataDirectory"),
        new PatchTarget(GlobalSaves, "BackupSlot"),
        new PatchTarget(GameplaySaves, "WriteToFileAsync"),
        new PatchTarget(GameplaySaves, "SetFileAsCorrupted"),
        new PatchTarget(GameplaySaves, "ReduceSaveRecordsCount"),
        new PatchTarget(GameplaySaves, "DeleteAll"),
        new PatchTarget(GameplaySaves, "DeleteFile"),
    };
    public static readonly PatchTarget DisassembleDetectionDeviceService = new(DisassembleDetection, "deviceService");
    public static readonly PatchTarget EmptyDetectionDeviceService = new(EmptyDetection, "deviceService");
    public static readonly PatchTarget DeviceServiceWorkSurface = new("Restory.Gameplay.Devices.DeviceService", "workSurface");

    // Button prompts: every prompt picks its glyph set in this one method; the patch swaps the pick for
    // the pad Steam Input hides from Rewired, or for the "Button prompts" setting.
    internal const string InputActionElement = "Restory.UI.Presenters.Controllers.GUI_InputActionElement";
    public static readonly PatchTarget PromptGetControllerId = new(InputActionElement, "TryGetControllerId");
    public static readonly PatchTarget PromptUpdateData = new(InputActionElement, "UpdateData");
    public static readonly PatchTarget PromptControllerIdsList = new(InputActionElement, "controllerIdsList");
    public static readonly PatchTarget PromptControllerIdsMap = new(InputActionElement, "rewiredControllerIdsDependencyMap");
    public static readonly PatchTarget PromptGameSettings = new(InputActionElement, "gameSettingsManager");
    public static readonly PatchTarget ControllerIdsMapLookup = new("IAmFuture.Data.Controllers.RewiredControllerIdsDependencyMap", "TryGetControllerId");
    public static readonly PatchTarget ControllerIdsLookup = new("IAmFuture.Data.Controllers.ControllerIdsList", "TryGetControllerId");
    public static readonly PatchTarget ControllerIdsDefaultGamepad = new("IAmFuture.Data.Controllers.ControllerIdsList", "DefaultGamepadId");
    public static readonly PatchTarget GameSettingsGamepadScheme = new("Restory.Gameplay.GameSettings.GameSettingsManager", "GamepadScheme");
    // PlayStation art: the prompt image is swapped for one of the game's unused PS5 sprites after it draws.
    internal const string PromptImageView = "Restory.UI.Views.Controllers.GUI_InputActionElementImageView";
    internal const string PromptView = "Restory.UI.Views.Controllers.GUI_InputActionElementView";
    public static readonly PatchTarget PromptImageUpdateView = new(PromptImageView, "UpdateView");
    public static readonly PatchTarget PromptImageTarget = new(PromptImageView, "targetImage");
    public static readonly PatchTarget PromptViewControllerId = new(PromptView, "controllerId");
    public static readonly PatchTarget PromptViewElementId = new(PromptView, "elementId");

    /// <summary>Named targets TargetCheck can't resolve, each with the reason.</summary>
    public static IReadOnlyList<PatchTarget> Unchecked { get; } = new[]
    {
        // TextMeshPro, not the game assembly.
        DropdownShow,
        DropdownHide,
        DropdownOnSubmit,
        DropdownOnPointerClick,
        DropdownList,
    };

    /// <summary>
    /// Every target TargetCheck resolves: the union of the feature manifests, less <see cref="Unchecked"/>.
    /// </summary>
    public static IReadOnlyList<PatchTarget> All => all ??= FeatureManifests.All
        .SelectMany(m => m.Targets)
        .Distinct()
        .Except(Unchecked)
        .ToList();

    private static IReadOnlyList<PatchTarget>? all;
}
