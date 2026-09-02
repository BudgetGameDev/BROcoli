using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        /// <summary>
        /// The shell both glow layers are drawn on. It is a sphere rather than the crystal's own
        /// faceted gem for two reasons: the gem is split across three submeshes, so a renderer
        /// given one material would draw only a third of it, and its hard normals turn a fresnel
        /// into visible triangles instead of a glow. A sphere's normals are its own directions,
        /// which is exactly the smooth falloff the effect is built on.
        /// </summary>
        private static Mesh glowSphereMesh;

        /// <summary>
        /// Two subdivisions of an icosahedron: 320 triangles. Enough that the silhouette reads as
        /// round at the size an orb is ever drawn, and small enough to be nothing next to the
        /// dungeon itself.
        /// </summary>
        private const int GlowSphereSubdivisions = 2;

        private const float GlowSphereRadius = 0.5f;

        private static Mesh GetGlowSphereMesh()
        {
            if (glowSphereMesh != null)
                return glowSphereMesh;

            (List<Vector3> directions, List<int> triangles) = BuildIcosahedron();
            Dictionary<long, int> midpoints = new();
            for (int pass = 0; pass < GlowSphereSubdivisions; pass++)
                triangles = Subdivide(directions, triangles, midpoints);

            Vector3[] vertices = new Vector3[directions.Count];
            Vector3[] normals = new Vector3[directions.Count];
            for (int i = 0; i < directions.Count; i++)
            {
                normals[i] = directions[i];
                vertices[i] = directions[i] * GlowSphereRadius;
            }

            glowSphereMesh = new Mesh { name = "Pickup Glow Shell" };
            glowSphereMesh.SetVertices(vertices);
            glowSphereMesh.SetNormals(normals);
            glowSphereMesh.SetTriangles(triangles, 0);
            glowSphereMesh.RecalculateBounds();
            glowSphereMesh.UploadMeshData(true);
            return glowSphereMesh;
        }

        private static (List<Vector3> directions, List<int> triangles) BuildIcosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            List<Vector3> directions = new()
            {
                new Vector3(-1f, t, 0f),
                new Vector3(1f, t, 0f),
                new Vector3(-1f, -t, 0f),
                new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t),
                new Vector3(0f, 1f, t),
                new Vector3(0f, -1f, -t),
                new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f),
                new Vector3(t, 0f, 1f),
                new Vector3(-t, 0f, -1f),
                new Vector3(-t, 0f, 1f),
            };
            for (int i = 0; i < directions.Count; i++)
                directions[i] = directions[i].normalized;

            List<int> triangles = new()
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            return (directions, triangles);
        }

        /// <summary>
        /// Splits every triangle into four, pushing each new corner back out onto the unit
        /// sphere. The midpoint cache is what keeps the corners shared between neighbouring
        /// triangles, and shared corners are what keep the shading smooth across the seam.
        /// </summary>
        private static List<int> Subdivide(
            List<Vector3> directions,
            List<int> triangles,
            Dictionary<long, int> midpoints
        )
        {
            List<int> refined = new(triangles.Count * 4);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                int ab = Midpoint(directions, midpoints, a, b);
                int bc = Midpoint(directions, midpoints, b, c);
                int ca = Midpoint(directions, midpoints, c, a);

                refined.AddRange(new[] { a, ab, ca });
                refined.AddRange(new[] { b, bc, ab });
                refined.AddRange(new[] { c, ca, bc });
                refined.AddRange(new[] { ab, bc, ca });
            }

            return refined;
        }

        private static int Midpoint(
            List<Vector3> directions,
            Dictionary<long, int> midpoints,
            int first,
            int second
        )
        {
            long key = first < second
                ? ((long)first << 32) | (uint)second
                : ((long)second << 32) | (uint)first;
            if (midpoints.TryGetValue(key, out int existing))
                return existing;

            directions.Add((directions[first] + directions[second]).normalized);
            int index = directions.Count - 1;
            midpoints[key] = index;
            return index;
        }
    }
}
