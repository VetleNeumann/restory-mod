using System.Collections.Generic;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace RestoryMod.Features.Hitboxes;

/// <summary>Where a collider's outline falls on screen, as points whose convex hull is that outline.</summary>
internal static class ColliderOutline
{
    private const int RingPoints = 16;
    private static readonly int[] CornerSigns = [-1, 1];

    /// <summary>
    /// A box's corners, points around a sphere or capsule, or the corners of a mesh's bounding box
    /// (<paramref name="approximate"/> is then set). False when part of it is behind the camera.
    /// </summary>
    public static bool OnScreen(Camera camera, Collider collider, List<NVector2> points, ref bool approximate)
    {
        var transform = collider.transform;
        switch (collider)
        {
            case BoxCollider box:
                return BoxCorners(camera, transform, new Bounds(box.center, box.size), points);
            case SphereCollider sphere:
                return Ring(camera, transform.TransformPoint(sphere.center), sphere.radius * LargestScale(transform), points);
            case CapsuleCollider capsule:
            {
                var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                var radius = capsule.radius * LargestScale(transform);
                var half = Mathf.Max(0f, capsule.height / 2 - capsule.radius) * axis;
                return Ring(camera, transform.TransformPoint(capsule.center + half), radius, points)
                    && Ring(camera, transform.TransformPoint(capsule.center - half), radius, points);
            }
            case MeshCollider mesh when mesh.sharedMesh:
                approximate = true;
                return BoxCorners(camera, transform, mesh.sharedMesh.bounds, points);
            default:
                approximate = true;
                return BoxCorners(camera, null, collider.bounds, points);
        }
    }

    /// <summary>Where a world point falls on screen; false when it is behind the camera or off screen.</summary>
    public static bool IsOnScreen(Camera camera, Vector3 world)
    {
        var screen = camera.WorldToScreenPoint(world);
        return screen.z > camera.nearClipPlane && screen.x >= 0 && screen.y >= 0 &&
               screen.x <= Screen.width && screen.y <= Screen.height;
    }

    private static float LargestScale(Transform transform)
    {
        var scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private static bool BoxCorners(Camera camera, Transform? transform, Bounds box, List<NVector2> points)
    {
        foreach (var x in CornerSigns)
        foreach (var y in CornerSigns)
        foreach (var z in CornerSigns)
        {
            var corner = box.center + Vector3.Scale(box.extents, new Vector3(x, y, z));
            if (!Project(camera, transform ? transform!.TransformPoint(corner) : corner, points)) return false;
        }
        return true;
    }

    /// <summary>Points around a sphere's outline as the camera sees it.</summary>
    private static bool Ring(Camera camera, Vector3 centre, float radius, List<NVector2> points)
    {
        var right = camera.transform.right * radius;
        var up = camera.transform.up * radius;
        for (var i = 0; i < RingPoints; i++)
        {
            var angle = i * 2 * Mathf.PI / RingPoints;
            if (!Project(camera, centre + right * Mathf.Cos(angle) + up * Mathf.Sin(angle), points)) return false;
        }
        return true;
    }

    private static bool Project(Camera camera, Vector3 world, List<NVector2> points)
    {
        var screen = camera.WorldToScreenPoint(world);
        if (screen.z <= camera.nearClipPlane) return false;
        points.Add(new NVector2(screen.x, screen.y));
        return true;
    }
}
