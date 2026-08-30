using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private void BuildThemeProps(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            switch (archetype.Theme)
            {
                case DungeonLayout.RoomTheme.Empty:
                    return;
                case DungeonLayout.RoomTheme.Sparse:
                    Scatter(
                        parent,
                        center,
                        archetype,
                        random,
                        occupied,
                        random.Next(0, 3),
                        DungeonPropTokens.Chair,
                        DungeonPropTokens.Stones
                    );
                    if (random.NextDouble() < 0.55)
                        PlaceSmallClusters(
                            parent,
                            center,
                            archetype,
                            random,
                            occupied,
                            1,
                            3,
                            4,
                            DungeonPropTokens.Barrel,
                            DungeonPropTokens.Pot
                        );
                    break;
                case DungeonLayout.RoomTheme.Storage:
                    Scatter(
                        parent,
                        center,
                        archetype,
                        random,
                        occupied,
                        3 + random.Next(0, 3),
                        DungeonPropTokens.WoodSupport,
                        DungeonPropTokens.WoodStructure,
                        DungeonPropTokens.Table
                    );
                    PlaceSmallClusters(
                        parent,
                        center,
                        archetype,
                        random,
                        occupied,
                        1 + random.Next(0, 2),
                        3,
                        5,
                        DungeonPropTokens.Barrel,
                        DungeonPropTokens.Pot
                    );
                    break;
                case DungeonLayout.RoomTheme.Banquet:
                    BuildBanquet(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomTheme.Armory:
                    BuildArmory(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomTheme.Shrine:
                    BuildShrine(parent, center, archetype, occupied);
                    break;
                case DungeonLayout.RoomTheme.Flooded:
                    Scatter(
                        parent,
                        center,
                        archetype,
                        random,
                        occupied,
                        3 + random.Next(0, 3),
                        DungeonPropTokens.Rocks,
                        DungeonPropTokens.Stones
                    );
                    break;
                case DungeonLayout.RoomTheme.TreasureVault:
                    BuildVault(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomTheme.Collapsed:
                    BuildCollapsed(parent, center, archetype, random, occupied);
                    break;
                case DungeonLayout.RoomTheme.Arena:
                    Scatter(
                        parent,
                        center,
                        archetype,
                        random,
                        occupied,
                        random.Next(1, 4),
                        DungeonPropTokens.Stones
                    );
                    break;
            }
        }

        private void BuildBanquet(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            bool horizontal =
                archetype.Shape == DungeonLayout.RoomShape.LongHorizontal
                || archetype.Shape == DungeonLayout.RoomShape.NarrowHorizontal;
            float[] stations = horizontal ? new[] { -6f, 0f, 6f } : new[] { -3.4f, 3.4f };
            foreach (float station in stations)
            {
                Vector2 table = horizontal ? new Vector2(station, 0f) : new Vector2(0f, station);
                PlaceNamed(
                    parent,
                    center,
                    DungeonPropTokens.Table,
                    table,
                    horizontal ? 0f : 90f,
                    occupied
                );
                Vector2 side = horizontal ? Vector2.up * 1.55f : Vector2.right * 1.55f;
                PlaceNamed(
                    parent,
                    center,
                    DungeonPropTokens.Chair,
                    table + side,
                    horizontal ? 180f : -90f,
                    occupied
                );
                PlaceNamed(
                    parent,
                    center,
                    DungeonPropTokens.Chair,
                    table - side,
                    horizontal ? 0f : 90f,
                    occupied
                );
            }
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                1,
                3,
                5,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Potion,
                DungeonPropTokens.Barrel
            );
        }

        private void BuildArmory(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            float w = Mathf.Max(3.3f, archetype.HalfWidth - 1.2f);
            float d = Mathf.Max(3.2f, archetype.HalfDepth - 1.1f);
            string[] display =
            {
                DungeonPropTokens.ShieldRound,
                DungeonPropTokens.WeaponSword,
                DungeonPropTokens.ShieldRectangle,
                DungeonPropTokens.WeaponSpear,
            };
            for (int i = 0; i < display.Length; i++)
            {
                float x = Mathf.Lerp(-w, w, (i + 0.5f) / display.Length);
                PlaceNamed(parent, center, display[i], new Vector2(x, d), 0f, occupied);
                PlaceNamed(
                    parent,
                    center,
                    display[(i + 2) % display.Length],
                    new Vector2(x, -d),
                    180f,
                    occupied
                );
            }
            PlaceNamed(
                parent,
                center,
                DungeonPropTokens.Trap,
                new Vector2(-2.8f, -1.5f),
                45f,
                occupied
            );
            PlaceNamed(
                parent,
                center,
                DungeonPropTokens.Trap,
                new Vector2(2.8f, 1.5f),
                45f,
                occupied
            );
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                1 + random.Next(0, 3),
                DungeonPropTokens.WoodSupport,
                DungeonPropTokens.Stones
            );
            if (random.NextDouble() < 0.5)
                PlaceSmallClusters(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    1,
                    3,
                    4,
                    DungeonPropTokens.Barrel,
                    DungeonPropTokens.Pot
                );
        }

        private void BuildShrine(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            List<OccupiedSpot> occupied
        )
        {
            // The four corner pillars this shrine was designed around asked for a
            // "Column" prop that no longer exists, so they have been building as
            // nothing at all. Restoring them needs a column prefab registered in
            // propPrefabs, not a token nothing answers.
            string offering = (archetype.Variant % 3) switch
            {
                0 => DungeonPropTokens.Potion,
                1 => DungeonPropTokens.Coin,
                _ => DungeonPropTokens.Key,
            };
            PlaceNamed(
                parent,
                center,
                offering,
                Vector2.zero,
                archetype.Variant * 90f,
                occupied,
                1f
            );
        }
    }
}
