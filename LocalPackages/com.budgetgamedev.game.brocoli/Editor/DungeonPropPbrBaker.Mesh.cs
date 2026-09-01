using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    public static partial class DungeonPropPbrBaker
    {
        /// <summary>The palette families a Kenney prop is painted from.</summary>
        public enum PaletteFamily
        {
            Warm,
            Cool,
            Dark,
            Gold,
        }

        /// <summary>
        /// Which family a palette colour belongs to. The atlas paints wood and
        /// terracotta in warm oranges, iron and stone in cool blue-greys, iron
        /// fittings almost black, and coins in saturated yellow.
        /// </summary>
        public static PaletteFamily Classify(Color color)
        {
            // Gold runs the red channel to the top of the palette; pale timber
            // gets close enough that a looser test claims the wood as well.
            if (color.r > 0.97f && color.b < 0.5f && color.g < 0.85f)
                return PaletteFamily.Gold;
            if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) < 0.32f)
                return PaletteFamily.Dark;
            return color.r > color.b + 0.04f ? PaletteFamily.Warm : PaletteFamily.Cool;
        }

        /// <summary>
        /// Splits <paramref name="source"/> into one sub-mesh per material the
        /// recipe asks for and gives it projected UVs. Vertices are unshared so
        /// neighbouring faces can project onto different planes without
        /// dragging a seam across each other.
        /// </summary>
        private static Mesh BuildMesh(
            Mesh source,
            Texture2D atlas,
            Recipe recipe,
            List<string> materials
        )
        {
            Color[] palette = ReadPixels(atlas, out int width, out int height);
            if (palette == null)
                return null;

            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] atlasUvs = source.uv;
            int[] triangles = source.triangles;
            if (atlasUvs.Length != positions.Length || triangles.Length == 0)
                return null;

            var vertices = new List<Vector3>(triangles.Length);
            var vertexNormals = new List<Vector3>(triangles.Length);
            var uvs = new List<Vector2>(triangles.Length);
            var groups = new List<List<int>>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                Vector2 centre = (atlasUvs[a] + atlasUvs[b] + atlasUvs[c]) / 3f;
                string material = recipe.For(Classify(Sample(palette, width, height, centre)));

                int group = materials.IndexOf(material);
                if (group < 0)
                {
                    materials.Add(material);
                    groups.Add(new List<int>());
                    group = materials.Count - 1;
                }

                Vector3 face = FaceNormal(positions[a], positions[b], positions[c]);
                AppendTriangle(
                    positions,
                    normals,
                    a,
                    b,
                    c,
                    face,
                    recipe.Cylindrical,
                    vertices,
                    vertexNormals,
                    uvs,
                    groups[group]
                );
            }

            var mesh = new Mesh { name = source.name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertexNormals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = groups.Count;
            for (int i = 0; i < groups.Count; i++)
                mesh.SetTriangles(groups[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AppendTriangle(
            Vector3[] positions,
            Vector3[] normals,
            int a,
            int b,
            int c,
            Vector3 face,
            bool cylindrical,
            List<Vector3> vertices,
            List<Vector3> vertexNormals,
            List<Vector2> uvs,
            List<int> group
        )
        {
            Vector3[] corners = { positions[a], positions[b], positions[c] };
            Vector2[] projected = cylindrical
                ? ProjectCylindrical(corners, face)
                : ProjectBox(corners, face);

            int[] indices = { a, b, c };
            for (int i = 0; i < 3; i++)
            {
                group.Add(vertices.Count);
                vertices.Add(corners[i]);
                vertexNormals.Add(normals.Length == positions.Length ? normals[indices[i]] : face);
                uvs.Add(projected[i]);
            }
        }

        /// <summary>
        /// Plane-projects a face along whichever axis it mostly faces. UVs come
        /// out in metres, so every prop shares the texel density the material's
        /// tiling sets.
        /// </summary>
        private static Vector2[] ProjectBox(Vector3[] corners, Vector3 face)
        {
            var uvs = new Vector2[3];
            float x = Mathf.Abs(face.x);
            float y = Mathf.Abs(face.y);
            float z = Mathf.Abs(face.z);
            for (int i = 0; i < 3; i++)
            {
                Vector3 corner = corners[i];
                if (y >= x && y >= z)
                    uvs[i] = new Vector2(corner.x, corner.z);
                else if (x >= z)
                    uvs[i] = new Vector2(corner.z, corner.y);
                else
                    uvs[i] = new Vector2(corner.x, corner.y);
            }
            return uvs;
        }

        /// <summary>
        /// Wraps a face around the prop's vertical axis, so barrel staves and
        /// pot walls read as one continuous surface instead of four flat sides
        /// meeting at hard seams. Caps still project flat.
        /// </summary>
        private static Vector2[] ProjectCylindrical(Vector3[] corners, Vector3 face)
        {
            if (Mathf.Abs(face.y) > 0.6f)
                return ProjectBox(corners, face);

            var uvs = new Vector2[3];
            float reference = Mathf.Atan2(corners[0].z, corners[0].x);
            for (int i = 0; i < 3; i++)
            {
                Vector3 corner = corners[i];
                float angle = Mathf.Atan2(corner.z, corner.x);
                // Keep a face that straddles the seam continuous rather than
                // letting one corner jump a full turn away from the others.
                while (angle - reference > Mathf.PI)
                    angle -= 2f * Mathf.PI;
                while (reference - angle > Mathf.PI)
                    angle += 2f * Mathf.PI;
                float radius = new Vector2(corner.x, corner.z).magnitude;
                uvs[i] = new Vector2(angle * Mathf.Max(radius, 0.01f), corner.y);
            }
            return uvs;
        }

        private static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            return normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
        }

        private static Color Sample(Color[] palette, int width, int height, Vector2 uv)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * width), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * height), 0, height - 1);
            return palette[y * width + x];
        }

        /// <summary>
        /// Reads an atlas straight off disk. The imported texture is compressed
        /// and unreadable, and making it readable just to bake would change how
        /// it ships.
        /// </summary>
        private static Color[] ReadPixels(Texture2D atlas, out int width, out int height)
        {
            width = 0;
            height = 0;
            string path = UnityEditor.AssetDatabase.GetAssetPath(atlas);
            if (string.IsNullOrEmpty(path))
                return null;

            byte[] bytes = System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(path));
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(bytes))
            {
                Object.DestroyImmediate(decoded);
                return null;
            }

            width = decoded.width;
            height = decoded.height;
            Color[] pixels = decoded.GetPixels();
            Object.DestroyImmediate(decoded);
            return pixels;
        }
    }
}
