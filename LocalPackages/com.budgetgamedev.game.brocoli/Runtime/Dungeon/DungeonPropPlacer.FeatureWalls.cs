using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly Vector3 FeatureScreenScale = new(1.4f, 0.9f, 1.4f);
        private const float FeatureScreenSpacing = 1.9f;

        /// <summary>
        /// Dresses the sealed band behind a room's full-height feature wall
        /// with a dense line of the environment's rubble. The keep-out collider
        /// built by <see cref="DungeonRoomBuilder"/> is what actually keeps the
        /// player out of the wall's occlusion shadow; this pile is what makes
        /// that blockage read as deliberate world-building instead of an
        /// invisible fence.
        /// </summary>
        private void BuildFeatureWallScreens(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            var keepOuts = new List<Rect>();
            DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, archetype);
            if (keepOuts.Count == 0)
                return;

            string[] tokens = DungeonEnvironmentProfile.Of(archetype.Environment).RubbleTokens;
            foreach (Rect keepOut in keepOuts)
            {
                int count = Mathf.Max(2, Mathf.FloorToInt(keepOut.width / FeatureScreenSpacing));
                for (int i = 0; i < count; i++)
                {
                    GameObject prefab = FindProp(tokens[(i + archetype.Variant) % tokens.Length]);
                    if (prefab == null)
                        prefab = FindProp(tokens[0]);
                    if (prefab == null)
                        return;

                    float x = Mathf.Lerp(
                        keepOut.xMin + 0.8f,
                        keepOut.xMax - 0.8f,
                        count == 1 ? 0.5f : i / (float)(count - 1)
                    );
                    float z = Mathf.Lerp(
                        keepOut.yMin + 0.7f,
                        keepOut.yMax - 0.7f,
                        (i & 1) == 0 ? 0.35f : 0.75f
                    );
                    var local = new Vector2(x, z);
                    DungeonPropMeasurement measurement = Measure(prefab);
                    SpawnScaledProp(
                        parent,
                        prefab,
                        center + local,
                        GroundPlane.YawRotation(random.Next(0, 360)),
                        FeatureScreenScale
                    );
                    occupied.Add(
                        new OccupiedSpot(local, measurement.Radius * FeatureScreenScale.x, true)
                    );
                }
            }
        }
    }
}
