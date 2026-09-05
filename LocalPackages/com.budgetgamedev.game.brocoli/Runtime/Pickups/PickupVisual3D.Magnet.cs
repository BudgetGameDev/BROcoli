using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        private static Mesh magnetMesh;

        private static Mesh GetMagnetMesh()
        {
            if (magnetMesh != null)
                return magnetMesh;

            // One continuous U, with a wide opening and equal arms for the silver tips.
            const int curveSegments = 12;
            const float outerRadius = 0.245f;
            const float innerRadius = 0.115f;
            const float halfDepth = 0.045f;
            var outer = new List<Vector2> { new(-outerRadius, 0.14f) };
            var inner = new List<Vector2> { new(-innerRadius, 0.14f) };
            for (int i = 0; i <= curveSegments; i++)
            {
                float angle = Mathf.PI + Mathf.PI * i / curveSegments;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                outer.Add(direction * outerRadius + Vector2.down * 0.01f);
                inner.Add(direction * innerRadius + Vector2.down * 0.01f);
            }
            outer.Add(new Vector2(outerRadius, 0.14f));
            inner.Add(new Vector2(innerRadius, 0.14f));

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 At(Vector2 p, float z) => new(p.x, p.y, z);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(
                    new[] { first, first + 1, first + 2, first, first + 2, first + 3 }
                );
            }

            for (int i = 0; i < outer.Count - 1; i++)
            {
                Vector3 a = At(outer[i], -halfDepth);
                Vector3 b = At(inner[i], -halfDepth);
                Vector3 c = At(outer[i + 1], -halfDepth);
                Vector3 d = At(inner[i + 1], -halfDepth);
                Vector3 back = Vector3.forward * (halfDepth * 2f);
                Quad(a, b, d, c);
                Quad(a + back, c + back, d + back, b + back);
                Quad(a, c, c + back, a + back);
                Quad(b, b + back, d + back, d);
            }
            int last = outer.Count - 1;
            Quad(
                At(outer[0], -halfDepth),
                At(outer[0], halfDepth),
                At(inner[0], halfDepth),
                At(inner[0], -halfDepth)
            );
            Quad(
                At(outer[last], -halfDepth),
                At(inner[last], -halfDepth),
                At(inner[last], halfDepth),
                At(outer[last], halfDepth)
            );
            magnetMesh = FinalizeMesh(
                "Pickup Magnet Horseshoe",
                vertices.ToArray(),
                triangles.ToArray()
            );
            return magnetMesh;
        }
    }
}
