using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as an occlusion unit.
/// Wall runs link to their endpoint posts so each run, gateway, and adjoining
/// post fades as one visual unit.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionSection : MonoBehaviour
{
    private const float EndpointTolerance = 0.25f;
    private static readonly HashSet<DungeonOcclusionSection> ConfiguredSections = new();

    private readonly HashSet<DungeonOcclusionSection> linkedSections = new();
    private Transform excludedRoot;
    private Vector3 firstEndpoint;
    private Vector3 secondEndpoint;
    private Vector3 junctionPosition;
    private bool isEdge;
    private bool isJunction;

    public void ConfigureEdge(Vector3 first, Vector3 second)
    {
        isEdge = true;
        firstEndpoint = first;
        secondEndpoint = second;
        RegisterAndRefreshLinks();
    }

    public void ConfigureJunction(Vector3 position)
    {
        isJunction = true;
        junctionPosition = position;
        RegisterAndRefreshLinks();
    }

    public void Exclude(Transform root)
    {
        excludedRoot = root;
    }

    public static bool TryCollectForHit(
        Collider hit,
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        DungeonOcclusionSection section = hit.GetComponentInParent<DungeonOcclusionSection>();
        if (section == null)
            return false;

        if (section.IsExcluded(hit.transform))
            return true;

        section.CollectWithLinks(
            camera,
            frustumPlanes,
            playerPosition,
            collectedSections,
            results,
            rendererBuffer
        );
        return true;
    }

    public static void CollectForVolume(
        DungeonOcclusionVolume volume,
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        DungeonOcclusionSection section = volume.GetComponentInParent<DungeonOcclusionSection>();
        if (section != null)
            section.CollectWithLinks(
                camera,
                frustumPlanes,
                playerPosition,
                collectedSections,
                results,
                rendererBuffer
            );
    }

    public static void CollectVisibleRenderers(
        Transform root,
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer,
        DungeonOcclusionSection section = null
    )
    {
        rendererBuffer.Clear();
        root.GetComponentsInChildren(false, rendererBuffer);
        Vector3 playerViewport = camera.WorldToViewportPoint(playerPosition);
        foreach (Renderer candidate in rendererBuffer)
        {
            if (
                candidate == null
                || !candidate.enabled
                || (section != null && section.IsExcluded(candidate.transform))
            )
                continue;
            int layerMask = 1 << candidate.gameObject.layer;
            if ((camera.cullingMask & layerMask) == 0)
                continue;
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.bounds))
                continue;

            Vector3 candidatePosition = candidate.bounds.center;
            candidatePosition.y = playerPosition.y;
            Vector3 viewportCenter = camera.WorldToViewportPoint(candidatePosition);
            if (
                viewportCenter.z > camera.nearClipPlane
                && viewportCenter.y <= playerViewport.y
            )
                results.Add(candidate);
        }
    }

    private bool IsExcluded(Transform candidate)
    {
        return excludedRoot != null
            && (candidate == excludedRoot || candidate.IsChildOf(excludedRoot));
    }

    private void CollectWithLinks(
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        CollectSection(
            this,
            camera,
            frustumPlanes,
            playerPosition,
            collectedSections,
            results,
            rendererBuffer
        );
        foreach (DungeonOcclusionSection linkedSection in linkedSections)
        {
            if (linkedSection == null)
                continue;
            CollectSection(
                linkedSection,
                camera,
                frustumPlanes,
                playerPosition,
                collectedSections,
                results,
                rendererBuffer
            );
        }
    }

    private static void CollectSection(
        DungeonOcclusionSection section,
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        if (!collectedSections.Add(section))
            return;

        CollectVisibleRenderers(
            section.transform,
            camera,
            frustumPlanes,
            playerPosition,
            results,
            rendererBuffer,
            section
        );
    }

    private void RegisterAndRefreshLinks()
    {
        ConfiguredSections.Add(this);
        RefreshLinks();
    }

    private static void RefreshLinks()
    {
        ConfiguredSections.RemoveWhere(section => section == null);
        foreach (DungeonOcclusionSection section in ConfiguredSections)
            section.linkedSections.Clear();

        float toleranceSqr = EndpointTolerance * EndpointTolerance;
        foreach (DungeonOcclusionSection edge in ConfiguredSections)
        {
            if (!edge.isEdge)
                continue;
            foreach (DungeonOcclusionSection junction in ConfiguredSections)
            {
                if (
                    !junction.isJunction
                    || (
                        GroundDistanceSqr(edge.firstEndpoint, junction.junctionPosition)
                            > toleranceSqr
                        && GroundDistanceSqr(edge.secondEndpoint, junction.junctionPosition)
                            > toleranceSqr
                    )
                )
                    continue;

                edge.linkedSections.Add(junction);
                junction.linkedSections.Add(edge);
            }
        }
    }

    private static float GroundDistanceSqr(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return x * x + z * z;
    }

    private void OnDisable()
    {
        ConfiguredSections.Remove(this);
        foreach (DungeonOcclusionSection section in ConfiguredSections)
            section.linkedSections.Remove(this);
        linkedSections.Clear();
    }
}
