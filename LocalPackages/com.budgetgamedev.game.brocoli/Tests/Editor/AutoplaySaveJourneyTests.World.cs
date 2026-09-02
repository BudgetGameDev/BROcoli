using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Step = BudgetGameDev.Games.Brocoli.AutoplaySaveJourneyDirector.Step;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The world the journey is driven against. Only the two scenes and the autosave
    /// controller are stood in for; claiming a slot, writing it, reading it back,
    /// resuming it, and dropping it on death are all the game's own save system, which
    /// is what makes these tests say something about the save slots.
    /// </summary>
    public sealed partial class AutoplaySaveJourneyTests
    {
        /// <summary>
        /// Stands the director up the way a run does: counting the free slots before
        /// the menu has claimed one, and only then starting the first character.
        /// </summary>
        private void StartJourney()
        {
            BuildDirector();
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            live = FreshRun(FirstSeed);
            scene = AutoplaySessionDirector.DungeonScene;
        }

        private void BuildDirector()
        {
            host = new GameObject("Save journey");
            director = host.AddComponent<AutoplaySaveJourneyDirector>();
            scene = AutoplaySessionDirector.MenuScene;
            WireWorld();
            Invoke(director, "Start");
        }

        /// <summary>
        /// Stands in for the two scenes and for the autosave controller. Everything
        /// underneath -- claiming a slot, writing it, reading it back, resuming it,
        /// dropping it on death -- is the game's own save system.
        /// </summary>
        private void WireWorld()
        {
            AutoplaySaveJourneyDirector.Surroundings world = director.World;
            world.ActiveScene = () => scene;
            world.GameDelta = () => 1f;
            world.Checkpoint = CheckpointTheLiveRun;
            world.CaptureLiveRun = (out BrocoliRunSave save) =>
            {
                save = Clone(live);
                return true;
            };
            world.QuitToMenu = () =>
            {
                // Leaving through the pause menu checkpoints on the way out.
                CheckpointTheLiveRun();
                scene = AutoplaySessionDirector.MenuScene;
                return true;
            };
            world.StartAnotherRun = () =>
            {
                if (!BrocoliSaveSystem.BeginNewGame(false))
                    return false;
                live = FreshRun(SecondSeed);
                scene = AutoplaySessionDirector.DungeonScene;
                return true;
            };
            world.ResumeRun = slot =>
            {
                if (!BrocoliSaveSystem.BeginContinue(slot))
                    return false;
                BrocoliSaveSystem.TryGetPendingContinue(out BrocoliRunSave resumed);
                live = Clone(resumed);
                BrocoliSaveSystem.FinishContinue();
                scene = AutoplaySessionDirector.DungeonScene;
                return true;
            };
            world.Die = () =>
            {
                BrocoliSaveSystem.DeleteActiveSave();
                return true;
            };
        }

        private static void ExpectTheJourneyToFinish() =>
            LogAssert.Expect(
                LogType.Log,
                "[Autoplay] The save journey went through every step it set out to."
            );

        /// <summary>What the autosave controller does on a timer and on the way out.</summary>
        private void CheckpointTheLiveRun() => BrocoliSaveSystem.Save(Clone(live));

        private void Pump()
        {
            for (int frame = 0; frame < Frames && director.Current != Step.Done; frame++)
                director.RunStep();
        }

        private const int FirstSeed = 4242;
        private const int SecondSeed = 9797;
        private const int PlayersOwnSeed = 1234;

        private static BrocoliRunSave Clone(BrocoliRunSave save) =>
            JsonUtility.FromJson<BrocoliRunSave>(JsonUtility.ToJson(save));

        /// <summary>
        /// A run partway through being played. The seed decides everything a resume
        /// has to bring back, so two characters built here are distinguishable in every
        /// field the journey compares.
        /// </summary>
        private static BrocoliRunSave FreshRun(int seed)
        {
            int scale = seed % 7 + 1;
            return new BrocoliRunSave
            {
                playerPosition = new Vector3(scale * 3f, 0f, scale * -2f),
                player = new BrocoliPlayerSave
                {
                    health = 40f + scale,
                    maxHealth = 100f,
                    attackSpeed = 0.6f,
                    damage = 8f,
                    movementSpeed = 4f,
                    experience = 10f + scale,
                    maxExperience = 30f,
                    level = 1f + scale,
                    detectionRadius = 12f,
                    sprayRange = 4f,
                    sprayWidth = 20f,
                    sprayDamageMultiplier = 1f,
                    critChance = 5f,
                    critDamage = 1.5f,
                },
                game = new BrocoliGameStateSave
                {
                    score = 100 + scale,
                    gameTime = 60f,
                    enemiesKilled = scale,
                },
                dungeon = new BrocoliDungeonSave { seed = seed, roomsVisited = scale },
            };
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, Members).Invoke(target, null);

        /// <summary>Calls one of the presses the journey makes for itself.</summary>
        private static object Call(object target, string name, params object[] arguments) =>
            target
                .GetType()
                .GetMethods(Members | BindingFlags.Static)
                .Single(method =>
                    method.Name == name && method.GetParameters().Length == arguments.Length
                )
                .Invoke(target, arguments);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Members).SetValue(target, value);
    }
}
