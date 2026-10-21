using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.Core;

namespace RestoryMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;

    /// <summary>
    /// Undo steps for what features leave in the game, run on unload. Under ScriptEngine (scripts/dev.sh hot)
    /// unloading is a reload, and the new plugin copy should find the game as it was before the mod.
    /// </summary>
    internal static readonly Teardown Teardown = new();

    private Harmony harmony = null!;
    private FileSystemWatcher? configWatcher;
    private volatile bool configFileChanged;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        new FeatureLoader(harmony, Config, gameObject, Logger).Load(FeatureList.All, FeatureList.LoadedElsewhere);
        WatchConfigFile();
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
        foreach (var failure in Teardown.Run())
            Logger.LogWarning($"Teardown step failed: {failure}");
    }
}
