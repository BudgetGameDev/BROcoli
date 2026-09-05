using System;
using System.Collections.Generic;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Scaffolding shared by the launcher tests.
    /// </summary>
    /// <remarks>
    /// Three things make the launcher awkward outside play mode, and each is dealt
    /// with once here: Awake and Start never run, so the build is driven by hand;
    /// the statics that the runtime resets on entering play mode have to be reset
    /// per test instead; and a scene load does not exist, so scene names are
    /// recorded rather than opened.
    /// </remarks>
    public abstract class GameLauncherFixture
    {
        protected readonly HubTestGames Games = new();

        /// <summary>Scenes the launcher asked for, newest last.</summary>
        protected readonly List<string> LoadedScenes = new();

        private readonly List<GameObject> spawned = new();
        private readonly List<UnityEngine.Object> assets = new();
        private string previousLastPlayed;

        [SetUp]
        public void ResetHubStatics()
        {
            previousLastPlayed = GameSession.LastPlayedId;
            PlayerPrefs.SetString(GameSession.LastPlayedKey, string.Empty);
            GameSession.ResetSessionState();
            GameCatalog.Invalidate();
            LoadedScenes.Clear();
            GameSession.SceneLoader = LoadedScenes.Add;
        }

        [TearDown]
        public void DestroyWhatTheTestBuilt()
        {
            foreach (GameObject host in spawned)
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            spawned.Clear();

            foreach (UnityEngine.Object asset in assets)
                if (asset != null)
                    UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();

            GameCatalog.Invalidate();
            Games.DestroyAll();
            GameSession.ResetSessionState();
            GameAudioSettings.Configure(null, null);
            Time.timeScale = 1f;
            PlayerPrefs.SetString(GameSession.LastPlayedKey, previousLastPlayed);
        }

        /// <summary>A launcher component whose interface has not been built yet.</summary>
        protected GameLauncher NewLauncher()
        {
            var host = new GameObject(nameof(GameLauncher));
            spawned.Add(host);
            return host.AddComponent<GameLauncher>();
        }

        /// <summary>A launcher showing exactly these games, already populated.</summary>
        protected GameLauncher LauncherListing(params GameDefinition[] games)
        {
            GameCatalog.cached = games;
            GameLauncher launcher = NewLauncher();
            Build(launcher);
            launcher.Populate();
            launcher.RestoreSelection();
            return launcher;
        }

        protected void Build(GameLauncher launcher) => Track(launcher.BuildInterface);

        protected void StartLauncher(GameLauncher launcher) => Track(launcher.Start);

        protected EventSystem NewEventSystem()
        {
            var host = new GameObject(nameof(EventSystem));
            spawned.Add(host);
            return host.AddComponent<EventSystem>();
        }

        /// <summary>A throwaway sprite, for the icon a registry entry may carry.</summary>
        protected Sprite NewIcon()
        {
            var texture = new Texture2D(4, 4);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            assets.Add(texture);
            assets.Add(icon);
            return icon;
        }

        /// <summary>
        /// Turns the automatic layout off so a test can place a row exactly where it
        /// wants it. Every row is put back at the top of the list first, so the only
        /// row out of view is the one the test moves.
        /// </summary>
        protected static void FreezeLayout(GameLauncher launcher)
        {
            RectTransform content = launcher.ListScroll.content;
            content.GetComponent<VerticalLayoutGroup>().enabled = false;
            content.GetComponent<ContentSizeFitter>().enabled = false;
            content.anchoredPosition = Vector2.zero;

            foreach (GameLauncher.GameEntry entry in launcher.Entries)
            {
                var row = (RectTransform)entry.Button.transform;
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, 76f);
                row.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// Runs a build step and records whatever it added to the open scene, so the
        /// canvas the launcher borrows or creates does not outlive the test.
        /// </summary>
        private void Track(Action build)
        {
            Canvas before = ScreenCanvasLocator.Find();
            int childrenBefore = before == null ? 0 : before.transform.childCount;
            bool hadEventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null;

            build();

            Canvas canvas = ScreenCanvasLocator.Find();
            if (canvas == null)
                return;

            if (before == null)
                spawned.Add(canvas.gameObject);
            else
                for (int index = childrenBefore; index < canvas.transform.childCount; index++)
                    spawned.Add(canvas.transform.GetChild(index).gameObject);

            if (hadEventSystem)
                return;

            EventSystem created = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (created != null)
                spawned.Add(created.gameObject);
        }
    }
}
