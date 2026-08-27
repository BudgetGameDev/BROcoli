using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A visual-only line-of-sight volume for geometry such as an arch lintel.
/// It never participates in gameplay physics or trigger callbacks.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionVolume : MonoBehaviour
{
    private static readonly HashSet<DungeonOcclusionVolume> ActiveSet = new();

    [SerializeField]
    private Vector3 center;

    [SerializeField]
    private Vector3 size = Vector3.one;

    public static IEnumerable<DungeonOcclusionVolume> Active => ActiveSet;

    public Bounds WorldBounds
    {
        get
        {
            Vector3 half = size * 0.5f;
            Bounds bounds = new(transform.TransformPoint(center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                bounds.Encapsulate(
                    transform.TransformPoint(center + Vector3.Scale(half, new Vector3(x, y, z)))
                );
            }
            return bounds;
        }
    }

    public void Configure(Vector3 localCenter, Vector3 localSize)
    {
        center = localCenter;
        size = localSize;
    }

    private void OnEnable()
    {
        ActiveSet.Add(this);
    }

    private void OnDisable()
    {
        ActiveSet.Remove(this);
    }
}
