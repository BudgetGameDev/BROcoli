using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as an occlusion unit.
/// Wall runs link to their endpoint posts and straight continuations so each
/// visible run, gateway, and adjoining post fades as one visual unit.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionSection : MonoBehaviour
{
    private const float EndpointTolerance = 0.25f;
    private static readonly HashSet<DungeonOcclusionSection> ConfiguredSections = new();

    private readonly HashSet<DungeonOcclusionSection> linkedSections = new();
    private Transform excludedRoot;
    private Transform gatewayRoot;
    private float gatewayFadeReferenceMinY;
    private float gatewayFadeReferenceHeight;
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

    public void ConfigureGateway(Transform root)
    {
        gatewayRoot = root;
        CacheGatewayFadeReference();
    }

    public bool TryGetGatewayFadeReference(
        Renderer renderer,
        out float minimumY,
        out float height
    )
    {
        minimumY = gatewayFadeReferenceMinY;
        height = gatewayFadeReferenceHeight;
        return renderer != null
            && IsGateway(renderer.transform)
            && gatewayFadeReferenceHeight > 0f;
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

        if (section.IsGateway(hit.transform))
        {
            section.CollectGatewayUnit(
                camera,
                frustumPlanes,
                playerPosition,
                collectedSections,
                results,
                rendererBuffer
            );
            return true;
        }

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
            section.CollectGatewayUnit(
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
        DungeonOcclusionSection section = null,
        Transform alwaysIncludeRoot = null
    )
    {
        rendererBuffer.Clear();
        root.GetComponentsInChildren(false, rendererBuffer);
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

            if (
                IsWithinRoot(candidate.transform, alwaysIncludeRoot)
                || DungeonOcclusionGeometry.IsFullyOnCameraSideOfPlayer(
                    candidate.bounds,
                    camera,
                    playerPosition
                )
            )
                results.Add(candidate);
        }
    }

    private bool IsExcluded(Transform candidate)
    {
        return excludedRoot != null
            && (candidate == excludedRoot || candidate.IsChildOf(excludedRoot));
    }

    private bool IsGateway(Transform candidate)
    {
        return IsWithinRoot(candidate, gatewayRoot);
    }

    private void CollectGatewayUnit(
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        if (gatewayRoot == null)
        {
            CollectWithLinks(
                camera,
                frustumPlanes,
                playerPosition,
                collectedSections,
                results,
                rendererBuffer
            );
            return;
        }

        collectedSections.Add(this);
        CollectVisibleRenderers(
            transform,
            camera,
            frustumPlanes,
            playerPosition,
            results,
            rendererBuffer,
            this,
            gatewayRoot
        );
        CollectLinkedJunctions(
            camera,
            frustumPlanes,
            playerPosition,
            collectedSections,
            results,
            rendererBuffer
        );
    }

    private static bool IsWithinRoot(Transform candidate, Transform root)
    {
        return root != null && (candidate == root || candidate.IsChildOf(root));
    }

    private void CacheGatewayFadeReference()
    {
        gatewayFadeReferenceMinY = 0f;
        gatewayFadeReferenceHeight = 0f;
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(false))
        {
            if (
                candidate == null
                || IsGateway(candidate.transform)
                || IsExcluded(candidate.transform)
                || candidate.bounds.size.y <= gatewayFadeReferenceHeight
            )
                continue;

            gatewayFadeReferenceMinY = candidate.bounds.min.y;
            gatewayFadeReferenceHeight = candidate.bounds.size.y;
        }
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
        // The directly occluding run and its gateway are one visual unit.
        // Keep the gateway included when the player is close enough for its
        // bounds to straddle the player's ground depth; linked runs still use
        // the stricter camera-side test in CollectSection below.
        CollectSection(
            this,
            camera,
            frustumPlanes,
            playerPosition,
            collectedSections,
            results,
            rendererBuffer,
            gatewayRoot
        );
        CollectLinkedJunctions(
            camera,
            frustumPlanes,
            playerPosition,
            collectedSections,
            results,
            rendererBuffer
        );
    }

    private void CollectLinkedJunctions(
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        foreach (DungeonOcclusionSection junction in linkedSections)
        {
            if (junction == null)
                continue;

            CollectSection(
                junction,
                camera,
                frustumPlanes,
                playerPosition,
                collectedSections,
                results,
                rendererBuffer
            );

            // Shared boundary runs are generated once per room-width segment.
            // At their common post, the first mesh of the neighbouring segment
            // overlaps the same visual seam. Include only a straight continuation
            // so that mesh cannot poke through a lowered run, without fading the
            // unrelated perpendicular walls that also meet at the post.
            if (!junction.isJunction || !isEdge)
                continue;
            foreach (DungeonOcclusionSection continuation in junction.linkedSections)
            {
                if (
                    continuation == null
                    || continuation == this
                    || !IsCollinearWith(continuation)
                )
                    continue;

                CollectSection(
                    continuation,
                    camera,
                    frustumPlanes,
                    playerPosition,
                    collectedSections,
                    results,
                    rendererBuffer,
                    continuation.gatewayRoot
                );
            }
        }
    }

    private bool IsCollinearWith(DungeonOcclusionSection other)
    {
        if (!isEdge || other == null || !other.isEdge)
            return false;

        Vector3 direction = secondEndpoint - firstEndpoint;
        Vector3 otherDirection = other.secondEndpoint - other.firstEndpoint;
        direction.y = 0f;
        otherDirection.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f || otherDirection.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        otherDirection.Normalize();
        return Mathf.Abs(Vector3.Dot(direction, otherDirection)) >= 0.999f;
    }

    private static void CollectSection(
        DungeonOcclusionSection section,
        Camera camera,
        Plane[] frustumPlanes,
        Vector3 playerPosition,
        HashSet<DungeonOcclusionSection> collectedSections,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer,
        Transform alwaysIncludeRoot = null
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
            section,
            alwaysIncludeRoot
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

internal static class DungeonOcclusionGeometry
{
    public static bool IsFullyOnCameraSideOfPlayer(
        Bounds bounds,
        Camera camera,
        Vector3 playerPosition
    )
    {
        // A wall that reaches beside or behind the player is not an occluder,
        // even when its centre is slightly camera-side. Compare the rear-most
        // point of its ground footprint so linked wall runs cannot shorten a
        // neighbouring full-height piece that merely straddles the player.
        Vector3 groundForward = Vector3.ProjectOnPlane(
            camera.transform.forward,
            Vector3.up
        );
        if (groundForward.sqrMagnitude <= 0.0001f)
        {
            Vector3 candidatePosition = bounds.center;
            candidatePosition.y = playerPosition.y;
            Vector3 candidateViewport = camera.WorldToViewportPoint(candidatePosition);
            Vector3 playerViewport = camera.WorldToViewportPoint(playerPosition);
            return candidateViewport.z > camera.nearClipPlane
                && candidateViewport.y <= playerViewport.y;
        }

        groundForward.Normalize();
        Vector3 fromCamera = bounds.center - camera.transform.position;
        float playerDepth = Vector3.Dot(
            playerPosition - camera.transform.position,
            groundForward
        );
        float rearDepth =
            Vector3.Dot(fromCamera, groundForward)
            + Mathf.Abs(groundForward.x) * bounds.extents.x
            + Mathf.Abs(groundForward.z) * bounds.extents.z;
        return rearDepth <= playerDepth;
    }
}
