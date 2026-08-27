using System.Collections.Generic;
using UnityEngine;

public sealed partial class DungeonOcclusionSection
{
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
                if (continuation == null || continuation == this || !IsCollinearWith(continuation))
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
