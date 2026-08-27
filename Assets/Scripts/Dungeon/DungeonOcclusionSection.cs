using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as an occlusion unit.
/// Wall runs link to their endpoint posts and straight continuations so each
/// visible run, gateway, and adjoining post fades as one visual unit.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class DungeonOcclusionSection : MonoBehaviour
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

    public bool TryGetGatewayFadeReference(Renderer renderer, out float minimumY, out float height)
    {
        minimumY = gatewayFadeReferenceMinY;
        height = gatewayFadeReferenceHeight;
        return renderer != null && IsGateway(renderer.transform) && gatewayFadeReferenceHeight > 0f;
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
        Vector3 groundForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
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
        float playerDepth = Vector3.Dot(playerPosition - camera.transform.position, groundForward);
        float rearDepth =
            Vector3.Dot(fromCamera, groundForward)
            + Mathf.Abs(groundForward.x) * bounds.extents.x
            + Mathf.Abs(groundForward.z) * bounds.extents.z;
        return rearDepth <= playerDepth;
    }
}
