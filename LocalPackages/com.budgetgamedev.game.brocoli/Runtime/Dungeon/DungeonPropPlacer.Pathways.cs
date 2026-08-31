using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        /// <summary>
        /// Places small clusters on the shoulders of the room's architectural
        /// route. The props make the route feel grown-in and inhabited, while the
        /// broad centreline and every railing gap stay free to walk through.
        /// </summary>
        private void BuildPathwayDressing(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            if (archetype.Theme == DungeonLayout.RoomTheme.Empty)
                return;

            switch (archetype.Shape)
            {
                case DungeonLayout.RoomShape.NarrowHorizontal:
                case DungeonLayout.RoomShape.LongHorizontal:
                    DressHorizontalPath(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomShape.Tiny:
                case DungeonLayout.RoomShape.NarrowVertical:
                case DungeonLayout.RoomShape.LongVertical:
                    DressVerticalPath(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomShape.Compact:
                case DungeonLayout.RoomShape.LargeSquare:
                    DressCourtyard(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomShape.Divided:
                    DressTurningPath(parent, center, archetype, random, occupied);
                    break;
            }
        }

        private void DressHorizontalPath(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            float station = Mathf.Min(6f, archetype.HalfWidth - 1.25f);
            float shoulder = Mathf.Max(1.75f, archetype.HalfDepth - 0.85f);
            foreach (
                Vector2 local in new[]
                {
                    new Vector2(-station, shoulder),
                    new Vector2(-station, -shoulder),
                    new Vector2(station, shoulder),
                    new Vector2(station, -shoulder),
                }
            )
                TryPlacePathProp(parent, center, archetype, random, occupied, local);
        }

        private void DressVerticalPath(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            float shoulder = Mathf.Max(1.75f, archetype.HalfWidth - 0.85f);
            float station = Mathf.Min(4.8f, archetype.HalfDepth - 1.25f);
            foreach (
                Vector2 local in new[]
                {
                    new Vector2(shoulder, -station),
                    new Vector2(-shoulder, -station),
                    new Vector2(shoulder, station),
                    new Vector2(-shoulder, station),
                }
            )
                TryPlacePathProp(parent, center, archetype, random, occupied, local);
        }

        private void DressCourtyard(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            float x = Mathf.Max(2.4f, archetype.HalfWidth - 1.8f);
            float z = Mathf.Max(2.4f, archetype.HalfDepth - 1.8f);
            foreach (
                Vector2 local in new[]
                {
                    new Vector2(-x, -z),
                    new Vector2(-x, z),
                    new Vector2(x, -z),
                    new Vector2(x, z),
                }
            )
                TryPlacePathProp(parent, center, archetype, random, occupied, local);
        }

        private void DressTurningPath(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            foreach (
                Vector2 local in new[]
                {
                    new Vector2(-4.8f, 4.1f),
                    new Vector2(-3.2f, -2.2f),
                    new Vector2(3.2f, 2.2f),
                    new Vector2(4.8f, -4.1f),
                }
            )
                TryPlacePathProp(parent, center, archetype, random, occupied, local);
        }

        private void TryPlacePathProp(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            Vector2 local
        )
        {
            string[] tokens =
            {
                DungeonPropTokens.Stones,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Barrel,
                DungeonPropTokens.Rocks,
            };
            GameObject prefab = FindProp(tokens[random.Next(tokens.Length)]);
            if (prefab == null)
                return;

            DungeonPropMeasurement measurement = Measure(prefab);
            float radius = measurement.Radius;
            float edgeMargin = radius + WallSealGap;
            if (
                Mathf.Abs(local.x) > archetype.HalfWidth - edgeMargin
                || Mathf.Abs(local.y) > archetype.HalfDepth - edgeMargin
                || OverlapsInteriorWall(local, radius, archetype)
            )
                return;

            foreach (OccupiedSpot other in occupied)
            {
                float separation = radius + other.Radius + PropGap * 0.45f;
                if ((local - other.Position).sqrMagnitude < separation * separation)
                    return;
            }

            SpawnProp(parent, prefab, center + local, GroundPlane.YawRotation(random.Next(0, 360)));
            occupied.Add(new OccupiedSpot(local, radius, measurement.IsLarge));
        }
    }
}
