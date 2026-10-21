using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Restory.Data.Devices;
using Restory.Data.Elements;
using Restory.Data.Elements.Condition;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.Soldering;
using Restory.Gameplay.Work.StateMachine;
using Restory.Gameplay.WorkOrders.EmailOrders;
using Restory.Gameplay.Workplace;
using Restory.Infrastructure.StateMachine;
using Restory.Data.SaveLoad;
using Restory.UI.Presenters.MainMenu;
using RestoryMod.Features.DropHint;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace RestoryMod.E2E;

/// <summary>A step the harness could not carry out: the run ends there (a hard failure).</summary>
internal sealed class StepFailed : Exception
{
    public StepFailed(string message) : base(message) { }
}

/// <summary>The condition a part of a spawned device starts in.</summary>
internal enum Condition
{
    Perfect,
    Dirty,
    Damaged,
}

/// <summary>
/// Setup the harness does by calling the game directly rather than by input: loading the save, putting a
/// chosen device on the bench, spare parts in the parts box, finishing Cleaning. None of it is under test.
/// </summary>
internal static class Game
{
    /// <summary>Finds a game service in any loaded scene's container, or throws.</summary>
    public static T Resolve<T>() where T : class
    {
        foreach (var context in Object.FindObjectsByType<SceneContext>(FindObjectsSortMode.None))
        {
            if (context.Container?.TryResolve<T>() is { } found) return found;
        }
        if (ProjectContext.HasInstance && ProjectContext.Instance.Container.TryResolve<T>() is { } project) return project;
        throw new StepFailed($"no {typeof(T).Name} in any scene container");
    }

    private static T Find<T>() where T : Object =>
        Object.FindAnyObjectByType<T>() is { } found && found ? found : throw new StepFailed($"no {typeof(T).Name} in the scene");

    /// <summary>Waits until <paramref name="done"/> holds, or fails the step after <paramref name="seconds"/>.</summary>
    public static IEnumerator Until(Func<bool> done, float seconds, string what)
    {
        var end = Time.unscaledTime + seconds;
        while (!Safe(done))
        {
            if (Time.unscaledTime > end) throw new StepFailed($"{what} within {seconds:0}s");
            yield return null;
        }
    }

    private static bool Safe(Func<bool> done)
    {
        try
        {
            return done();
        }
        catch (StepFailed)
        {
            return false;
        }
    }

    private static bool InRoom() =>
        Resolve<GlobalStateObserver>().IsInGameLoop && Find<WorkStateMachine>().ActiveState is DetectionWorkState;

    /// <summary>From the main menu (or already in the game) to standing in the room, free to look around.</summary>
    public static IEnumerator LoadSave()
    {
        if (Safe(InRoom)) yield break;
        yield return Until(() => Object.FindAnyObjectByType<GUI_MainMenu>() is { } menu && menu && menu.ContinueButtonVisibility,
            60f, "the main menu offered Continue");
        yield return new WaitForSecondsRealtime(1f);
        // What the main menu's Continue button does.
        Resolve<SaveSlotService>().LoadGame();
        yield return Until(InRoom, 120f, "the save loaded into the room");
        yield return new WaitForSecondsRealtime(2f);
    }

    private static readonly System.Text.RegularExpressions.Regex AssetSuffix =
        new(@"\s*-\s*(ElementInfo|DeviceInfo)$");

    /// <summary>A model's or part's asset name without the " - ElementInfo" the assets carry.</summary>
    public static string NameOf(Object asset) => AssetSuffix.Replace(asset.name, "");

    /// <summary>The device model by its asset name, or the list of names in the failure.</summary>
    public static DeviceInfo Model(string name)
    {
        var models = Resolve<DeviceInfoDatabase>().Devices.OfType<DeviceInfo>().ToList();
        return models.FirstOrDefault(m => NameOf(m) == name)
            ?? throw new StepFailed($"no device model '{name}'; models: {string.Join(", ", models.Select(m => NameOf(m)))}");
    }

    /// <summary>One part of a device model by name.</summary>
    public static ElementInfo Element(string model, string part) =>
        Model(model).Elements.OfType<ElementInfo>().FirstOrDefault(e => NameOf(e) == part)
        ?? throw new StepFailed($"{model} has no part {part}");

    /// <summary>Every device model with its parts, for picking one: "name: part, part, ...".</summary>
    public static IEnumerable<string> DescribeModels() =>
        Resolve<DeviceInfoDatabase>().Devices.OfType<DeviceInfo>().Select(m =>
            $"{NameOf(m)}: " + string.Join(", ", m.Elements.OfType<ElementInfo>().Select(e =>
                $"{NameOf(e)}{(e.Category == ElementCategory.Small ? " (screw)" : "")}{(e.CanBeDirty ? " d" : "")}{(e.CanBeBroken ? " b" : "")}")));

    private static ElementConditionBase ConditionFor(Condition condition)
    {
        var conditions = Resolve<DefaultElementConditions>();
        return condition switch
        {
            Condition.Dirty => conditions.DirtyElementCondition,
            Condition.Damaged => conditions.DamagedElementCondition,
            _ => conditions.PerfectElementCondition,
        };
    }

    /// <summary>
    /// Puts a fresh device of this model on the bench, in place of whatever was there, every part perfect
    /// but the ones named (as the game's device cheat does).
    /// </summary>
    public static IEnumerator PlaceDevice(DeviceInfo model, IReadOnlyDictionary<string, Condition> conditions)
    {
        yield return Until(InRoom, 10f, "standing in the room to swap the device");
        var unknown = conditions.Keys.Where(k => model.Elements.OfType<ElementInfo>().All(e => NameOf(e) != k)).ToList();
        if (unknown.Count > 0) throw new StepFailed($"{NameOf(model)} has no part {string.Join(", ", unknown)}");
        var elements = model.Elements.OfType<ElementInfo>().Select(info => new ElementData
        {
            Info = info,
            Condition = ConditionFor(conditions.TryGetValue(NameOf(info), out var c) ? c : Condition.Perfect),
        }).ToList();
        var deviceCondition = new RandomlyGeneratedDeviceCondition("e2e_" + Guid.NewGuid().ToString("N"), model, null, elements);
        var devices = Find<DeviceService>();
        var work = Find<WorkStateMachine>();
        var data = devices.CreateDeviceData(deviceCondition, Find<WorkSurface>().DeviceSpawnPoint);
        devices.DestroyDeviceContainer();
        devices.PlaceNewDeviceContainer(data);
        work.Enter<DisabledWorkState>();
        work.Enter<DetectionWorkState>();
        yield return null;
    }

    /// <summary>Puts a perfect spare part of this kind loose on the bench, as if taken out of the parts box.</summary>
    public static IEnumerator SpawnSpare(ElementInfo info)
    {
        // As the parts box drops a part on the bench (ElementService.TryCreateAndDropElement).
        var part = Find<ElementService>().CreateElementOnSurface(new ElementData { Info = info, Condition = ConditionFor(Condition.Perfect) });
        part.BehaviorSwitcher.SwitchToPlacedBehavior();
        part.ConditionHandler.ElementData.IsInspected = true;
        yield return null;
        part.Activate();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    /// <summary>Throws a loose part away, as the trash can would.</summary>
    public static void Discard(ElementBase part) => Find<ElementService>().DestroyElement(part);

    /// <summary>From the room to the workbench with the device on it, ready for the player's first press.</summary>
    public static IEnumerator EnterWorkbench()
    {
        Find<DeviceService>().PlacedDeviceContainer.Activate();
        yield return Until(() => WorkbenchSession.Service is { } s && WorkbenchSession.Detecting(s) && Bench.Device(), 20f,
            "the workbench session started");
        yield return new WaitForSecondsRealtime(1f);
    }

    /// <summary>The device on the bench passed the game's check as fully restored.</summary>
    public static bool Restored() =>
        Find<DeviceService>().PlacedDeviceContainer is { } container && container
        && container.Quality is Restory.Data.Devices.Quality.IdealDeviceQuality;

    /// <summary>Cleans a dirty part to perfect at once, as the game's cleaning cheat does.</summary>
    public static void Clean(ElementBase part)
    {
        var cleaner = Find<ElementCleaner>();
        cleaner.SetCleaningTool(Resolve<CleaningToolSelectionService>().CurrentlySelectedTool);
        cleaner.SetTarget(part);
        cleaner.CompleteCleaning(Resolve<SolderingService>().IsActive);
        cleaner.ResetTarget();
    }
}
