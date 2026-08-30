using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shared low-poly meshes every pickup visual reuses. Each one is built
/// once on first request and cached for the rest of the session.
/// </summary>
public sealed partial class PickupVisual3D
{
    private static Mesh GetBoxMesh()
    {
        if (boxMesh != null)
            return boxMesh;

        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
        };
        int[] triangles =
        {
            0,
            2,
            1,
            0,
            3,
            2,
            1,
            2,
            6,
            1,
            6,
            5,
            5,
            6,
            7,
            5,
            7,
            4,
            4,
            7,
            3,
            4,
            3,
            0,
            3,
            7,
            6,
            3,
            6,
            2,
            4,
            0,
            1,
            4,
            1,
            5,
        };

        boxMesh = FinalizeMesh("Pickup Box", vertices, triangles);
        return boxMesh;
    }

    private static Mesh GetCylinderMesh()
    {
        if (cylinderMesh != null)
            return cylinderMesh;

        List<Vector3> vertices = new List<Vector3>(RadialSegments * 2 + 2);
        List<int> triangles = new List<int>(RadialSegments * 12);
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RadialSegments;
            float x = Mathf.Cos(angle) * 0.5f;
            float y = Mathf.Sin(angle) * 0.5f;
            vertices.Add(new Vector3(x, y, -0.5f));
            vertices.Add(new Vector3(x, y, 0.5f));
        }

        int nearCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -0.5f));
        int farCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, 0.5f));

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;
            int near = i * 2;
            int far = near + 1;
            int nearNext = next * 2;
            int farNext = nearNext + 1;

            triangles.AddRange(new[] { nearCenter, nearNext, near });
            triangles.AddRange(new[] { farCenter, far, farNext });
            triangles.AddRange(new[] { near, nearNext, farNext, near, farNext, far });
        }

        cylinderMesh = FinalizeMesh("Pickup Cylinder", vertices.ToArray(), triangles.ToArray());
        return cylinderMesh;
    }

    private static Mesh GetRingMesh()
    {
        if (ringMesh != null)
            return ringMesh;

        List<Vector3> vertices = new List<Vector3>(RadialSegments * 4);
        List<int> triangles = new List<int>(RadialSegments * 24);
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RadialSegments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vertices.Add(new Vector3(direction.x * 0.5f, direction.y * 0.5f, -0.5f));
            vertices.Add(new Vector3(direction.x * 0.37f, direction.y * 0.37f, -0.5f));
            vertices.Add(new Vector3(direction.x * 0.5f, direction.y * 0.5f, 0.5f));
            vertices.Add(new Vector3(direction.x * 0.37f, direction.y * 0.37f, 0.5f));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int current = i * 4;
            int next = ((i + 1) % RadialSegments) * 4;

            int nearOuter = current;
            int nearInner = current + 1;
            int farOuter = current + 2;
            int farInner = current + 3;
            int nextNearOuter = next;
            int nextNearInner = next + 1;
            int nextFarOuter = next + 2;
            int nextFarInner = next + 3;

            triangles.AddRange(
                new[]
                {
                    nearOuter,
                    nextNearInner,
                    nextNearOuter,
                    nearOuter,
                    nearInner,
                    nextNearInner,
                    farOuter,
                    nextFarOuter,
                    nextFarInner,
                    farOuter,
                    nextFarInner,
                    farInner,
                    nearOuter,
                    nextNearOuter,
                    nextFarOuter,
                    nearOuter,
                    nextFarOuter,
                    farOuter,
                    nearInner,
                    farInner,
                    nextFarInner,
                    nearInner,
                    nextFarInner,
                    nextNearInner,
                }
            );
        }

        ringMesh = FinalizeMesh("Pickup Ring", vertices.ToArray(), triangles.ToArray());
        return ringMesh;
    }

    private static Mesh GetGemMesh()
    {
        if (gemMesh != null)
            return gemMesh;

        const int sides = 8;
        List<Vector3> vertices = new List<Vector3>(sides + 2)
        {
            new Vector3(0f, 0f, -0.62f),
            new Vector3(0f, 0f, 0.46f),
        };
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f));
        }

        List<int>[] facets = { new List<int>(), new List<int>(), new List<int>() };
        for (int i = 0; i < sides; i++)
        {
            int current = 2 + i;
            int next = 2 + ((i + 1) % sides);
            List<int> group = facets[i % facets.Length];
            group.AddRange(new[] { 0, next, current });
            group.AddRange(new[] { 1, current, next });
        }

        gemMesh = new Mesh { name = "Pickup Faceted Gem", subMeshCount = facets.Length };
        gemMesh.SetVertices(vertices);
        for (int i = 0; i < facets.Length; i++)
            gemMesh.SetTriangles(facets[i], i);
        gemMesh.RecalculateNormals();
        gemMesh.RecalculateBounds();
        gemMesh.UploadMeshData(true);
        return gemMesh;
    }

    private static Mesh FinalizeMesh(string meshName, Vector3[] vertices, int[] triangles)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }
}
