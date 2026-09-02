namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// How an environment theme dresses the platform boundary. The structural
    /// facade underneath -- the parapet and the cliff courses that carry it below
    /// the floor -- is built by DungeonRoomBuilder on every boundary edge in every
    /// theme, so a style here only says what stands on top of that masonry.
    /// </summary>
    public enum DungeonBoundaryStyle
    {
        /// <summary>The structural masonry is the whole edge; nothing is added.</summary>
        MasonryRailing,

        /// <summary>A broken line of boundary props placed by DungeonPropPlacer.</summary>
        RockLine,

        /// <summary>
        /// The bare facade. The theme has no boundary props yet; assigning some
        /// means giving it a style and tokens here, not touching the generator.
        /// </summary>
        Undressed,
    }

    /// <summary>
    /// The authored profile of one broad environment: what its platform edges
    /// are built from and which props stand in for terrain rubble, clutter, and
    /// pathway dressing inside its rooms. Room themes ask the profile instead of
    /// naming props directly, so a cave never borrows dungeon carpentry and a
    /// dungeon never grows cave rocks. Themes whose real assets are still
    /// missing point at the closest neutral stand-ins; plugging in a new
    /// environment kit later means re-pointing one profile below.
    /// </summary>
    public readonly struct DungeonEnvironmentProfile
    {
        public readonly DungeonBoundaryStyle BoundaryStyle;

        /// <summary>Props alternated along a RockLine boundary edge.</summary>
        public readonly string[] BoundaryTokens;

        /// <summary>How many boundary props dress one RockLine room edge.</summary>
        public readonly int BoundaryPropsPerEdge;

        /// <summary>Terrain debris for collapsed and flooded ground.</summary>
        public readonly string[] RubbleTokens;

        /// <summary>Small container clutter grouped into clusters.</summary>
        public readonly string[] ClutterTokens;

        /// <summary>Props scattered on the shoulders of the travel route.</summary>
        public readonly string[] PathwayTokens;

        /// <summary>
        /// Whether diagonal galleries break their route with low rubble
        /// barriers made from <see cref="RubbleTokens"/>.
        /// </summary>
        public readonly bool UsesRubbleBarriers;

        private DungeonEnvironmentProfile(
            DungeonBoundaryStyle boundaryStyle,
            string[] boundaryTokens,
            int boundaryPropsPerEdge,
            string[] rubbleTokens,
            string[] clutterTokens,
            string[] pathwayTokens,
            bool usesRubbleBarriers
        )
        {
            BoundaryStyle = boundaryStyle;
            BoundaryTokens = boundaryTokens;
            BoundaryPropsPerEdge = boundaryPropsPerEdge;
            RubbleTokens = rubbleTokens;
            ClutterTokens = clutterTokens;
            PathwayTokens = pathwayTokens;
            UsesRubbleBarriers = usesRubbleBarriers;
        }

        private static readonly string[] Rocks =
        {
            DungeonPropTokens.Rocks,
            DungeonPropTokens.Stones,
        };
        private static readonly string[] Carpentry =
        {
            DungeonPropTokens.WoodSupport,
            DungeonPropTokens.WoodStructure,
            DungeonPropTokens.Pot,
        };
        private static readonly string[] StonesAndPots =
        {
            DungeonPropTokens.Stones,
            DungeonPropTokens.Pot,
        };
        private static readonly string[] BarrelsAndPots =
        {
            DungeonPropTokens.Barrel,
            DungeonPropTokens.Pot,
        };
        private static readonly string[] DungeonPathway =
        {
            DungeonPropTokens.Pot,
            DungeonPropTokens.Barrel,
            DungeonPropTokens.WoodSupport,
        };
        private static readonly string[] RockPathway =
        {
            DungeonPropTokens.Rocks,
            DungeonPropTokens.Stones,
            DungeonPropTokens.Pot,
        };

        private static readonly DungeonEnvironmentProfile Dungeon = new(
            DungeonBoundaryStyle.MasonryRailing,
            System.Array.Empty<string>(),
            0,
            Carpentry,
            BarrelsAndPots,
            DungeonPathway,
            false
        );

        private static readonly DungeonEnvironmentProfile Cave = new(
            DungeonBoundaryStyle.RockLine,
            Rocks,
            3,
            Rocks,
            StonesAndPots,
            RockPathway,
            true
        );

        private static readonly DungeonEnvironmentProfile Mountain = new(
            DungeonBoundaryStyle.RockLine,
            Rocks,
            3,
            Rocks,
            StonesAndPots,
            RockPathway,
            true
        );

        // Plains, forest, and desert are placeholders until their environment
        // kits are acquired: the structural boundary facade undressed, and
        // neutral interior props.
        private static readonly DungeonEnvironmentProfile Plains = new(
            DungeonBoundaryStyle.Undressed,
            System.Array.Empty<string>(),
            0,
            Carpentry,
            BarrelsAndPots,
            DungeonPathway,
            false
        );

        private static readonly DungeonEnvironmentProfile Forest = new(
            DungeonBoundaryStyle.Undressed,
            System.Array.Empty<string>(),
            0,
            Carpentry,
            BarrelsAndPots,
            DungeonPathway,
            false
        );

        private static readonly DungeonEnvironmentProfile Desert = new(
            DungeonBoundaryStyle.Undressed,
            System.Array.Empty<string>(),
            0,
            StonesAndPots,
            StonesAndPots,
            StonesAndPots,
            false
        );

        public static DungeonEnvironmentProfile Of(DungeonLayout.EnvironmentTheme theme)
        {
            return theme switch
            {
                DungeonLayout.EnvironmentTheme.Cave => Cave,
                DungeonLayout.EnvironmentTheme.Plains => Plains,
                DungeonLayout.EnvironmentTheme.Forest => Forest,
                DungeonLayout.EnvironmentTheme.Mountain => Mountain,
                DungeonLayout.EnvironmentTheme.Desert => Desert,
                _ => Dungeon,
            };
        }
    }
}
