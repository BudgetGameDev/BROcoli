using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Whether the camera can see a prop it was never told about, and whether it
    /// lowers it the way it lowers a wall.
    ///
    /// The rest of the visibility suite reasons about a hand-written model of two
    /// occluder kinds, so it cannot answer either question. These tests build real
    /// objects instead, and the ones that matter most are objects that match no
    /// asset in the project: if the system handles a shape it has never seen, it
    /// handles the prop somebody adds after this was written.
    /// </summary>
    public sealed class DungeonPropOcclusionTests
    {
        private const float BaseFraction = 0.45f;
        private const float FeatherFraction = 0.12f;

        /// <summary>A player-sized character for the cut to be measured against.</summary>
        private const float CharacterHeight = 2.2f;

        private GameObject room;

        [SetUp]
        public void SetUp()
        {
            room = DungeonPropFixtures.RoomRoot();
        }

        [TearDown]
        public void TearDown()
        {
            if (room != null)
                Object.DestroyImmediate(room);
        }

        /// <summary>
        /// The whole point: a prop nothing in the codebase knows about is adopted
        /// as an occluder the first time a sight line reaches it. Nobody registered
        /// it, named it, or tagged it.
        /// </summary>
        [Test]
        public void APropTheSystemHasNeverSeenIsAdoptedAsAnOccluder()
        {
            GameObject prop = DungeonPropFixtures.NovelProp(
                room.transform,
                new Vector3(1.3f, 2.9f, 0.8f),
                new Vector3(4f, 0f, -2f)
            );

            DungeonOccluder occluder = DungeonOccluder.Owning(
                prop.GetComponentInChildren<Collider>()
            );

            Assert.That(
                occluder,
                Is.Not.Null,
                "a solid prop standing in a room is invisible to the camera system, so it would "
                    + "hide the player with nothing lowering it"
            );
            Assert.That(
                DungeonOccluder.ForGroup(occluder.GroupId),
                Is.SameAs(occluder),
                "the adopted prop never registered its visibility group, so the fader could "
                    + "select it and then find nothing to fade"
            );
        }

        /// <summary>
        /// A prop is its own object. If the search for what one occluder is ran
        /// past it, a barrel standing in front of the player would take the whole
        /// room down with it.
        /// </summary>
        [Test]
        public void EachPropIsItsOwnOccluderRatherThanTheRoomAroundIt()
        {
            GameObject first = DungeonPropFixtures.NovelProp(
                room.transform,
                new Vector3(1f, 2f, 1f),
                new Vector3(3f, 0f, 0f)
            );
            GameObject second = DungeonPropFixtures.NovelProp(
                room.transform,
                new Vector3(0.6f, 1.8f, 0.6f),
                new Vector3(-3f, 0f, 0f)
            );

            DungeonOccluder a = DungeonOccluder.Owning(first.GetComponentInChildren<Collider>());
            DungeonOccluder b = DungeonOccluder.Owning(second.GetComponentInChildren<Collider>());

            Assert.That(a.transform, Is.SameAs(first.transform), "an occluder swallowed the room");
            Assert.That(b.transform, Is.SameAs(second.transform), "an occluder swallowed the room");
            Assert.That(
                a.GroupId,
                Is.Not.EqualTo(b.GroupId),
                "two unrelated props share one visibility group, so hiding behind one would fade "
                    + "the other across the room"
            );
        }

        /// <summary>
        /// An eligible tall prop gives way the same way a wall does: measured off
        /// itself, keeping its base and losing its top. Low-profile props are covered
        /// separately because they deliberately never enter this fade path.
        /// </summary>
        [Test]
        public void AnUnknownPropKeepsItsBaseAndLosesItsTop(
            [Values(1.75f, 2.6f, 4.2f)] float height
        )
        {
            GameObject prop = DungeonPropFixtures.NovelProp(
                room.transform,
                new Vector3(1.1f, height, 1.1f),
                new Vector3(2f, 0f, 1f)
            );
            Renderer renderer = prop.GetComponentInChildren<Renderer>();
            DungeonOccluder occluder = DungeonOccluder.Owning(
                prop.GetComponentInChildren<Collider>()
            );

            Assert.That(
                occluder.TryGetFadeReference(renderer, out float minimumY, out float measured),
                "the prop could not be measured, so it has no cutoff to fade to"
            );
            Assert.That(measured, Is.EqualTo(height).Within(0.05f), "the prop was mismeasured");

            OcclusionFadeProfile profile = OcclusionFadeProfile.For(
                minimumY,
                measured,
                CharacterHeight,
                BaseFraction,
                FeatherFraction
            );
            Assert.That(
                profile.IsSolidWhenLowered(minimumY + measured * 0.1f),
                $"a {height:0.0}-tall prop loses its base when lowered, so it would read as "
                    + "floating instead of as lowered"
            );
            Assert.That(
                profile.CoverageAt(minimumY + measured, 1f),
                Is.LessThan(0.05f),
                $"a {height:0.0}-tall prop keeps its top when lowered, so it would go on hiding "
                    + "whoever is standing behind it"
            );
        }

        /// <summary>
        /// Everything tall enough to hide a character has to be something the
        /// camera can lower and something that keeps its base. Low-profile props
        /// deliberately stay visible and are excluded from automatic adoption.
        /// </summary>
        [Test]
        public void EveryEligibleSolidPieceARoomBuildsCanBeLoweredAndKeepsItsBase()
        {
            var host = new GameObject("BuilderHost");
            try
            {
                List<GameObject> prefabs = DungeonPropFixtures.AllPrefabs();
                var layout = new DungeonLayout(20260830);
                var cell = new Vector2Int(1, 1);
                var archetype = new DungeonLayout.RoomArchetype(
                    DungeonLayout.RoomShape.DiagonalGallery,
                    DungeonLayout.RoomTheme.Sparse,
                    DungeonLayout.EnvironmentTheme.Cave,
                    10.2f,
                    6.4f,
                    0
                );

                DungeonPropFixtures.Builder(host).BuildInterior(room.transform, cell, archetype);
                DungeonPropFixtures
                    .Placer(host, prefabs)
                    .BuildContents(
                        room.transform,
                        cell,
                        archetype,
                        layout.RoomRandom(cell, 505),
                        null
                    );

                List<Renderer> pieces = DungeonPropFixtures.OccludingRenderers(room);
                Assert.That(pieces, Is.Not.Empty, "the room built nothing solid to test");

                foreach (Renderer piece in pieces)
                {
                    DungeonOccluder occluder = DungeonOccluder.Owning(piece);
                    if (occluder == null)
                    {
                        Assert.That(
                            piece.bounds.size.y,
                            Is.LessThan(DungeonOccluder.MinimumAutomaticFadeHeight),
                            $"'{Path(piece.transform)}' is tall enough to hide the player but "
                                + "was excluded from the camera visibility system"
                        );
                        continue;
                    }
                    Assert.That(
                        occluder,
                        Is.Not.Null,
                        $"'{Path(piece.transform)}' is solid and on screen but belongs to no "
                            + "occluder, so it can hide the player with nothing lowering it"
                    );
                    Assert.That(
                        occluder.TryGetFadeReference(piece, out float minimumY, out float height),
                        $"'{Path(piece.transform)}' has no measurable height to fade against"
                    );

                    OcclusionFadeProfile profile = OcclusionFadeProfile.For(
                        minimumY,
                        height,
                        CharacterHeight,
                        BaseFraction,
                        FeatherFraction
                    );
                    Assert.That(
                        profile.IsSolidWhenLowered(minimumY),
                        $"'{Path(piece.transform)}' vanishes entirely when lowered instead of "
                            + "keeping its base"
                    );
                    Assert.That(
                        profile.CoverageAt(minimumY + height, 1f),
                        Is.LessThan(0.05f),
                        $"'{Path(piece.transform)}' keeps its top when lowered"
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Adopting props must not break the grouping walls rely on: a built run
        /// still fades as one wall rather than as a pile of separate slabs.
        /// </summary>
        [Test]
        public void ABuiltWallRunStillFadesAsOneOccluder()
        {
            var host = new GameObject("BuilderHost");
            try
            {
                DungeonPropFixtures
                    .Builder(host)
                    .BuildEdge(
                        room.transform,
                        new DungeonEdge(0, 0, true),
                        new DungeonPassage(false, 0, 0)
                    );

                var groups = new HashSet<int>();
                foreach (Collider collider in room.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger)
                        continue;
                    DungeonOccluder occluder = DungeonOccluder.Owning(collider);
                    Assert.That(occluder, Is.Not.Null, Path(collider.transform));
                    groups.Add(occluder.GroupId);
                }

                Assert.That(
                    groups,
                    Has.Count.EqualTo(1),
                    "one wall run resolved to several visibility groups, so part of it would "
                        + "lower while the rest stayed standing"
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static string Path(Transform piece)
        {
            return piece.parent == null ? piece.name : $"{Path(piece.parent)}/{piece.name}";
        }
    }
}
