using System.Collections.Generic;
using Restory.Data.Elements;
using Restory.Gameplay.Elements;
using UnityEngine;
using UnityEngine.Rendering;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// Gives a hint ghost the shape the game draws. The game builds a ghost from one mesh of its part, set
/// at the socket with that mesh's rotation and scale dropped, and a screw's at its lifted pose (most of
/// the way out): a cable's ghost came out turned and in part, a screw's floated above the screw. This
/// lays the ghost over the part as drawn: every visible mesh of it, each where it is relative to the
/// ghost's parent, merged into the ghost's own mesh. A part is either there (its meshes as drawn now)
/// or missing (its prefab as it sits once in: at the socket's origin, a cable in its attached model).
/// </summary>
internal static class GhostFit
{
    /// <summary>A part that is there, attached or loose: its meshes as drawn now.</summary>
    public static void ToPart(ElementProjection ghost, ElementBase part)
    {
        if (!ghost || !part || !ghost.transform.parent) return;
        var toParent = ghost.transform.parent.worldToLocalMatrix;
        var pieces = new List<(Mesh, Matrix4x4)>();
        foreach (var filter in part.GetComponentsInChildren<MeshFilter>())
        {
            if (Drawn(filter, part, filter.transform)) pieces.Add((filter.sharedMesh, toParent * filter.transform.localToWorldMatrix));
        }
        Fit(ghost, pieces);
    }

    /// <summary>An empty socket: the part's prefab as it sits once in.</summary>
    public static void ToPrefab(ElementProjection ghost, ElementInfo info)
    {
        var prefab = info ? info.Prefab : null;
        if (!ghost || !prefab) return;
        var root = prefab!.transform;
        var model = prefab is FlexibleElement flexible ? flexible.AttachedModel.transform : root;
        // In its socket the part sits at the origin, unturned, at its own scale.
        var toSocket = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix;
        var pieces = new List<(Mesh, Matrix4x4)>();
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            if (Drawn(filter, prefab, model)) pieces.Add((filter.sharedMesh, toSocket * filter.transform.localToWorldMatrix));
        }
        Fit(ghost, pieces);
    }

    /// <summary>
    /// A mesh the part itself shows: rendered, active up to <paramref name="top"/> (a model a prefab
    /// switches on once in counts), and not another part nested in it or a ghost on one of its sockets.
    /// </summary>
    private static bool Drawn(MeshFilter filter, ElementBase part, Transform top)
    {
        if (!filter.sharedMesh || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) return false;
        if (filter.GetComponentInParent<ElementProjection>(includeInactive: true)) return false;
        if (filter.GetComponentInParent<ElementBase>(includeInactive: true) != part) return false;
        for (var t = filter.transform; t && t != top; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
        }
        return true;
    }

    /// <summary>
    /// Sets the ghost where the first piece is (position and turn; its scale stays one, as the pulse
    /// sets it) and gives it the pieces as one mesh, scale included. A mesh the game won't let be read
    /// can't be merged: then only the first piece's place is taken, as the game's own mesh still shows.
    /// </summary>
    private static void Fit(ElementProjection ghost, List<(Mesh Mesh, Matrix4x4 Pose)> pieces)
    {
        if (pieces.Count == 0) return;
        var first = pieces[0].Pose;
        ghost.transform.localPosition = first.GetColumn(3);
        ghost.transform.localRotation = first.rotation;
        var filter = ghost.MeshFilter;
        if (!filter || !pieces.TrueForAll(p => p.Mesh.isReadable)) return;

        var fromGhost = Matrix4x4.TRS(first.GetColumn(3), first.rotation, Vector3.one).inverse;
        var parts = new List<CombineInstance>();
        var vertices = 0;
        foreach (var (mesh, pose) in pieces)
        {
            vertices += mesh.vertexCount;
            for (var i = 0; i < mesh.subMeshCount; i++)
                parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = fromGhost * pose });
        }
        var merged = new Mesh { name = pieces[0].Mesh.name + " (hint)" };
        if (vertices > ushort.MaxValue) merged.indexFormat = IndexFormat.UInt32;
        merged.CombineMeshes(parts.ToArray(), mergeSubMeshes: true, useMatrices: true);
        Puff(merged, Inflate / Mathf.Max(ghost.transform.lossyScale.x, 1e-6f));
        // The game's copy is the ghost's to destroy, as the factory destroys whatever mesh it ends with.
        var copy = filter!.sharedMesh;
        filter.sharedMesh = merged;
        if (copy) Object.Destroy(copy);
    }

    /// <summary>
    /// How far a ghost stands out from its part's surface, in metres. Lying exactly on it, the outline's
    /// depth test flips pixel by pixel between in view and hidden, and the ghost flickers.
    /// </summary>
    private const float Inflate = 0.0015f;

    /// <summary>Moves every vertex out along its normal by <paramref name="distance"/> (mesh units).</summary>
    private static void Puff(Mesh mesh, float distance)
    {
        var normals = mesh.normals;
        if (normals.Length != mesh.vertexCount)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }
        var vertices = mesh.vertices;
        for (var i = 0; i < vertices.Length; i++) vertices[i] += normals[i] * distance;
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }
}
