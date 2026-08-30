using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BrocoliSaveSystemTests
    {
        [Test]
        public void CheckpointRoundTripsEveryTopLevelStateGroup()
        {
            BrocoliRunSave original = CreateValidSave();
            original.mobileControls = true;
            original.playerPosition = new Vector3(31f, 0.5f, -18f);
            original.game.score = 42;
            original.dungeon.rooms.Add(
                new BrocoliRoomSave
                {
                    x = 2,
                    y = -1,
                    visited = true,
                    openedChestSlots = new System.Collections.Generic.List<int> { 1, 4 },
                }
            );

            bool loaded = BrocoliSaveSystem.TryDeserialize(
                BrocoliSaveSystem.Serialize(original),
                out BrocoliRunSave restored
            );

            Assert.That(loaded, Is.True);
            Assert.That(restored.mobileControls, Is.True);
            Assert.That(restored.playerPosition, Is.EqualTo(original.playerPosition));
            Assert.That(restored.player.health, Is.EqualTo(original.player.health));
            Assert.That(restored.game.score, Is.EqualTo(42));
            Assert.That(restored.dungeon.seed, Is.EqualTo(original.dungeon.seed));
            Assert.That(restored.dungeon.rooms[0].openedChestSlots, Is.EqualTo(new[] { 1, 4 }));
        }

        [Test]
        public void DeadPlayerCheckpointIsNotResumable()
        {
            BrocoliRunSave save = CreateValidSave();
            save.player.health = 0f;

            Assert.That(BrocoliSaveSystem.IsValid(save), Is.False);
        }

        [Test]
        public void DifferentSchemaVersionIsNotResumable()
        {
            BrocoliRunSave save = CreateValidSave();
            save.version++;

            Assert.That(BrocoliSaveSystem.IsValid(save), Is.False);
        }

        [Test]
        public void MalformedJsonIsNotResumable()
        {
            Assert.That(BrocoliSaveSystem.TryDeserialize("{not json", out _), Is.False);
        }

        [Test]
        public void ARunThatCouldNotBeMovedIsNotResumable()
        {
            BrocoliRunSave save = CreateValidSave();
            save.player.movementSpeed = 0f;

            Assert.That(BrocoliSaveSystem.IsValid(save), Is.False);
        }

        [Test]
        public void EarnedTradeOffStatsRemainResumable()
        {
            BrocoliRunSave save = CreateValidSave();
            save.player.damage = -3f;
            save.player.armor = -2f;

            Assert.That(BrocoliSaveSystem.IsValid(save), Is.True);
        }

        [Test]
        public void CorruptTemporaryBoostIsNotResumable()
        {
            BrocoliRunSave save = CreateValidSave();
            save.player.temporaryBoosts.Add(
                new BrocoliTemporaryBoostSave
                {
                    type = TemporaryBoostType.Damage,
                    amount = 2f,
                    remainingTime = float.NaN,
                }
            );

            Assert.That(BrocoliSaveSystem.IsValid(save), Is.False);
        }

        private static BrocoliRunSave CreateValidSave()
        {
            return new BrocoliRunSave
            {
                playerPosition = new Vector3(0f, 0.5f, 0f),
                player = new BrocoliPlayerSave
                {
                    health = 75f,
                    maxHealth = 100f,
                    attackSpeed = 0.6f,
                    damage = 8f,
                    movementSpeed = 4f,
                    experience = 12f,
                    maxExperience = 30f,
                    level = 1f,
                    detectionRadius = 12f,
                    sprayRange = 4f,
                    sprayWidth = 20f,
                    sprayDamageMultiplier = 1f,
                    critChance = 5f,
                    critDamage = 1.5f,
                    dodgeChance = 0f,
                    armor = 0f,
                    healthRegen = 0f,
                    lifeSteal = 0f,
                },
                game = new BrocoliGameStateSave
                {
                    score = 10,
                    gameTime = 9f,
                    enemiesKilled = 2,
                },
                dungeon = new BrocoliDungeonSave { seed = 12345, roomsVisited = 1 },
            };
        }
    }
}
