using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector2 = System.Numerics.Vector2;

namespace RestoryMod.Features.Hitboxes;

/// <summary>
/// Flat drawing over everything, in screen pixels from the bottom left, called from OnGUI's repaint.
/// Everything is triangles between one <see cref="Begin"/> and <see cref="End"/>.
/// </summary>
internal static class OverlayGl
{
    private static Material? material;
    private static bool noShader;
    private static Texture2D? backdrop;

    /// <summary>Dark see-through background for the readout.</summary>
    public static Texture2D Backdrop
    {
        get
        {
            if (!backdrop)
            {
                backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                var created = backdrop;
                Plugin.Teardown.Add(() =>
                {
                    if (created) Object.Destroy(created);
                });
                backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
                backdrop.Apply();
            }
            return backdrop!;
        }
    }

    /// <summary>Starts a batch of triangles. False when no shader to draw with exists (logged once).</summary>
    public static bool Begin()
    {
        if (!material && !noShader) material = CreateMaterial();
        if (!material) return false;

        material!.SetPass(0);
        GL.PushMatrix();
        // Inside OnGUI the parameterless pixel matrix runs from the top left like GUI space (seen in
        // game), so spell out bottom 0, top height: points here are measured from the bottom left.
        GL.LoadPixelMatrix(0, Screen.width, 0, Screen.height);
        GL.Begin(GL.TRIANGLES);
        return true;
    }

    public static void End()
    {
        GL.End();
        GL.PopMatrix();
    }

    private static Material? CreateMaterial()
    {
        foreach (var name in new[] { "Hidden/Internal-Colored", "UI/Default", "Sprites/Default" })
        {
            var shader = Shader.Find(name);
            if (!shader) continue;

            var created = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            created.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            created.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            created.SetInt("_Cull", (int)CullMode.Off);
            created.SetInt("_ZWrite", 0);
            created.SetInt("_ZTest", (int)CompareFunction.Always);
            Plugin.Logger.LogInfo($"Hitbox overlay draws with shader {name}.");
            Plugin.Teardown.Add(() =>
            {
                if (created) Object.Destroy(created);
            });
            return created;
        }
        noShader = true;
        Plugin.Logger.LogWarning("Hitbox overlay: no shader to draw with, so it shows only the readout.");
        return null;
    }

    public static void Line(Vector2 a, Vector2 b, float width, Color colour)
    {
        var along = b - a;
        if (along.sqrMagnitude < 0.0001f) return;
        var side = new Vector2(-along.y, along.x).normalized * (width / 2);
        Triangle(a - side, a + side, b + side, colour);
        Triangle(a - side, b + side, b - side, colour);
    }

    /// <summary>A closed outline.</summary>
    public static void Outline(IReadOnlyList<NVector2> points, float width, Color colour)
    {
        for (var i = 0; i < points.Count; i++)
            Line(ToUnity(points[i]), ToUnity(points[(i + 1) % points.Count]), width, colour);
    }

    public static void FillConvex(IReadOnlyList<NVector2> points, Color colour)
    {
        for (var i = 1; i + 1 < points.Count; i++)
            Triangle(ToUnity(points[0]), ToUnity(points[i]), ToUnity(points[i + 1]), colour);
    }

    public static void Rect(float x, float y, float width, float height, Color colour)
    {
        var bottomLeft = new Vector2(x, y);
        var topRight = new Vector2(x + width, y + height);
        Triangle(bottomLeft, new Vector2(topRight.x, y), topRight, colour);
        Triangle(bottomLeft, topRight, new Vector2(x, topRight.y), colour);
    }

    private static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color colour)
    {
        GL.Color(colour);
        GL.Vertex3(a.x, a.y, 0f);
        GL.Vertex3(b.x, b.y, 0f);
        GL.Vertex3(c.x, c.y, 0f);
    }

    private static Vector2 ToUnity(NVector2 point) => new(point.X, point.Y);
}
