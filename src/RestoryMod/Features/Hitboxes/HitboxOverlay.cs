using System.Collections.Generic;
using System.Text;
using RestoryMod.Core;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace RestoryMod.Features.Hitboxes;

/// <summary>
/// Draws the hitbox overlay while a workbench hover state is running and the overlay is switched on.
/// Everything is drawn flat over the game from the same on-screen shapes the minimum hitbox size
/// uses, so the picture and the picking cannot drift apart.
/// </summary>
internal sealed class HitboxOverlay : MonoBehaviour
{
    private const int CursorHitLimit = 32;

    private static readonly Color Enlarged = new(1f, 0.7f, 0.1f);
    private static readonly Color GamePickColour = Color.white;

    private readonly WorkbenchHitboxes scene = new();
    private readonly PickMap pickMap = new();
    private readonly RaycastHit[] cursorHits = new RaycastHit[CursorHitLimit];
    private readonly StringBuilder text = new();

    private int computedFrame = -1;
    private float minimumSize;
    private WorkbenchThing? gamePick;
    private HitboxPick<WorkbenchThing> assistPick;
    private GUIStyle? textStyle;

    private void Awake() => useGUILayout = false;

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !HitboxOverlayFeature.Show.Value) return;

        // Only while a workbench hover state ran this frame: not while a part is held, nor away from
        // the workbench.
        var snapshot = HoverCapture.Latest;
        var camera = snapshot.Camera;
        if (Time.frameCount - snapshot.Frame > 1 || !camera) return;

        if (computedFrame != Time.frameCount)
        {
            computedFrame = Time.frameCount;
            Compute(snapshot, camera!);
        }
        Draw(snapshot);
    }

    private void Compute(HoverSnapshot snapshot, Camera camera)
    {
        minimumSize = HitboxOverlayFeature.MinimumSizePixels;
        scene.Update(camera, snapshot.LayerMask);
        gamePick = scene.Find(snapshot.GamePick);
        assistPick = snapshot.NotPicking is null
            ? HitboxPicker.Pick(ToCore(snapshot.Cursor), minimumSize, scene.Hitboxes, gamePick, snapshot.PanelUnderCursor)
            : new HitboxPick<WorkbenchThing>(gamePick, PickReason.Native);

        if (HitboxOverlayFeature.PickMap.Value) pickMap.Step(scene, camera, snapshot, minimumSize);
        else pickMap.Clear();

        WriteReadout(snapshot, camera);
    }

    private void Draw(HoverSnapshot snapshot)
    {
        if (OverlayGl.Begin())
        {
            pickMap.Draw();

            // Back to front, so nearer outlines draw over farther ones.
            for (var i = scene.Things.Count - 1; i >= 0; i--)
            {
                var thing = scene.Things[i];
                var growth = thing.Hitbox.Growth(minimumSize);
                if (growth > 0)
                {
                    foreach (var outline in thing.Hitbox.Hitbox.Outlines(growth))
                    {
                        OverlayGl.FillConvex(outline, Enlarged.WithAlpha(0.1f));
                        OverlayGl.Outline(outline, 1.5f, Enlarged.WithAlpha(0.8f));
                    }
                }
                var colour = KindColour(thing.Kind).WithAlpha(thing.Hitbox.Seen ? 0.95f : 0.35f);
                foreach (var outline in thing.Hitbox.Hitbox.Outlines(0))
                    OverlayGl.Outline(outline, thing.Kind.Enlargeable() ? 2f : 1f, colour);
            }

            if (assistPick.Thing is { } winner)
            {
                foreach (var outline in winner.Hitbox.Hitbox.Outlines(0))
                    OverlayGl.FillConvex(outline, KindColour(winner.Kind).WithAlpha(0.4f));
                var growth = winner.Hitbox.Growth(minimumSize);
                if (growth > 0)
                {
                    foreach (var outline in winner.Hitbox.Hitbox.Outlines(growth))
                        OverlayGl.Outline(outline, 2.5f, Enlarged);
                }
            }
            if (gamePick is not null && gamePick != assistPick.Thing)
            {
                foreach (var outline in gamePick.Hitbox.Hitbox.Outlines(0))
                    OverlayGl.Outline(outline, 3f, GamePickColour);
            }

            Crosshair(snapshot.Cursor);
            OverlayGl.End();
        }
        DrawReadout(snapshot.Cursor);
    }

    /// <summary>Marks the exact point the game aims its ray at, whatever the cursor picture shows.</summary>
    private static void Crosshair(Vector2 at)
    {
        foreach (var (width, colour) in new[] { (3f, Color.black), (1f, Color.white) })
        {
            OverlayGl.Line(at + new Vector2(-10, 0), at + new Vector2(-3, 0), width, colour);
            OverlayGl.Line(at + new Vector2(3, 0), at + new Vector2(10, 0), width, colour);
            OverlayGl.Line(at + new Vector2(0, -10), at + new Vector2(0, -3), width, colour);
            OverlayGl.Line(at + new Vector2(0, 3), at + new Vector2(0, 10), width, colour);
        }
    }

    private void WriteReadout(HoverSnapshot snapshot, Camera camera)
    {
        text.Clear();
        var percent = HitboxOverlayFeature.MinimumSizePercent.Value;
        text.Append($"<b>HITBOX OVERLAY</b>  minimum {minimumSize:0} px ({percent:0.#}% of {Screen.height})");
        if (HitboxOverlayFeature.PickMap.Value) text.Append("  · pick map");
        text.AppendLine();

        text.Append($"game: {Describe(gamePick)}   assist: {Describe(assistPick.Thing)} ({Reason(assistPick.Reason)})");
        if (snapshot.NotPicking is { } why) text.Append($"   [{why}]");
        text.AppendLine();

        var count = Physics.RaycastNonAlloc(camera.ScreenPointToRay(snapshot.Cursor), cursorHits,
            WorkbenchHitboxes.GameRayLength, snapshot.LayerMask);
        System.Array.Sort(cursorHits, 0, count, HitDistance.Instance);
        text.Append($"under the cursor, front to back: {count} hit{(count == 1 ? "" : "s")}");
        if (count > snapshot.GameHitLimit) text.Append($" <color=#ff8080>(the game keeps only {snapshot.GameHitLimit}, unsorted)</color>");
        text.AppendLine();
        for (var i = 0; i < Mathf.Min(count, 10); i++)
        {
            var hit = cursorHits[i];
            var thing = scene.Find(hit.collider);
            text.Append($"  {hit.distance:0.000} m  ");
            text.AppendLine(thing is null ? $"{LayerMask.LayerToName(hit.collider.gameObject.layer)}: {hit.collider.name}" : Details(thing));
        }

        var reaching = new List<(WorkbenchThing Thing, float Distance, float Growth)>();
        foreach (var hitbox in scene.Hitboxes)
        {
            var growth = hitbox.Growth(minimumSize);
            var distance = hitbox.Hitbox.DistanceTo(ToCore(snapshot.Cursor));
            if (growth > 0 && distance <= growth) reaching.Add((hitbox.Thing, distance, growth));
        }
        if (reaching.Count > 0)
        {
            reaching.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            text.AppendLine("enlarged hitboxes reaching the cursor (px from the native outline / grown):");
            foreach (var (thing, distance, growth) in reaching)
                text.AppendLine($"  {Describe(thing)}  {distance:0.0} / {growth:0.0}");
        }

        text.Append("<color=#33e6ff>screw</color> · <color=#d9d9ff>screw slot</color> · <color=#4dff4d>loose</color> · ")
            .Append("<color=#fff24d>attached</color> · <color=#ff66ff>switch</color> · <color=#999999>does nothing</color> · ")
            .Append("<color=#ffb31a>enlarged</color> · filled = assist pick · white = game pick when it differs · dim = hidden");
    }

    private string Details(WorkbenchThing thing)
    {
        var line = $"{Describe(thing)}  {thing.Hitbox.Hitbox.NarrowestWidth:0} px wide";
        var growth = thing.Hitbox.Growth(minimumSize);
        if (growth > 0) line += $", enlarged +{growth:0}";
        if (thing.Kind.Enlargeable() && !thing.Hitbox.Seen) line += ", hidden";
        if (thing.Approximate) line += ", outline approximate";
        return line;
    }

    private static string Describe(WorkbenchThing? thing) => thing is null ? "nothing" : $"{thing.Name} ({thing.Kind.Label()})";

    private static string Reason(PickReason reason) => reason switch
    {
        PickReason.Panel => "a panel is under the cursor",
        PickReason.Enlarged => "enlarged hitbox",
        _ => "native",
    };

    private void DrawReadout(Vector2 cursor)
    {
        textStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 14,
            richText = true,
            wordWrap = false,
            padding = new RectOffset(8, 8, 6, 6),
            normal = { textColor = Color.white, background = OverlayGl.Backdrop },
        };
        var content = new GUIContent(text.ToString());
        var size = textStyle.CalcSize(content);

        // GUI space runs from the top left; keep the box beside the cursor and on screen.
        var x = cursor.x + 28;
        var y = Screen.height - cursor.y + 28;
        if (x + size.x > Screen.width) x = cursor.x - 28 - size.x;
        if (y + size.y > Screen.height) y = Screen.height - cursor.y - 28 - size.y;
        GUI.Label(new Rect(Mathf.Max(0, x), Mathf.Max(0, y), size.x, size.y), content, textStyle);
    }

    internal static Color KindColour(ThingKind kind) => kind switch
    {
        ThingKind.Screw => new Color(0.2f, 0.9f, 1f),
        ThingKind.ScrewSlot => new Color(0.85f, 0.85f, 1f),
        ThingKind.LoosePart => new Color(0.3f, 1f, 0.3f),
        ThingKind.AttachedPart => new Color(1f, 0.95f, 0.3f),
        ThingKind.Switch => new Color(1f, 0.4f, 1f),
        ThingKind.WashingBinPart => new Color(0.3f, 0.8f, 0.7f),
        _ => new Color(0.6f, 0.6f, 0.6f),
    };

    internal static NVector2 ToCore(Vector2 v) => new(v.x, v.y);

    private sealed class HitDistance : IComparer<RaycastHit>
    {
        public static readonly HitDistance Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}

/// <summary>
/// Pick map: tints each spot on screen by the thing the cursor would hover there. Works through a
/// few rows of cells per frame, so the whole screen refreshes several times a second without a hitch.
/// </summary>
internal sealed class PickMap
{
    private const int Cell = 16;
    private const int RowsPerFrame = 6;

    private Color[] cells = [];
    private int columns;
    private int rows;
    private int nextRow;
    private RaycastHit[] gameHits = [];
    private readonly List<RayHit<WorkbenchThing>> scratch = new();
    private readonly List<HitboxThing<WorkbenchThing>> near = new();

    public void Clear()
    {
        if (cells.Length == 0) return;
        cells = [];
        columns = rows = nextRow = 0;
    }

    public void Step(WorkbenchHitboxes scene, Camera camera, HoverSnapshot snapshot, float minimumSize)
    {
        var wantColumns = Mathf.CeilToInt(Screen.width / (float)Cell);
        var wantRows = Mathf.CeilToInt(Screen.height / (float)Cell);
        if (wantColumns != columns || wantRows != rows)
        {
            columns = wantColumns;
            rows = wantRows;
            cells = new Color[columns * rows];
            nextRow = 0;
        }
        if (gameHits.Length != snapshot.GameHitLimit) gameHits = new RaycastHit[snapshot.GameHitLimit];

        for (var step = 0; step < RowsPerFrame; step++)
        {
            var row = nextRow;
            nextRow = (nextRow + 1) % rows;
            for (var column = 0; column < columns; column++)
            {
                var point = new Vector2((column + 0.5f) * Cell, (row + 0.5f) * Cell);
                var native = scene.GamePickAt(camera, point, snapshot.LayerMask, gameHits, scratch);

                near.Clear();
                foreach (var hitbox in scene.Hitboxes)
                {
                    var growth = hitbox.Growth(minimumSize);
                    var reach = hitbox.Thing.ScreenBounds;
                    if (growth > 0 && point.x >= reach.xMin - growth && point.x <= reach.xMax + growth &&
                        point.y >= reach.yMin - growth && point.y <= reach.yMax + growth)
                        near.Add(hitbox);
                }
                var pick = HitboxPicker.Pick(HitboxOverlay.ToCore(point), minimumSize, near, native, panelUnderCursor: false);
                cells[row * columns + column] = pick.Thing is { } winner
                    ? winner.Colour.WithAlpha(pick.Reason == PickReason.Enlarged ? 0.45f : 0.25f)
                    : Color.clear;
            }
        }
    }

    public void Draw()
    {
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var colour = cells[row * columns + column];
            if (colour.a > 0) OverlayGl.Rect(column * Cell, row * Cell, Cell, Cell, colour);
        }
    }
}

internal static class ColourExtensions
{
    public static Color WithAlpha(this Color colour, float alpha) => new(colour.r, colour.g, colour.b, alpha);
}
