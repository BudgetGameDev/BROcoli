using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonRoomBuilder
    {
        private readonly List<DungeonWallPiece> interiorWalls = new();
        private readonly Dictionary<string, Transform> sections = new();

        /// <summary>
        /// Instantiates a planned set of wall pieces, grouping them into occlusion
        /// sections. Runs that touch are one freestanding structure and share a
        /// section, so a cross or a T lowers every arm at once instead of dropping
        /// the run the camera happened to hit and leaving the other standing.
        /// </summary>
        private void InstantiateWallRuns(
            Transform parent,
            List<DungeonWallPiece> walls,
            Vector2Int room
        )
        {
            Dictionary<string, string> groups = DungeonWallGrouping.ResolveInteriorGroups(walls);
            sections.Clear();
            foreach (DungeonWallPiece piece in walls)
            {
                // Feature pieces are the one sanctioned full-height interior
                // wall; their hidden band is sealed off, so unlike every other
                // interior piece they may stand tall enough to hide someone.
                float heightScale =
                    piece.Kind == DungeonWallKind.InteriorFeature ? 1f
                    : piece.AlongX ? InteriorRailingHeightScale
                    : InteriorWallHeightScale;
                InstantiateScaledWall(
                    SectionFor(parent, groups, piece.Section, room),
                    piece,
                    heightScale,
                    piece.BaseLift,
                    name: piece.Kind == DungeonWallKind.InteriorFeature
                            ? "DungeonWall - Interior Feature Wall"
                        : piece.AlongX ? "DungeonWall - Interior Low Railing"
                        : "DungeonWall - Interior Half Wall"
                );
            }
            sections.Clear();
        }

        private readonly List<DungeonRailingSegment> interiorRailings = new();
        private readonly List<Rect> featureKeepOuts = new();

        /// <summary>
        /// Builds the room's curved and diagonal railing chains: the same wall
        /// masonry, trimmed to each segment's length, turned to its own yaw and
        /// kept at railing height. Being under the automatic fade height they
        /// never join the occlusion system, so the axis-aligned footprint maths
        /// never has to reason about a rotated slab.
        /// </summary>
        private void BuildRailings(
            Transform parent,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            interiorRailings.Clear();
            DungeonRoomGeometry.AppendInteriorRailings(interiorRailings, room, archetype);
            if (interiorRailings.Count == 0)
                return;

            var root = new GameObject($"Railings - {archetype.Shape}");
            root.transform.SetParent(parent, false);
            root.AddComponent<DungeonContentRoot>();
            foreach (DungeonRailingSegment segment in interiorRailings)
            {
                GameObject piece = Instantiate(
                    wallPrefab,
                    segment.PrefabPosition.ToWorld(segment.BaseLift),
                    Quaternion.Euler(0f, segment.YawDegrees, 0f),
                    root.transform
                );
                piece.name = "DungeonWall - Curved Railing";
                piece.transform.localScale = Vector3.Scale(
                    piece.transform.localScale,
                    new Vector3(segment.LengthScale, InteriorRailingHeightScale, 1f)
                );
            }
        }

        /// <summary>
        /// Seals the hidden band behind a feature wall with an invisible
        /// collider. The rocks and rubble the prop placer piles there make the
        /// blockage read as intentional; this collider is what guarantees it.
        /// </summary>
        private void BuildFeatureKeepOuts(
            Transform parent,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            featureKeepOuts.Clear();
            DungeonRoomGeometry.AppendFeatureKeepOuts(featureKeepOuts, room, archetype);
            foreach (Rect keepOut in featureKeepOuts)
            {
                var blocker = new GameObject("Feature Wall Keep-Out Collision");
                blocker.transform.SetParent(parent, false);
                blocker.transform.position = keepOut.center.ToWorld(0.7f);
                var collider = blocker.AddComponent<BoxCollider>();
                collider.size = new Vector3(keepOut.width, 1.4f, keepOut.height);
                int wallLayer = LayerMask.NameToLayer("Wall");
                if (wallLayer >= 0)
                    blocker.layer = wallLayer;
            }
        }

        private Transform SectionFor(
            Transform parent,
            Dictionary<string, string> groups,
            string section,
            Vector2Int room
        )
        {
            string group = groups.TryGetValue(section, out string resolved) ? resolved : section;
            if (!sections.TryGetValue(group, out Transform existing))
            {
                existing = CreateOcclusionSection(parent, group);
                existing.GetComponent<DungeonOcclusionSection>().ConfigureRoom(room);
                sections[group] = existing;
            }
            return existing;
        }

        private static Transform CreateOcclusionSection(Transform parent, string name)
        {
            var section = new GameObject($"Occlusion Section - {name}");
            section.transform.SetParent(parent, false);
            section.AddComponent<DungeonOcclusionSection>();
            return section.transform;
        }
    }
}
