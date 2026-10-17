using System;
using System.Collections.Generic;
using System.Numerics;

namespace RestoryMod.Core;

/// <summary>
/// A thing's hitbox as it appears on screen, in pixels: one convex outline per shape the game hit
/// tests (a part can have several). A convex shape's on-screen outline is the convex hull of its
/// corners projected to the screen, so that is what each outline is built from.
/// </summary>
public sealed class Hitbox
{
    /// <summary>Each shape's outline, counter-clockwise.</summary>
    private readonly Vector2[][] outlines;

    private Hitbox(Vector2[][] outlines, float narrowestWidth)
    {
        this.outlines = outlines;
        NarrowestWidth = narrowestWidth;
    }

    /// <summary>Narrowest width across the whole thing, measured in any direction.</summary>
    public float NarrowestWidth { get; }

    /// <summary>Builds a hitbox from each shape's corners as projected to the screen.</summary>
    public static Hitbox FromShapes(IEnumerable<IReadOnlyList<Vector2>> shapes)
    {
        var outlines = new List<Vector2[]>();
        var allPoints = new List<Vector2>();
        foreach (var shape in shapes)
        {
            var outline = ConvexHull(shape);
            if (outline.Length == 0) continue;
            outlines.Add(outline);
            allPoints.AddRange(outline);
        }
        return new Hitbox(outlines.ToArray(), Width(ConvexHull(allPoints)));
    }

    /// <summary>
    /// How far the enlarged hitbox reaches past the native one on every side: enough for the
    /// narrowest width to reach <paramref name="minimumSize"/>, or 0 for a hitbox already that wide.
    /// </summary>
    public float GrowthFor(float minimumSize) => Math.Max(0, (minimumSize - NarrowestWidth) / 2);

    /// <summary>
    /// The outline of each shape grown by <paramref name="growth"/> px on every side, for drawing:
    /// straight edges pushed out, corners rounded. With 0 growth, the native outlines.
    /// </summary>
    public IReadOnlyList<Vector2[]> Outlines(float growth)
    {
        if (growth <= 0) return outlines;

        var grown = new Vector2[outlines.Length][];
        for (var i = 0; i < outlines.Length; i++)
            grown[i] = Grow(outlines[i], growth);
        return grown;
    }

    /// <summary>Largest angle one straight step may cut off a rounded corner.</summary>
    private const float CornerStep = MathF.PI / 12;

    private static Vector2[] Grow(Vector2[] outline, float growth)
    {
        if (outline.Length == 1) return Arc(outline[0], 0, 2 * MathF.PI, growth, closed: true);

        // Each corner gets an arc from the outward normal of the edge coming in to that of the edge
        // going out; the straight edges join consecutive arcs. A segment becomes a stadium.
        var points = new List<Vector2>();
        for (var i = 0; i < outline.Length; i++)
        {
            var corner = outline[i];
            var from = Angle(OutwardNormal(outline[(i + outline.Length - 1) % outline.Length], corner));
            var to = Angle(OutwardNormal(corner, outline[(i + 1) % outline.Length]));
            if (to < from) to += 2 * MathF.PI;
            points.AddRange(Arc(corner, from, to, growth, closed: false));
        }
        return points.ToArray();
    }

    private static Vector2[] Arc(Vector2 centre, float from, float to, float radius, bool closed)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling((to - from) / CornerStep));
        var points = new Vector2[closed ? steps : steps + 1];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = from + (to - from) * i / steps;
            points[i] = centre + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return points;
    }

    /// <summary>Unit normal pointing away from a counter-clockwise outline's inside.</summary>
    private static Vector2 OutwardNormal(Vector2 a, Vector2 b) => Vector2.Normalize(new Vector2(b.Y - a.Y, a.X - b.X));

    private static float Angle(Vector2 direction) => MathF.Atan2(direction.Y, direction.X);

    /// <summary>How far a screen point lies outside the hitbox: 0 on or inside any of its shapes.</summary>
    public float DistanceTo(Vector2 point)
    {
        var nearest = float.MaxValue;
        foreach (var outline in outlines)
            nearest = Math.Min(nearest, DistanceTo(outline, point));
        return nearest;
    }

    private static float DistanceTo(Vector2[] outline, Vector2 point)
    {
        if (outline.Length == 1) return Vector2.Distance(outline[0], point);

        var inside = outline.Length >= 3;
        var nearest = float.MaxValue;
        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            if (Cross(a, b, point) < 0) inside = false;
            nearest = Math.Min(nearest, DistanceToSegment(point, a, b));
        }
        return inside ? 0 : nearest;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var along = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared(), 0, 1) : 0;
        return Vector2.Distance(point, a + ab * along);
    }

    /// <summary>Andrew's monotone chain: the counter-clockwise hull, without collinear points.</summary>
    private static Vector2[] ConvexHull(IReadOnlyList<Vector2> points)
    {
        var sorted = new List<Vector2>(points);
        sorted.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        if (sorted.Count < 3) return Distinct(sorted);

        var hull = new Vector2[sorted.Count * 2];
        var count = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            while (count >= 2 && Cross(hull[count - 2], hull[count - 1], sorted[i]) <= 0) count--;
            hull[count++] = sorted[i];
        }
        for (int i = sorted.Count - 2, lower = count + 1; i >= 0; i--)
        {
            while (count >= lower && Cross(hull[count - 2], hull[count - 1], sorted[i]) <= 0) count--;
            hull[count++] = sorted[i];
        }
        // The last point repeats the first.
        Array.Resize(ref hull, Math.Max(count - 1, 1));
        return hull;
    }

    private static Vector2[] Distinct(List<Vector2> sorted)
    {
        if (sorted.Count == 2 && sorted[0] == sorted[1]) sorted.RemoveAt(1);
        return sorted.ToArray();
    }

    /// <summary>Narrowest width of a convex outline: the smallest of its heights over each edge.</summary>
    private static float Width(Vector2[] outline)
    {
        if (outline.Length < 3) return 0;

        var narrowest = float.MaxValue;
        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            var length = Vector2.Distance(a, b);
            var height = 0f;
            foreach (var p in outline)
                height = Math.Max(height, Cross(a, b, p) / length);
            narrowest = Math.Min(narrowest, height);
        }
        return narrowest;
    }

    /// <summary>Twice the signed area of triangle o-a-b; positive when it turns counter-clockwise.</summary>
    private static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}
