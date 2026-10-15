using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.Diagnostics;
using RestoryMod.Features.DpadSend;
using RestoryMod.Features.DropdownModes;
using RestoryMod.Features.Notepad;
using RestoryMod.Features.PartsBox;
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
        DropdownModesFeature.Bind(Config);
        SettingsMenuDumpPatch.Bind(Config);
        PartsBoxFeature.Bind(Config);
        PartsBoxDiagnostics.Bind(Config);
        NotepadKeyFeature.Bind(Config);
        ShoulderButtonsFeature.Bind(Config);
        DpadSendFeature.Bind(Config);
        GamepadBindingsDump.Bind(Config);
        StickScrollFeature.Bind(Config);
        WatchConfigFile();

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        FeaturePatcher.Apply(harmony, Logger,
            typeof(CursorSpeedPatch),
            typeof(CursorSpeedSliderPatch),
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
            typeof(StickScrollGameHandlerPatch),
            typeof(LookDirectionPatch),
            typeof(CleaningToolWindowCapturePatch),
            typeof(InventoryCapturePatch),
            typeof(BrowserCapturePatch),
            typeof(DpadSendPatch),
            typeof(DpadSendFallbackGuardPatch),
            typeof(DpadSendSlotPressPatch),
            typeof(SettingsMenuDumpPatch),
            typeof(GamepadBindingsDump));
        if (StickScrollFeature.TargetsPresent()) gameObject.AddComponent<StickScrollDriver>();
        gameObject.AddComponent<ShoulderButtonsPoller>();
        // Idle unless DpadSendSlotPressPatch applied: it is what records a pressed slot.
        gameObject.AddComponent<DpadSendPoller>();
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
