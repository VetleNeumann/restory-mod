using System.Collections.Generic;
using System.Linq;
using EPOOutline;
using HarmonyLib;
using Restory.Gameplay.Common;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.PartsBox;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>One thing at the workbench: a socket of the device (with its part or empty), or a loose part.</summary>
internal sealed class BenchThing
{
    public BenchThing(string name, ElementSocket? socket, ElementBase? loose)
    {
        Name = name;
        Socket = socket;
        Loose = loose;
    }

    public string Name { get; }
    public ElementSocket? Socket { get; }
    public ElementBase? Loose { get; }

    /// <summary>The part in the socket, or the loose part.</summary>
    public ElementBase? Part => Socket ? (Socket!.NestedElement ? Socket.NestedElement : null) : Loose;

    /// <summary>Where on screen to aim the cursor to act on this thing, or null when it is off screen.</summary>
    public Vector2? ScreenPoint(Camera camera)
    {
        // A part's own collider, or an empty socket's screw-slot marker.
        var own = Part ? Part!.transform : Socket!.transform;
        var collider = own.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.enabled);
        var world = collider ? collider!.bounds.center : own.position;
        var screen = camera.WorldToScreenPoint(world);
        return screen.z > 0f ? new Vector2(screen.x, screen.y) : null;
    }
}

/// <summary>
/// Reads what the player sees at the workbench: every thing there and its mark (CONTEXT.md, Mark). The
/// hint's ghosts are read off the game's pulse highlighter by the spot they sit on and the look preset they
/// wear, which is what decides how each is drawn.
/// </summary>
internal static class Bench
{
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, List<ElementProjection>> Ghosts =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, List<ElementProjection>>(
            PatchTargets.HighlighterProjections.MemberName);

    /// <summary>The device on the bench in the current workbench session, or null.</summary>
    public static Device? Device()
    {
        var service = WorkbenchSession.Service;
        if (service is null) return null;
        var device = WorkbenchSession.Device(service);
        return device ? device : null;
    }

    /// <summary>Every socket of the device (blockers included, in a stable order) and every loose part.</summary>
    public static List<BenchThing> Things()
    {
        var things = new List<BenchThing>();
        var device = Device();
        if (!device) return things;
        var seen = new HashSet<ElementSocket>();
        var queue = new Queue<ElementSocket>(device!.ElementSockets.Where(s => s));
        var counts = new Dictionary<string, int>();
        string Numbered(string name)
        {
            counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
            return name;
        }
        var sockets = new List<(string name, ElementSocket socket)>();
        while (queue.Count > 0)
        {
            var socket = queue.Dequeue();
            if (!seen.Add(socket)) continue;
            var takes = socket.CompatibleElementInfo;
            sockets.Add((Numbered(takes ? Game.NameOf(takes) : socket.name), socket));
            foreach (var next in socket.Blockers.Concat(socket.BlockedSockets))
                if (next && !seen.Contains(next)) queue.Enqueue(next);
        }
        var loose = BenchState.LooseParts(device).Where(p => p)
            .Select(p => (name: Numbered("loose " + Game.NameOf(p.Info)), part: p)).ToList();

        // Things that share a name are told apart by their order: "Screw #1", "Screw #2"...
        var taken = new Dictionary<string, int>();
        string Unique(string name)
        {
            if (counts[name] == 1) return name;
            taken[name] = taken.TryGetValue(name, out var n) ? n + 1 : 1;
            return $"{name} #{taken[name]}";
        }
        things.AddRange(sockets.Select(s => new BenchThing(Unique(s.name), s.socket, null)));
        things.AddRange(loose.Select(l => new BenchThing(Unique(l.name), null, l.part)));
        return things;
    }

    private static readonly AccessTools.FieldRef<ElementView, ElementBase> ViewElement =
        AccessTools.FieldRefAccess<ElementView, ElementBase>(PatchTargets.ElementViewElement.MemberName);
    private static readonly AccessTools.FieldRef<ElementView, OutlinableAdapter> ViewOutline =
        AccessTools.FieldRefAccess<ElementView, OutlinableAdapter>(PatchTargets.ElementViewOutline.MemberName);

    // The game's own outline is on, not just the part selected: the mod keeps a blocked part's off.
    private static bool HoverOutlined(ElementBase part)
    {
        if (!part.IsSelected) return false;
        var view = part.GetComponentsInChildren<ElementView>().FirstOrDefault(v => ViewElement(v) == part);
        if (!view) return false;
        var outline = ViewOutline(view!);
        return outline && outline.IsActive;
    }

    /// <summary>The mark each thing carries now. A hint mark counts over the hover outline.</summary>
    public static Dictionary<string, Mark> Marks(List<BenchThing> things)
    {
        var marks = things.ToDictionary(t => t.Name, t => t.Part && HoverOutlined(t.Part!) ? Mark.HoverOutline : Mark.None);

        var service = WorkbenchSession.Service;
        var highlighter = service is null ? null : WorkbenchSession.Highlighter(service);
        if (!highlighter) return marks;
        foreach (var (thing, ghost) in Drawn(things, highlighter!))
            marks[thing.Name] = MarkOf(present: thing.Loose || thing.Part, GamePulse.LookOf(ghost));
        return marks;
    }

    /// <summary>
    /// For each thing whose hint ghost wears the screw look (a screw or screw slot): whether it is drawn
    /// in the hidden style now, as one whole (CONTEXT.md, Hidden style).
    /// </summary>
    public static Dictionary<string, bool> Hidden(List<BenchThing> things)
    {
        var hidden = new Dictionary<string, bool>();
        var service = WorkbenchSession.Service;
        var highlighter = service is null ? null : WorkbenchSession.Highlighter(service);
        if (!highlighter) return hidden;
        foreach (var (thing, ghost) in Drawn(things, highlighter!))
        {
            if (GamePulse.LookOf(ghost) == Look.Screw) hidden[thing.Name] = GamePulse.DrawnHidden(highlighter!, ghost);
        }
        return hidden;
    }

    // Every ghost the pulse highlighter draws now, with the thing it sits on.
    private static IEnumerable<(BenchThing thing, ElementProjection ghost)> Drawn(List<BenchThing> things,
        ProjectionHighlighterBase highlighter)
    {
        var bySpot = new Dictionary<Transform, BenchThing>();
        foreach (var thing in things)
        {
            if (thing.Socket) bySpot[thing.Socket!.transform] = thing;
            else if (thing.Loose) bySpot[thing.Loose!.transform] = thing;
        }
        foreach (var ghost in Ghosts(highlighter))
        {
            if (!ghost || !ghost.gameObject.activeInHierarchy) continue;
            var outline = ghost.OutlinableAdapter.Outlinable;
            if (!outline || !outline.enabled) continue;
            if (!ghost.transform.parent || !bySpot.TryGetValue(ghost.transform.parent, out var thing)) continue;
            yield return (thing, ghost);
        }
    }

    // A missing socket wears the missing look, even a tube's entry with a part in it and room behind. Else
    // on a thing that is there the ghost draws over it: a pulse; in an empty socket it stands for the part
    // that goes there: a ghost. The look is read off the preset the ghost wears, which decides how it is
    // drawn (see GamePulse.Paint).
    private static Mark MarkOf(bool present, Look? look)
    {
        if (look == Look.Missing) return Mark.MissingGhost;
        return present ? Mark.Pulse : Mark.Ghost;
    }

    /// <summary>The marks as one log line per thing, for a failure report or a dump.</summary>
    public static IEnumerable<string> Describe(Dictionary<string, Mark> marks) =>
        marks.Select(m => $"[E2E]     {m.Key}: {m.Value}");
}
