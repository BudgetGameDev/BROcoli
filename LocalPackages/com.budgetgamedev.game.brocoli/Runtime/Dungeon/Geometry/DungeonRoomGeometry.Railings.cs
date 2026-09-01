using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public static partial class DungeonRoomGeometry
    {
        // Railing chains obey the same envelope as interior wall runs: everything
        // stays at least one tile clear of the outer shell, so the perimeter
        // corridor survives and no chain can reach into a doorway. See
        // InteriorRunHalfTilesX/Z in DungeonRoomGeometry.Interior.
        public const float InteriorHalfWidthLimit =
            DungeonLayout.RoomWidth / 2f - DungeonLayout.TileSize;
        public const float InteriorHalfDepthLimit =
            DungeonLayout.RoomDepth / 2f - DungeonLayout.TileSize;

        /// <summary>
        /// The curved and diagonal low railings that give a room its route:
        /// serpentine curves, broken causeway parapets, diagonal lanes, arena
        /// rings, and rounded corners. Chains always carry deliberate gaps and
        /// stop inside the perimeter corridor, so they shape movement without
        /// ever sealing a doorway apart from the rest of the room. Purely a
        /// function of the archetype, like every other geometry plan.
        /// </summary>
        public static void AppendInteriorRailings(
            List<DungeonRailingSegment> railings,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            Vector2 center = DungeonLayout.RoomCenter(room);
            switch (archetype.Shape)
            {
                case DungeonLayout.RoomShape.SerpentineHall:
                    AppendSerpentine(railings, center);
                    break;
                case DungeonLayout.RoomShape.Causeway:
                    AppendCausewayParapets(railings, center);
                    break;
                case DungeonLayout.RoomShape.DiagonalGallery:
                    AppendDiagonalLane(railings, center, (archetype.Variant & 1) != 0);
                    break;
                case DungeonLayout.RoomShape.GrandArena:
                    AppendArenaRing(railings, center, archetype.Variant);
                    break;
                case DungeonLayout.RoomShape.OpenHall:
                    AppendRoundedCorners(railings, center, archetype.Variant);
                    break;
            }
        }

        /// <summary>
        /// Two railing curves snake across the hall, so the walkable channel
        /// swells and constricts. Each side carries one two-segment gap, offset
        /// to opposite ends, keeping the route loose rather than a corridor.
        /// </summary>
        private static void AppendSerpentine(List<DungeonRailingSegment> railings, Vector2 center)
        {
            const float amplitude = 2.9f;
            const float halfSpan = 9f;
            const float shoulder = 1.95f;
            const int samples = 10;

            var north = new List<Vector2>(samples + 1);
            var south = new List<Vector2>(samples + 1);
            for (int i = 0; i <= samples; i++)
            {
                float x = Mathf.Lerp(-halfSpan, halfSpan, i / (float)samples);
                float path = amplitude * Mathf.Sin(Mathf.PI * x / halfSpan);
                north.Add(center + new Vector2(x, path + shoulder));
                south.Add(center + new Vector2(x, path - shoulder));
            }

            AppendChain(railings, north, skipFrom: 7, skipTo: 8);
            AppendChain(railings, south, skipFrom: 1, skipTo: 2);
        }

        /// <summary>
        /// A narrow bridge: two straight parapet runs with one aligned break in
        /// the middle, so the causeway can still be entered from north and
        /// south. The flanks read as water or void; the archetype's shallow
        /// half depth keeps content on the deck.
        /// </summary>
        private static void AppendCausewayParapets(
            List<DungeonRailingSegment> railings,
            Vector2 center
        )
        {
            const float parapetOffset = 1.95f;
            const float gapHalfWidth = 1.1f;
            const int piecesPerRun = 4;

            foreach (float side in new[] { parapetOffset, -parapetOffset })
            {
                foreach (float direction in new[] { -1f, 1f })
                {
                    var points = new List<Vector2>(piecesPerRun + 1);
                    for (int i = 0; i <= piecesPerRun; i++)
                    {
                        float x = Mathf.Lerp(
                            direction * gapHalfWidth,
                            direction * InteriorHalfWidthLimit,
                            i / (float)piecesPerRun
                        );
                        points.Add(center + new Vector2(x, side));
                    }
                    AppendChain(railings, points);
                }
            }
        }

        /// <summary>
        /// The gallery's route made physical: two railing lines parallel to the
        /// room's main diagonal. The lane between them is walked corner to
        /// corner; the north line breaks in the middle and the south line stays
        /// short of both ends, so the lane can always be entered and crossed.
        /// </summary>
        private static void AppendDiagonalLane(
            List<DungeonRailingSegment> railings,
            Vector2 center,
            bool mirrored
        )
        {
            var from = new Vector2(-8.5f, -3.8f);
            var to = new Vector2(8.5f, 3.8f);
            Vector2 direction = (to - from).normalized;
            Vector2 offset = new Vector2(-direction.y, direction.x) * 1.9f;
            const int pieces = 8;

            var north = new List<Vector2>(pieces + 1);
            var south = new List<Vector2>(pieces + 1);
            for (int i = 0; i <= pieces; i++)
            {
                Vector2 along = Vector2.Lerp(from, to, i / (float)pieces);
                north.Add(Place(center, along + offset, mirrored));
                south.Add(Place(center, along - offset, mirrored));
            }

            AppendChain(railings, north, skipFrom: 3, skipTo: 4);
            AppendChain(railings, south, skipFrom: 0, skipTo: 0);
            railings.RemoveAt(railings.Count - 1); // south line also stops short of its far end
        }

        /// <summary>
        /// The arena's broken ring: four arcs of low railing around the fighting
        /// pit, with wide gaps on the compass points. The variant turns the
        /// whole ring, so arenas do not all break in the same places.
        /// </summary>
        private static void AppendArenaRing(
            List<DungeonRailingSegment> railings,
            Vector2 center,
            int variant
        )
        {
            const float radius = 5.4f;
            float turn = variant * 11.25f;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float from = quadrant * 90f + 25f + turn;
                AppendArc(railings, center, radius, from, from + 50f, 2);
            }
        }

        /// <summary>
        /// Quarter arcs across the hall's corners round the room off and pocket
        /// the space behind them, so an open hall reads as a shaped chamber
        /// instead of a bare box. Which corners curve is the variant's choice.
        /// </summary>
        private static void AppendRoundedCorners(
            List<DungeonRailingSegment> railings,
            Vector2 center,
            int variant
        )
        {
            const float radius = 4.6f;
            var corners = new List<Vector2>();
            bool all = variant == 3;
            if (all || (variant & 1) == 0)
            {
                corners.Add(new Vector2(InteriorHalfWidthLimit, InteriorHalfDepthLimit));
                corners.Add(new Vector2(-InteriorHalfWidthLimit, -InteriorHalfDepthLimit));
            }
            if (all || (variant & 1) == 1)
            {
                corners.Add(new Vector2(-InteriorHalfWidthLimit, InteriorHalfDepthLimit));
                corners.Add(new Vector2(InteriorHalfWidthLimit, -InteriorHalfDepthLimit));
            }

            foreach (Vector2 corner in corners)
            {
                // The arc opens toward the room centre: it starts on the corner's
                // horizontal reach and sweeps to its vertical reach.
                float towardCenterX = corner.x > 0f ? 180f : 0f;
                float sweep = (corner.x > 0f) == (corner.y > 0f) ? 90f : -90f;
                AppendArc(
                    railings,
                    center + corner,
                    radius,
                    towardCenterX,
                    towardCenterX + sweep,
                    3
                );
            }
        }

        /// <summary>Adds a polyline as railing segments, optionally skipping a
        /// run of pieces to leave a deliberate gap.</summary>
        private static void AppendChain(
            List<DungeonRailingSegment> railings,
            List<Vector2> points,
            int skipFrom = -1,
            int skipTo = -1
        )
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (i >= skipFrom && i <= skipTo)
                    continue;
                railings.Add(new DungeonRailingSegment(points[i], points[i + 1], i));
            }
        }

        /// <summary>Adds one arc as a chain of chords.</summary>
        private static void AppendArc(
            List<DungeonRailingSegment> railings,
            Vector2 center,
            float radius,
            float fromDegrees,
            float toDegrees,
            int pieces
        )
        {
            var points = new List<Vector2>(pieces + 1);
            for (int i = 0; i <= pieces; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)pieces) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            AppendChain(railings, points);
        }

        private static Vector2 Place(Vector2 center, Vector2 local, bool mirrored)
        {
            return center + new Vector2(mirrored ? -local.x : local.x, local.y);
        }
    }
}
