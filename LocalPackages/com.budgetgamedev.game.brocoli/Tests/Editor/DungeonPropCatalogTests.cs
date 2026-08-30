using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Keeps the prop set the game asks for and the prop set it actually has from
    /// drifting apart.
    ///
    /// A theme names the props it wants, because no measurement can say a chair
    /// belongs beside a table. That one authored link is the only place the
    /// generator can still be wrong about a prop, and it used to fail in silence:
    /// a token matching nothing placed nothing and said nothing, which is how
    /// shrines and treasure vaults lost their pillars for several releases without
    /// a single test going red. These turn that into a failure.
    ///
    /// Nothing here lists a prop. The tokens come from
    /// <see cref="DungeonPropTokens.All"/> and the prefabs are read off disk, so
    /// adding, renaming, or replacing a prop is covered without editing this file.
    /// </summary>
    public sealed class DungeonPropCatalogTests
    {
        private const string ScenePath =
            "Packages/com.budgetgamedev.game.brocoli/Scenes/Brocoli_Dungeon.unity";
        private const string NamePrefix = "Dungeon";

        /// <summary>
        /// Every prop a theme can ask for exists. This is the gate that would have
        /// caught the missing column prefab the day it was deleted.
        /// </summary>
        [Test]
        public void EveryPropAThemeAsksForExists()
        {
            List<GameObject> prefabs = DungeonPropFixtures.AllPrefabs();
            foreach (string token in DungeonPropTokens.All)
            {
                Assert.That(
                    DungeonPropPlacer.ResolveProp(prefabs, token),
                    Is.Not.Null,
                    $"rooms ask for a \"{token}\" prop and the project has none, so every room "
                        + "asking for it is being built without it and nothing says so"
                );
            }
        }

        /// <summary>
        /// A token names the prop it means, not whichever prop happens to contain
        /// its letters first. Matching is by substring against an ordered list, so
        /// "Pot" would quietly become a potion if the two ever changed places -
        /// this fails on the swap rather than on someone noticing in a playthrough.
        /// </summary>
        [Test]
        public void EveryTokenNamesThePropItMeans()
        {
            List<GameObject> prefabs = DungeonPropFixtures.AllPrefabs();
            foreach (string token in DungeonPropTokens.All)
            {
                GameObject resolved = DungeonPropPlacer.ResolveProp(prefabs, token);
                Assert.That(resolved, Is.Not.Null, token);
                Assert.That(
                    resolved.name,
                    Is.EqualTo(NamePrefix + token),
                    $"the token \"{token}\" resolves to '{resolved.name}', which is a different "
                        + "prop that merely contains the same letters"
                );
            }
        }

        /// <summary>
        /// Every prop can be placed: it has to measure a footprint to be spaced by
        /// and a height to be faded against. A prefab that measures nothing is one
        /// whose art never imported, and it would place invisibly.
        /// </summary>
        [Test]
        public void EveryPropCanBeMeasured()
        {
            foreach (string token in DungeonPropTokens.All)
            {
                GameObject prefab = DungeonPropPlacer.ResolveProp(
                    DungeonPropFixtures.AllPrefabs(),
                    token
                );
                DungeonPropMeasurement measurement = DungeonPropMeasurement.Of(prefab);
                Assert.That(
                    measurement.Radius,
                    Is.GreaterThan(0f),
                    $"'{prefab.name}' takes up no floor, so it would be spaced as though it "
                        + "were not there"
                );
                Assert.That(
                    measurement.Height,
                    Is.GreaterThan(0f),
                    $"'{prefab.name}' measures no height, so it has nothing to fade against"
                );
            }
        }

        /// <summary>
        /// A prop's solid parts end up on the layer the camera and the projectiles
        /// search. A prop left on the default layer looks correct and is wrong in
        /// three systems at once, none of which complains.
        /// </summary>
        [Test]
        public void ASolidPropIsEnrolledOnTheOccluderLayer()
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            Assert.That(wallLayer, Is.GreaterThanOrEqualTo(0), "the project has no Wall layer");

            GameObject room = DungeonPropFixtures.RoomRoot();
            var host = new GameObject("PlacerHost");
            try
            {
                GameObject stray = DungeonPropFixtures.NovelProp(
                    room.transform,
                    Vector3.one,
                    Vector3.zero
                );
                foreach (Transform piece in stray.GetComponentsInChildren<Transform>())
                    piece.gameObject.layer = 0;

                List<GameObject> prefabs = DungeonPropFixtures.AllPrefabs();
                var layout = new DungeonLayout(7);
                var cell = new Vector2Int(2, 1);
                DungeonPropFixtures
                    .Placer(host, prefabs)
                    .BuildContents(
                        room.transform,
                        cell,
                        layout.Archetype(cell),
                        layout.RoomRandom(cell, 505),
                        null
                    );

                foreach (Collider collider in room.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || collider.transform.IsChildOf(stray.transform))
                        continue;
                    Assert.That(
                        collider.gameObject.layer,
                        Is.EqualTo(wallLayer),
                        $"'{collider.name}' was placed solid but off the occluder layer, so the "
                            + "camera cannot see it and projectiles pass through it"
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(room);
            }
        }

        /// <summary>
        /// The props the shipped scene registers are real. A reference to a deleted
        /// asset serializes as an empty slot that the placer skips in silence, so
        /// the scene can lose a prop without anything to show for it.
        /// </summary>
        [Test]
        public void TheShippedSceneRegistersOnlyRealProps()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                DungeonPropPlacer placer = FindPlacer(scene);
                Assert.That(
                    placer,
                    Is.Not.Null,
                    $"{ScenePath} holds no {nameof(DungeonPropPlacer)}"
                );

                var serialized = new SerializedObject(placer);
                SerializedProperty registered = serialized.FindProperty("propPrefabs");
                var prefabs = new List<GameObject>(registered.arraySize);
                for (int i = 0; i < registered.arraySize; i++)
                {
                    Object entry = registered.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(
                        entry,
                        Is.Not.Null,
                        $"{ScenePath} registers nothing in prop slot {i}, which is what a "
                            + "reference to a deleted prefab leaves behind"
                    );
                    prefabs.Add((GameObject)entry);
                }

                foreach (string token in DungeonPropTokens.All)
                {
                    Assert.That(
                        DungeonPropPlacer.ResolveProp(prefabs, token),
                        Is.Not.Null,
                        $"the shipped scene registers no prop matching \"{token}\", so rooms "
                            + "asking for it build without it"
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static DungeonPropPlacer FindPlacer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DungeonPropPlacer placer = root.GetComponentInChildren<DungeonPropPlacer>(true);
                if (placer != null)
                    return placer;
            }
            return null;
        }
    }
}
