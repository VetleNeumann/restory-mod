using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.Features.ButtonPrompts;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.Diagnostics;
using RestoryMod.Features.DpadSend;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.DropdownModes;
using RestoryMod.Features.Hitboxes;
using RestoryMod.Features.HoverGrace;
using RestoryMod.Features.MailList;
using RestoryMod.Features.Notepad;
using RestoryMod.Features.PartsBox;
using RestoryMod.Features.RotationSpeed;
using RestoryMod.Features.Settings;
using RestoryMod.Features.ShoulderButtons;
using RestoryMod.Features.StickScroll;

namespace RestoryMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;

    private Harmony harmony = null!;
    private FileSystemWatcher? configWatcher;
    private volatile bool configFileChanged;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        CursorSpeedFeature.Bind(Config);
        RotationSpeedFeature.Bind(Config);
        DropdownModesFeature.Bind(Config);
        SettingsMenuDumpPatch.Bind(Config);
        PartsBoxFeature.Bind(Config);
        PartsBoxDiagnostics.Bind(Config);
        NotepadKeyFeature.Bind(Config);
        ShoulderButtonsFeature.Bind(Config);
        DpadSendFeature.Bind(Config);
        GamepadBindingsDump.Bind(Config);
        OutlineDiagnostics.Bind(Config);
        StickScrollFeature.Bind(Config);
        HoverGraceFeature.Bind(Config);
        HitboxEnlargingFeature.Bind(Config);
        HitboxOverlayFeature.Bind(Config);
        MailListFeature.Bind(Config);
        DropHintFeature.Bind(Config);
        ButtonPromptsFeature.Bind(Config);
        WatchConfigFile();

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        FeaturePatcher.Apply(harmony, Logger,
            typeof(CursorSpeedPatch),
            typeof(RotationSpeedPatch),
            typeof(SettingsRowsPatch),
            typeof(DropdownOpenPatch),
            typeof(DropdownFramePatch),
            typeof(DropdownStickMovePatch),
            typeof(DropdownSubmitPatch),
            typeof(DropdownCursorClickPatch),
            typeof(DropdownCursorImagePatch),
            typeof(PartsBoxSortPatch),
            typeof(PartsBoxHeadersPatch),
            typeof(PartsBoxShowTimingPatch),
            typeof(PartsBoxPrewarmPatch),
            typeof(NotepadKeyListenPatch),
            typeof(NotepadKeyUnlistenPatch),
            typeof(NotepadKeyHoldOpenPatch),
            typeof(NotepadKeyIgnoreHoverPatch),
            typeof(NotepadKeyIgnoreHoverGetterPatch),
            typeof(StickScrollGameHandlerPatch),
            typeof(LookDirectionPatch),
            typeof(CleaningToolWindowCapturePatch),
            typeof(InventoryCapturePatch),
            typeof(BrowserCapturePatch),
            typeof(DpadSendPatch),
            typeof(DpadSendFallbackGuardPatch),
            typeof(DpadSendSlotPressPatch),
            typeof(DpadSendCarryPatch),
            typeof(HoverGraceDisassemblePatch),
            typeof(HoverGraceWorkPatch),
            typeof(HoverGraceWorkResetPatch),
            typeof(HoverGraceWorkSelectPatch),
            typeof(HitboxOverlayDetectionPatch),
            typeof(HitboxOverlayEmptyBenchPatch),
            typeof(HitboxEnlargingDetectionPatch),
            typeof(HitboxEnlargingEmptyBenchPatch),
            typeof(HitboxEnlargingRayPatch),
            typeof(MailLaunchPatch),
            typeof(MailStopPatch),
            typeof(MailSelectedPatch),
            typeof(MailFolderChangedPatch),
            typeof(MailContentsChangedPatch),
            typeof(DropHintOneGhostPatch),
            typeof(DropHintFreedScrewsPatch),
            typeof(SettingsMenuDumpPatch),
            typeof(GamepadBindingsDump),
            typeof(OutlineAdapterActivePatch),
            typeof(OutlineAdapterPresetPatch),
            typeof(ProjectionPulsePatch),
            typeof(DropHintGraphPatch),
            typeof(ButtonPromptsPatch));
        if (StickScrollFeature.TargetsPresent()) gameObject.AddComponent<StickScrollDriver>();
        gameObject.AddComponent<ShoulderButtonsPoller>();
        if (ButtonPromptsFeature.TargetsPresent()) gameObject.AddComponent<ButtonPromptsPoller>();
        // Idle unless DpadSendSlotPressPatch applied: it is what records a pressed slot.
        gameObject.AddComponent<DpadSendPoller>();
        // Idle unless the mail list targets resolve and MailLaunchPatch has captured the Mail app.
        gameObject.AddComponent<MailListPoller>();
        if (OutlineDiagnostics.Enabled) gameObject.AddComponent<OutlineWatcher>();
        // Idle unless ShowHitboxes is on and a workbench hover state recorded this frame.
        gameObject.AddComponent<HitboxOverlay>();
    }

    /// <summary>
    /// BepInEx does not reload config files on its own. Watch ours so hand edits apply live.
    /// The watcher fires on a worker thread; the reload happens in Update on the main thread.
    /// Our own saves also trigger it, which is harmless: unchanged values raise no events.
    /// </summary>
    private void WatchConfigFile()
    {
        var path = Config.ConfigFilePath;
        configWatcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        configWatcher.Changed += (_, _) => configFileChanged = true;
        configWatcher.EnableRaisingEvents = true;
    }

    private void Update()
    {
        if (!configFileChanged) return;
        configFileChanged = false;

        try
        {
            Config.Reload();
            Logger.LogInfo("Config reloaded from disk.");
        }
        catch (IOException e)
        {
            // Editor still holds the file; try again next frame.
            configFileChanged = true;
            Logger.LogDebug($"Config reload deferred: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        configWatcher?.Dispose();
        harmony?.UnpatchSelf();
    }
}
