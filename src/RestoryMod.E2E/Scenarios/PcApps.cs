using System;
using System.Collections;
using System.Collections.Generic;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// The pad in the room and at the PC. In the room L2 and R2 turn the view left and right, stopping at each
/// side. At the PC the D-pad's left and right switch apps along the taskbar. In the mail, L1 and R1 switch
/// folders and the D-pad's up and down step through the letters. In the browser, L2 and R2 switch tabs and
/// L1 and R1 the categories on the page. None of it logs an error, the PC's click sound included.
/// </summary>
internal sealed class PcApps : IScenario
{
    private const string Mail = "GUI_MailClient";
    private const string Browser = "GUI_WebBrowser";

    public Dictionary<string, string> Options { private get; set; } = new();

    private Driver d = null!;

    public IEnumerator Run(Driver driver)
    {
        d = driver;
        yield return Game.LoadSave();
        yield return Look();
        yield return Game.EnterPc();
        using var errors = new ErrorWatch();
        yield return Apps();
        yield return MailApp();
        yield return BrowserApp();
        yield return d.ExpectState("nothing at the PC logged an error", "errors logged", "none", errors.Errors);
        yield return Game.LeavePc();
    }

    // L2 and R2 away from the workbench: one press, one turn, no further than each side.
    private IEnumerator Look()
    {
        yield return d.ExpectState("in the room: looking ahead", "the view", "Main", Game.Look);
        yield return d.TapL2();
        yield return d.ExpectState("L2: the view turns left", "the view", "Left", Game.Look);
        yield return d.TapL2();
        yield return d.ExpectState("L2 again: the view stays left", "the view", "Left", Game.Look);
        yield return TapR2();
        yield return d.ExpectState("R2: the view turns back ahead", "the view", "Main", Game.Look);
        yield return TapR2();
        yield return d.ExpectState("R2 again: the view turns right", "the view", "Right", Game.Look);
        yield return d.TapL2();
        yield return d.ExpectState("L2: the view turns back ahead", "the view", "Main", Game.Look);
    }

    private IEnumerator TapR2()
    {
        yield return d.R2(true);
        yield return d.R2(false);
    }

    // The D-pad's left and right along the taskbar.
    private IEnumerator Apps()
    {
        var start = Pc.App();
        yield return d.Press(PadButton.Right);
        yield return d.ExpectState("D-pad right at the PC: an app on the taskbar opens", "the app open", "another app",
            () => Changed(Pc.App(), start, "app"));
        var first = Pc.App();
        yield return d.Press(PadButton.Right);
        yield return d.ExpectState("D-pad right again: the next app opens", "the app open", "another app",
            () => Changed(Pc.App(), first, "app"));
        yield return d.Press(PadButton.Left);
        yield return d.ExpectState("D-pad left: the app before opens again", "the app open", first, Pc.App);
    }

    // "another <kind>" once the thing shown has changed from what it was, else what it is now.
    private static string Changed(string now, string before, string kind) =>
        now != before && now != "none" ? $"another {kind}" : now;

    // Along the taskbar to an app with the D-pad, not checked itself.
    private IEnumerator Open(string app)
    {
        for (var i = 0; Pc.App() != app; i++)
        {
            if (i == 8) throw new StepFailed($"the D-pad never opened {app}; open: {Pc.App()}");
            yield return d.Press(PadButton.Right);
            yield return Driver.Wait(0.3f);
        }
    }

    private IEnumerator MailApp()
    {
        yield return Open(Mail);
        var folder = Pc.Folder();
        yield return d.Press(PadButton.RB);
        yield return d.ExpectState("R1 in the mail: the next folder opens", "the folder", "another folder",
            () => Changed(Pc.Folder(), folder, "folder"));
        yield return d.Press(PadButton.LB);
        yield return d.ExpectState("L1: the folder before opens again", "the folder", folder, Pc.Folder);

        var before = Pc.Letter();
        yield return d.Press(PadButton.Down);
        yield return d.ExpectState("D-pad down in the mail: another letter is selected", "the letter selected", "another letter",
            () => Pc.Letter() is null ? "none" : Same(Pc.Letter(), before) ? "the same letter" : "another letter");
        var first = Pc.Letter();
        yield return d.Press(PadButton.Down);
        yield return d.ExpectState("D-pad down again: the next letter is selected", "the letter selected", "another letter",
            () => Pc.Letter() is null ? "none" : Same(Pc.Letter(), first) ? "the same letter" : "another letter");
        yield return d.Press(PadButton.Up);
        yield return d.ExpectState("D-pad up: the letter before is selected again", "the letter selected", "the same letter",
            () => Same(Pc.Letter(), first) ? "the same letter" : "another letter");
    }

    private static bool Same(object? a, object? b) => a is not null && Equals(a, b);

    private IEnumerator BrowserApp()
    {
        yield return Open(Browser);
        var tab = Pc.Tab();
        yield return TapR2();
        yield return d.ExpectState("R2 in the browser: the next tab opens", "the tab", "another tab",
            () => Changed(Pc.Tab(), tab, "tab"));
        yield return d.TapL2();
        yield return d.ExpectState("L2: the tab before opens again", "the tab", tab, Pc.Tab);

        var category = Pc.Category();
        yield return d.Press(PadButton.RB);
        yield return d.ExpectState("R1 in the browser: the next category is picked", "the category", "another category",
            () => Changed(Pc.Category(), category, "category"));
        yield return d.Press(PadButton.LB);
        yield return d.ExpectState("L1: the category before is picked again", "the category", category, Pc.Category);
    }
}
