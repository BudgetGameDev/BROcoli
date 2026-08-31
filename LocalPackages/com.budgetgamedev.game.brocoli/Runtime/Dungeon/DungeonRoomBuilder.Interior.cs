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
                bool railing = piece.AlongX;
                InstantiateScaledWall(
                    SectionFor(parent, groups, piece.Section, room),
                    piece,
                    railing ? InteriorRailingHeightScale : InteriorWallHeightScale,
                    piece.BaseLift,
                    name: railing
                        ? "DungeonWall - Interior Low Railing"
                        : "DungeonWall - Interior Half Wall"
                );
            }
            sections.Clear();
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
