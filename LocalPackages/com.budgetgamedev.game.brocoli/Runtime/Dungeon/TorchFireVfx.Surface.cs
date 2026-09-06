using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class TorchFireVfx
    {
        internal const int FlameSegments = 12;

        private void ConfigureFlameSurface(
            ParticleSystem particles,
            ParticleSystemRenderer renderer,
            Material material,
            float height
        )
        {
            if (flameMesh == null)
                flameMesh = CreateFlameMesh();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = flameMesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            // Crossed, subdivided surfaces have real height and volume. A view billboard
            // pitches its foot toward the camera, appearing detached in the overhead view.
            var main = particles.main;
            main.startSizeZ = main.startSizeX;
            main.startRotation = 0f;
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            material.SetVector("_FlameForwardWS", forward);
            // Keep the ignition region over the coals; a stronger bend carries it past the rim.
            material.SetFloat("_FlameLeanMetres", 0.1f * transform.lossyScale.y);
            material.SetFloat("_FlameHeightMetres", height * transform.lossyScale.y);
            material.SetFloat(
                "_FlamePhase",
                Mathf.Abs(GetEntityId().GetHashCode() % 1021) * 0.137f
            );
            material.SetFloat("_FlamePlaneWeight", 0.45f);
        }

        internal static Mesh CreateFlameMesh()
        {
            const int planes = 3;
            int rows = FlameSegments + 1;
            var vertices = new Vector3[planes * rows * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[planes * FlameSegments * 6];
            for (int plane = 0; plane < planes; plane++)
            {
                float angle = plane * Mathf.PI / planes;
                Vector3 across = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (int row = 0; row < rows; row++)
                {
                    float height = (float)row / FlameSegments;
                    int start = (plane * rows + row) * 2;
                    for (int side = 0; side < 2; side++)
                    {
                        vertices[start + side] =
                            across * (side - 0.5f) + Vector3.up * (height - 0.07f);
                        uvs[start + side] = new Vector2(side, height);
                    }
                    if (row == FlameSegments)
                        continue;
                    int triangle = (plane * FlameSegments + row) * 6;
                    triangles[triangle] = start;
                    triangles[triangle + 1] = start + 2;
                    triangles[triangle + 2] = start + 1;
                    triangles[triangle + 3] = start + 1;
                    triangles[triangle + 4] = start + 2;
                    triangles[triangle + 5] = start + 3;
                }
            }
            var mesh = new Mesh
            {
                name = "Torch flame surfaces",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                uv = uvs,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            // Include shader lean and breathing in renderer culling bounds.
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1.5f, 1.8f));
            return mesh;
        }
    }
}
