using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.UI.Presenters;
using RestoryMod.Core;
using RestoryMod.Features.DpadSend;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryMod.Features.MailList;

/// <summary>One row of the open mail folder's list: the mail and the button showing it.</summary>
internal readonly struct MailEntry(GUI_EmailMessageButton button, object letter)
{
    public GUI_EmailMessageButton Button { get; } = button;
    public object Letter { get; } = letter;
}

/// <summary>
/// The D-pad steps the Selected mail in the PC's Mail app, and once the player has selected a mail,
/// one stays selected: on opening another mail folder, after an order is taken, and when mail
/// arrives in an empty folder. The list scrolls to keep the Selected mail in view. See CONTEXT.md:
/// Selected mail. Every selection runs the list's own click handler, so the game opens and marks
/// the mail read as on a click.
/// </summary>
internal static class MailListFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Mail", "DpadSelectsMail", true,
            "In the PC's mail app, D-pad up/down step through the mail in the open folder (hold to repeat). " +
            "Once a mail is selected, one stays selected when switching folders or taking an order, and " +
            "the list scrolls to keep it in view.");
    }

    private static bool? targetsPresent;

    public static bool TargetsPresent() => targetsPresent ??= MailApp.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.MailClientList,
        PatchTargets.MailListEntries,
        PatchTargets.MailListClick,
        PatchTargets.MailListPairButton,
        PatchTargets.MailListPairLetter);

    /// <summary>Set by the player's first selection in this run of the Mail app.</summary>
    public static bool KeepSelected { get; private set; }

    /// <summary>The Selected mail moved; scroll it into view once the list's layout has caught up.</summary>
    public static bool ScrollPending { get; private set; }

    public static void StartKeepingSelected()
    {
        KeepSelected = true;
        RequestScroll();
    }

    public static void StopKeepingSelected() => KeepSelected = false;

    public static void RequestScroll() => ScrollPending = true;

    public static void ScrollDone() => ScrollPending = false;

    private static AccessTools.FieldRef<GUI_MailClient, GUI_MailClientEmailButtonsInFolderPanel>? listField;
    private static FieldInfo? entriesField;
    private static FieldInfo? pairButton;
    private static FieldInfo? pairLetter;
    private static Action<GUI_MailClientEmailButtonsInFolderPanel, GUI_EmailMessageButton>? click;

    public static GUI_MailClientEmailButtonsInFolderPanel? List(GUI_MailClient client) =>
        (listField ??= AccessTools.FieldRefAccess<GUI_MailClient, GUI_MailClientEmailButtonsInFolderPanel>(
            PatchTargets.MailClientList.MemberName!))(client);

    /// <summary>The open folder's mail, top first, as the list shows them.</summary>
    public static List<MailEntry> Entries(GUI_MailClientEmailButtonsInFolderPanel list)
    {
        entriesField ??= GameTargets.Field(PatchTargets.MailListEntries);
        pairButton ??= GameTargets.Field(PatchTargets.MailListPairButton);
        pairLetter ??= GameTargets.Field(PatchTargets.MailListPairLetter);

        // The pairs are structs; reading their fields through the boxed copy is enough.
        var entries = new List<MailEntry>();
        foreach (var pair in (IList)entriesField!.GetValue(list))
        {
            var button = (GUI_EmailMessageButton)pairButton!.GetValue(pair);
            if (button) entries.Add(new MailEntry(button, pairLetter!.GetValue(pair)));
        }
        return entries;
    }

    /// <summary>The Selected mail's index in <paramref name="entries"/>, or -1.</summary>
    public static int SelectedIndex(GUI_MailClientEmailButtonsInFolderPanel list, List<MailEntry> entries)
    {
        var selected = list.CurrentlySelectedLetter;
        if (selected is null) return -1;
        return entries.FindIndex(e => ReferenceEquals(e.Letter, selected));
    }

    /// <summary>Selects the mail as a click on its button would.</summary>
    public static void Select(GUI_MailClientEmailButtonsInFolderPanel list, GUI_EmailMessageButton button)
    {
        click ??= AccessTools.MethodDelegate<Action<GUI_MailClientEmailButtonsInFolderPanel, GUI_EmailMessageButton>>(
            GameTargets.Method(PatchTargets.MailListClick)!);
        click(list, button);
    }

    /// <summary>
    /// Scrolls the list so the Selected mail's row is fully visible. The top mail scrolls the list
    /// all the way up, so whatever sits above the first row shows too.
    /// </summary>
    public static void ScrollSelectedIntoView()
    {
        if (MailApp.Running() is not { } client) return;
        var list = List(client);
        if (!list) return;
        var entries = Entries(list!);
        var index = SelectedIndex(list!, entries);
        if (index < 0) return;

        var row = (RectTransform)entries[index].Button.transform;
        var scrollRect = row.GetComponentInParent<ScrollRect>();
        if (!scrollRect || !scrollRect.content) return;
        // A rebuilt list has not been laid out yet this frame.
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        if (index == 0)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
        else UiScroll.RowIntoView(scrollRect, row);
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Mail list failed (further errors suppressed): {e}");
    }
}

/// <summary>Any selection, by D-pad or click, starts keeping a mail selected.</summary>
[HarmonyPatch]
internal static class MailSelectedPatch
{
    private static bool Prepare() => MailListFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.MailListClick)!;

    private static void Postfix()
    {
        if (MailListFeature.Enabled.Value) MailListFeature.StartKeepingSelected();
    }
}

/// <summary>Opening another mail folder selects its top mail.</summary>
[HarmonyPatch]
internal static class MailFolderChangedPatch
{
    private static bool Prepare() => MailListFeature.TargetsPresent() && GameTargets.AllPresent(PatchTargets.MailFolderChanged);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.MailFolderChanged)!;

    private static void Postfix(GUI_MailClient __instance)
    {
        try
        {
            if (!MailListFeature.Enabled.Value || !MailListFeature.KeepSelected) return;
            var list = MailListFeature.List(__instance);
            if (!list) return;
            var entries = MailListFeature.Entries(list!);
            if (entries.Count > 0) MailListFeature.Select(list!, entries[0].Button);
        }
        catch (Exception e)
        {
            MailListFeature.LogOnce(e);
        }
    }
}

/// <summary>
/// The game rebuilds the list when mail arrives or moves (an order taken) and drops the selection
/// if its mail left. Afterwards the same mail, or the one taking its place, is selected.
/// </summary>
[HarmonyPatch]
internal static class MailContentsChangedPatch
{
    private static bool Prepare() => MailListFeature.TargetsPresent() && GameTargets.AllPresent(PatchTargets.MailContentsChanged);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.MailContentsChanged)!;

    private static void Prefix(GUI_MailClient __instance, out (List<object> Letters, int Selected)? __state)
    {
        __state = null;
        try
        {
            if (!MailListFeature.Enabled.Value || !MailListFeature.KeepSelected) return;
            var list = MailListFeature.List(__instance);
            if (!list) return;
            var entries = MailListFeature.Entries(list!);
            __state = (entries.ConvertAll(e => e.Letter), MailListFeature.SelectedIndex(list!, entries));
        }
        catch (Exception e)
        {
            MailListFeature.LogOnce(e);
        }
    }

    private static void Postfix(GUI_MailClient __instance, (List<object> Letters, int Selected)? __state)
    {
        if (__state is not { } before) return;
        try
        {
            var list = MailListFeature.List(__instance);
            if (!list) return;
            var entries = MailListFeature.Entries(list!);
            var target = MailSelection.AfterChange(before.Letters, before.Selected, entries.ConvertAll(e => e.Letter));
            if (target < 0) return;
            if (target != MailListFeature.SelectedIndex(list!, entries)) MailListFeature.Select(list!, entries[target].Button);
            // Mail arriving above the Selected mail pushes it down, maybe out of view.
            else MailListFeature.RequestScroll();
        }
        catch (Exception e)
        {
            MailListFeature.LogOnce(e);
        }
    }
}

/// <summary>
/// Reads the D-pad each frame while the Mail app runs; the app has no per-frame update of its own.
/// Holding up or down repeats like a held arrow key. Scrolls the Selected mail into view late in
/// the frame, after any rebuild of the list.
/// </summary>
internal sealed class MailListPoller : MonoBehaviour
{
    /// <summary>The game reads gamepad input as this Rewired player.</summary>
    private const int GamePlayerId = 0;

    /// <summary>The game's own step-and-repeat timing for held scroll input.</summary>
    private readonly HoldRepeat repeat = new(delay: 0.3f, interval: 0.1f);

    private void Update()
    {
        try
        {
            if (!MailListFeature.Enabled.Value || !MailListFeature.TargetsPresent() || !ReInput.isReady) return;
            var client = MailApp.Running();
            var held = client ? DpadSendPatch.HeldVertical(ReInput.players.GetPlayer(GamePlayerId)) : 0;
            var step = repeat.Update(held, Time.unscaledDeltaTime);
            if (step == 0) return;

            var list = MailListFeature.List(client!);
            if (!list) return;
            var entries = MailListFeature.Entries(list!);
            var current = MailListFeature.SelectedIndex(list!, entries);
            var next = MailSelection.Step(entries.Count, current, step);
            if (next >= 0 && next != current) MailListFeature.Select(list!, entries[next].Button);
            // At an end the selection stays put, but the view still comes back to it after a stick scroll.
            else if (next >= 0) MailListFeature.RequestScroll();
        }
        catch (Exception e)
        {
            MailListFeature.LogOnce(e);
        }
    }

    private void LateUpdate()
    {
        if (!MailListFeature.ScrollPending) return;
        try
        {
            MailListFeature.ScrollSelectedIntoView();
        }
        catch (Exception e)
        {
            MailListFeature.LogOnce(e);
        }
        finally
        {
            MailListFeature.ScrollDone();
        }
    }
}
