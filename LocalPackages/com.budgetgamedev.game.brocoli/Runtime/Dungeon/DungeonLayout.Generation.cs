using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class DungeonLayout
    {
        /// <summary>
        /// Chooses a repeatable room shape and theme. Theme affinities make the
        /// combinations feel authored: feasts use long halls, vaults use focused
        /// square chambers, and flooded/collapsed rooms favour broader footprints.
        /// </summary>
        public RoomArchetype Archetype(Vector2Int room)
        {
            EnvironmentTheme environment = EnvironmentAt(room);
            if (Ring(room) == 0)
                return CreateArchetype(RoomShape.OpenHall, RoomTheme.Sparse, environment, 0);

            if (TryGetMegaCluster(room, out Vector2Int anchor, out _))
            {
                // The whole cluster shares one theme rolled at its anchor, so a
                // merged hall reads as one authored space, not a patchwork.
                System.Random megaRandom = RoomRandom(anchor, MegaThemeSalt);
                RoomTheme megaTheme = megaRandom.NextDouble() switch
                {
                    < 0.30 => RoomTheme.Arena,
                    < 0.50 => RoomTheme.Banquet,
                    < 0.68 => RoomTheme.Flooded,
                    < 0.84 => RoomTheme.Storage,
                    _ => RoomTheme.Sparse,
                };
                return CreateArchetype(
                    RoomShape.MegaSection,
                    megaTheme,
                    environment,
                    megaRandom.Next(0, 4)
                );
            }

            System.Random themeRandom = RoomRandom(room, 808);
            double themeRoll = themeRandom.NextDouble();
            RoomTheme theme = themeRoll switch
            {
                < 0.08 => RoomTheme.Empty,
                < 0.20 => RoomTheme.Sparse,
                < 0.34 => RoomTheme.Storage,
                < 0.46 => RoomTheme.Banquet,
                < 0.58 => RoomTheme.Armory,
                < 0.68 => RoomTheme.Shrine,
                < 0.78 => RoomTheme.Flooded,
                < 0.85 => RoomTheme.TreasureVault,
                < 0.92 => RoomTheme.Collapsed,
                _ => RoomTheme.Arena,
            };

            System.Random shapeRandom = RoomRandom(room, 809);
            double shapeRoll = shapeRandom.NextDouble();
            RoomShape shape;
            switch (theme)
            {
                case RoomTheme.Arena:
                    shape = RoomShape.GrandArena;
                    break;
                case RoomTheme.Banquet:
                    shape =
                        shapeRoll < 0.30 ? RoomShape.LongHorizontal
                        : shapeRoll < 0.45 ? RoomShape.NarrowHorizontal
                        : shapeRoll < 0.75 ? RoomShape.DiagonalGallery
                        : shapeRoll < 0.87 ? RoomShape.LongVertical
                        : RoomShape.LargeSquare;
                    break;
                case RoomTheme.Shrine:
                case RoomTheme.TreasureVault:
                    shape =
                        shapeRoll < 0.15 ? RoomShape.Tiny
                        : shapeRoll < 0.30 ? RoomShape.Compact
                        : shapeRoll < 0.45 ? RoomShape.LargeSquare
                        : shapeRoll < 0.75 ? RoomShape.DiagonalGallery
                        : shapeRoll < 0.88 ? RoomShape.NarrowVertical
                        : RoomShape.OpenHall;
                    break;
                case RoomTheme.Flooded:
                    shape =
                        shapeRoll < 0.18 ? RoomShape.OpenHall
                        : shapeRoll < 0.58 ? RoomShape.DiagonalGallery
                        : shapeRoll < 0.68 ? RoomShape.LongHorizontal
                        : shapeRoll < 0.82 ? RoomShape.LargeSquare
                        : shapeRoll < 0.92 ? RoomShape.NarrowVertical
                        : RoomShape.Divided;
                    break;
                case RoomTheme.Collapsed:
                    shape =
                        shapeRoll < 0.58 ? RoomShape.DiagonalGallery
                        : shapeRoll < 0.72 ? RoomShape.Divided
                        : shapeRoll < 0.82 ? RoomShape.NarrowVertical
                        : shapeRoll < 0.90 ? RoomShape.Tiny
                        : shapeRoll < 0.97 ? RoomShape.OpenHall
                        : RoomShape.LargeSquare;
                    break;
                case RoomTheme.Empty:
                    shape =
                        shapeRoll < 0.55 ? RoomShape.DiagonalGallery
                        : shapeRoll < 0.70 ? RoomShape.NarrowVertical
                        : shapeRoll < 0.82 ? RoomShape.Tiny
                        : shapeRoll < 0.92 ? RoomShape.Divided
                        : RoomShape.OpenHall;
                    break;
                default:
                    shape = shapeRoll switch
                    {
                        < 0.40 => RoomShape.DiagonalGallery,
                        < 0.48 => RoomShape.Tiny,
                        < 0.56 => RoomShape.Compact,
                        < 0.62 => RoomShape.NarrowHorizontal,
                        < 0.68 => RoomShape.LongHorizontal,
                        < 0.78 => RoomShape.LargeSquare,
                        < 0.86 => RoomShape.NarrowVertical,
                        < 0.92 => RoomShape.LongVertical,
                        < 0.97 => RoomShape.Divided,
                        _ => RoomShape.OpenHall,
                    };
                    break;
            }

            return CreateArchetype(shape, theme, environment, shapeRandom.Next(0, 4));
        }

        /// <summary>
        /// Rolls a room's population archetype. Counts are capacity-relative, so
        /// the same distribution packs a tiny chamber wall-to-wall and floods an
        /// open hall: some rooms are empty, most hold a small or medium group,
        /// some are packed, a few fill to capacity as swarms, and rare rooms are
        /// elite dens (a couple of low-tier enemies promoted to elites).
        /// </summary>
        public RoomPopulation Population(Vector2Int room)
        {
            int ring = Ring(room);
            if (ring == 0)
                return new RoomPopulation(0, false);
            RoomArchetype archetype = Archetype(room);
            if (archetype.Theme == RoomTheme.Empty)
                return new RoomPopulation(0, false);

            System.Random random = RoomRandom(room, 303);
            if (archetype.Shape == RoomShape.GrandArena)
            {
                int hordeSize = 13 + random.Next(0, 6) + Mathf.Min(ring, 3);
                return new RoomPopulation(Mathf.Min(archetype.EnemyCapacity, hordeSize), false);
            }

            if (archetype.Shape == RoomShape.MegaSection)
                return MegaCellPopulation(room, random, ring, archetype.EnemyCapacity);

            int capacity = archetype.EnemyCapacity;
            double roll = random.NextDouble();
            if (roll < 0.14)
                return new RoomPopulation(0, false);
            if (roll < 0.24 && ring >= 2 && capacity >= RoomPopulation.SpiderSwarmSize)
                return RoomPopulation.SpiderSwarm();
            if (roll < 0.46)
                return new RoomPopulation(1 + random.Next(0, 2) + ring / 4, false);
            if (roll < 0.72)
                return new RoomPopulation(
                    Mathf.Min(capacity, 3 + random.Next(0, 3) + Mathf.Min(ring, 5)),
                    false
                );
            if (roll < 0.86)
                return new RoomPopulation(
                    Mathf.Min(capacity, Mathf.Max(3, capacity * 3 / 4 + random.Next(0, 3))),
                    false
                );
            if (roll < 0.94)
                // A swarm fills the room to capacity, however small the room is.
                return new RoomPopulation(capacity, false);
            return new RoomPopulation(2 + random.Next(0, 2), true);
        }

        /// <summary>
        /// Population for one cell of a mega room. The cluster rolls its character
        /// once at its anchor — deserted, scattered, an elite patrol, packed, or a
        /// wall-to-wall horde — and every cell jitters around that, so a horde
        /// hall is a horde in every corner rather than a patchwork.
        /// </summary>
        private RoomPopulation MegaCellPopulation(
            Vector2Int room,
            System.Random cellRandom,
            int ring,
            int capacity
        )
        {
            TryGetMegaCluster(room, out Vector2Int anchor, out _);
            double style = RoomRandom(anchor, MegaPopulationSalt).NextDouble();
            if (style < 0.10)
                return new RoomPopulation(0, false);
            if (style < 0.40)
                return new RoomPopulation(2 + cellRandom.Next(0, 3), false);
            if (style < 0.52)
                return new RoomPopulation(1 + cellRandom.Next(0, 2), true);
            if (style < 0.85)
                return new RoomPopulation(
                    Mathf.Min(capacity, 7 + cellRandom.Next(0, 4) + Mathf.Min(ring, 4)),
                    false
                );
            return new RoomPopulation(capacity - cellRandom.Next(0, 3), false);
        }

        private static RoomArchetype CreateArchetype(
            RoomShape shape,
            RoomTheme theme,
            EnvironmentTheme environment,
            int variant
        )
        {
            (float halfWidth, float halfDepth) = shape switch
            {
                RoomShape.GrandArena or RoomShape.MegaSection => (12f, 8.2f),
                RoomShape.Tiny => (2.8f, 2.8f),
                RoomShape.Compact => (4.7f, 4.7f),
                RoomShape.NarrowHorizontal => (10.2f, 2.8f),
                RoomShape.NarrowVertical => (2.8f, 8.2f),
                RoomShape.LargeSquare => (8.2f, 6.4f),
                RoomShape.LongHorizontal => (10.2f, 4.5f),
                RoomShape.LongVertical => (4.5f, 6.4f),
                _ => (10.2f, 6.4f),
            };
            return new RoomArchetype(shape, theme, environment, halfWidth, halfDepth, variant);
        }

        /// <summary>Health multiplier applied to enemies the deeper the player goes.</summary>
        public float EnemyHealthScale(Vector2Int room)
        {
            return 1f + 0.15f * Mathf.Max(0, Ring(room) - 1);
        }
    }
}
