using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as an occlusion unit.
/// Doorways split a wall run into separate left, gateway, and right sections.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionSection : MonoBehaviour
{
    public static bool TryCollectForHit(
        Collider hit,
        Camera camera,
        Plane[] frustumPlanes,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        DungeonOcclusionSection section = hit.GetComponentInParent<DungeonOcclusionSection>();
        if (section == null)
            return false;

        if (collectedSections.Add(section))
            CollectVisibleRenderers(
                section.transform,
                camera,
                frustumPlanes,
                results,
                rendererBuffer
            );
        return true;
    }

    public static void CollectForVolume(
        DungeonOcclusionVolume volume,
        Camera camera,
        Plane[] frustumPlanes,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        DungeonOcclusionSection section = volume.GetComponentInParent<DungeonOcclusionSection>();
        if (section != null && collectedSections.Add(section))
            CollectVisibleRenderers(
                section.transform,
                camera,
                frustumPlanes,
                results,
                rendererBuffer
            );
    }

    public static void CollectVisibleRenderers(
        Transform root,
        Camera camera,
        Plane[] frustumPlanes,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        rendererBuffer.Clear();
        root.GetComponentsInChildren(false, rendererBuffer);
        foreach (Renderer candidate in rendererBuffer)
        {
            if (candidate == null || !candidate.enabled)
                continue;
            int layerMask = 1 << candidate.gameObject.layer;
            if ((camera.cullingMask & layerMask) == 0)
                continue;
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.bounds))
                results.Add(candidate);
        }
    }
}
