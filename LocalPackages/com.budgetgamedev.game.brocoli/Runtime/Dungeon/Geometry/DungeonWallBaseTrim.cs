using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Takes the loose base dressing off a wall standing on the platform's
    /// cliff edge.
    ///
    /// The kit's wall model carries the floor tile it was authored standing on,
    /// and a handful of small rocks are scattered across that tile in front of
    /// the masonry. Inside a room they land on the floor and read as rubble at
    /// the foot of the wall. On a cliff the model's apron side is the drop, so
    /// the tile reaches out past the lip and the rocks go with it - a scatter
    /// of stones hanging in open air, well clear of anything holding them up.
    ///
    /// They cannot simply be switched off: they are the same submesh of the
    /// same renderer as the wall itself. So a cliff piece is given its own copy
    /// of the mesh with those islands dropped. It is built once and shared by
    /// every piece that asks for it, so the whole cliff still draws as one mesh.
    /// </summary>
    public static class DungeonWallBaseTrim
    {
        /// <summary>
        /// Geometry is kept when the island it belongs to reaches the slab.
        /// Anything standing entirely clear of the slab on the apron side is
        /// dressing lying on the model's tile rather than part of the wall, and
        /// on a cliff there is no floor under it. Testing against the near face
        /// rather than the tile's edge leaves the decision a wide margin: the
        /// wall's own face stones bulge out of the slab and stay, while the
        /// base rocks fail it by more than half a metre.
        /// </summary>
        private const float SlabNearFace =
            DungeonWallPiece.SlabCenterOffset - DungeonWallPiece.SlabHalfThickness;

        /// <summary>
        /// Vertices this close together are one point. The model splits them
        /// for its normals and UVs, so shared indices alone would report a
        /// single solid piece as a scattering of unconnected islands.
        /// </summary>
        private const float WeldGrid = 1000f;

        private static readonly Dictionary<Mesh, Mesh> TrimmedMeshes = new();

        /// <summary>
        /// Replaces every mesh under an instantiated wall with its trimmed
        /// counterpart. Safe to call on a wall that has already been scaled or
        /// rotated: the cut is measured in the wall's own local space, where
        /// the model always sits the same way round.
        /// </summary>
        public static void RemoveLooseBase(GameObject wall)
        {
            if (wall == null)
                return;

            foreach (MeshFilter filter in wall.GetComponentsInChildren<MeshFilter>())
            {
                Mesh source = filter.sharedMesh;
                if (source == null)
                    continue;

                filter.sharedMesh = WithoutLooseBase(
                    source,
                    wall.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                );
            }
        }

        private static Mesh WithoutLooseBase(Mesh source, Matrix4x4 modelToWall)
        {
            if (TrimmedMeshes.TryGetValue(source, out Mesh cached) && cached != null)
                return cached;

            Vector3[] vertices = source.vertices;
            bool[] keep = ReachesTheSlab(vertices, source.triangles, modelToWall);

            var trimmed = new Mesh
            {
                name = $"{source.name} (no loose base)",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = source.indexFormat,
                vertices = vertices,
                normals = source.normals,
                tangents = source.tangents,
                uv = source.uv,
                colors = source.colors,
                subMeshCount = source.subMeshCount,
            };
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                trimmed.SetTriangles(KeptTriangles(source.GetTriangles(submesh), keep), submesh);
            trimmed.RecalculateBounds();

            TrimmedMeshes[source] = trimmed;
            return trimmed;
        }

        private static List<int> KeptTriangles(int[] triangles, bool[] keep)
        {
            var kept = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // The three corners of a triangle are one island by definition,
                // so whichever way the first one went, the others went with it.
                if (!keep[triangles[i]])
                    continue;
                kept.Add(triangles[i]);
                kept.Add(triangles[i + 1]);
                kept.Add(triangles[i + 2]);
            }
            return kept;
        }

        /// <summary>
        /// Marks every vertex whose connected island touches the slab.
        /// </summary>
        private static bool[] ReachesTheSlab(
            Vector3[] vertices,
            int[] triangles,
            Matrix4x4 modelToWall
        )
        {
            var island = new int[vertices.Length];
            for (int i = 0; i < island.Length; i++)
                island[i] = i;

            var welded = new Dictionary<Vector3Int, int>(vertices.Length);
            var local = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                local[i] = modelToWall.MultiplyPoint3x4(vertices[i]);
                var point = new Vector3Int(
                    Mathf.RoundToInt(local[i].x * WeldGrid),
                    Mathf.RoundToInt(local[i].y * WeldGrid),
                    Mathf.RoundToInt(local[i].z * WeldGrid)
                );
                if (welded.TryGetValue(point, out int first))
                    Merge(island, i, first);
                else
                    welded[point] = i;
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Merge(island, triangles[i], triangles[i + 1]);
                Merge(island, triangles[i + 1], triangles[i + 2]);
            }

            var reaching = new HashSet<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (local[i].z >= SlabNearFace)
                    reaching.Add(Root(island, i));
            }

            var keep = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                keep[i] = reaching.Contains(Root(island, i));
            return keep;
        }

        private static int Root(int[] island, int vertex)
        {
            while (island[vertex] != vertex)
            {
                island[vertex] = island[island[vertex]];
                vertex = island[vertex];
            }
            return vertex;
        }

        private static void Merge(int[] island, int first, int second)
        {
            first = Root(island, first);
            second = Root(island, second);
            if (first != second)
                island[first] = second;
        }
    }
}
