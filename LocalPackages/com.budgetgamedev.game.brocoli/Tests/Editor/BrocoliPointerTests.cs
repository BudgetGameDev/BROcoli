using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class BrocoliPointerTests
    {
        /// <summary>
        /// Every screen the pointer asks about answers from a static field, and a test earlier
        /// in the run can leave one pointing at an object it never destroyed. Clearing them is
        /// what the domain reload does for a real session; here it has to be asked for.
        /// </summary>
        [SetUp]
        public void ForgetScreensLeftBehindByOtherTests()
        {
            ClearStatic(typeof(MainMenu), "active");
            ClearStatic(typeof(PauseMenu), "active");
            ClearStatic(typeof(LevelUpScreen), "active");
            ClearStatic(typeof(GameOverOverlay), "active");
            ClearStatic(typeof(ExplorationOverlay), "instance");

            Assume.That(
                BrocoliPointer.HoldsPointer(),
                Is.False,
                "the fixture starts with no screen open"
            );
        }

        private static void ClearStatic(System.Type owner, string fieldName)
        {
            owner
                .GetField(
                    fieldName,
                    System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.NonPublic
                )
                ?.SetValue(null, null);
        }

        [Test]
        public void NothingHoldsThePointerWhileTheDungeonIsJustBeingPlayed()
        {
            Assert.That(
                BrocoliPointer.HoldsPointer(),
                Is.False,
                "with no screen open the pointer is left to the reveal timer"
            );
        }

        [Test]
        public void TheInventoryAndMapHoldThePointer()
        {
            Canvas canvas = BuildCanvas();
            // The overlay refuses to open on a stopped clock, and edit mode is one.
            float timeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                ExplorationOverlay overlay = canvas.gameObject.AddComponent<ExplorationOverlay>();
                // Edit mode never calls Awake, and the overlay builds its interface there.
                typeof(ExplorationOverlay)
                    .GetMethod(
                        "Awake",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(overlay, null);

                Assert.That(ExplorationOverlay.AnyOpen, Is.False);
                Assert.That(BrocoliPointer.HoldsPointer(), Is.False);

                // The overlay opens through the same path the I key drives.
                overlay.ProcessGlobalInput(false, true, false, false, false, false);
                Assert.That(ExplorationOverlay.AnyOpen, Is.True);
                Assert.That(
                    BrocoliPointer.HoldsPointer(),
                    Is.True,
                    "the inventory is clicked, so the pointer must not time out under it"
                );

                overlay.Close();
                Assert.That(BrocoliPointer.HoldsPointer(), Is.False);
            }
            finally
            {
                Time.timeScale = timeScale;
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        /// <summary>
        /// The game-over screen and the level-up screen both bring themselves up from play
        /// mode only, so what is checked here is that the pointer asks them at all. Each
        /// answers false while it is not showing, which is the state an edit-mode test can
        /// put them in; the wiring is the part that would silently go missing.
        /// </summary>
        [Test]
        public void ThePointerAsksEveryClickDrivenScreen()
        {
            GameObject host = new("Screens");
            try
            {
                host.AddComponent<LevelUpScreen>();
                Assert.That(LevelUpScreen.AnyShowing, Is.False);
                Assert.That(GameOverOverlay.AnyVisible, Is.False);
                Assert.That(BrocoliPointer.HoldsPointer(), Is.False);

                string source = System.IO.File.ReadAllText(
                    "Packages/com.budgetgamedev.game.brocoli/Runtime/Input/BrocoliPointer.cs"
                );
                foreach (
                    string screen in new[]
                    {
                        "MainMenu.AnyOpen",
                        "PauseMenu.AnyPaused",
                        "ExplorationOverlay.AnyOpen",
                        "LevelUpScreen.AnyShowing",
                        "GameOverOverlay.AnyVisible",
                    }
                )
                {
                    Assert.That(
                        source,
                        Does.Contain(screen),
                        $"{screen} is a screen the player clicks at, so the pointer has to "
                            + "stay up while it is open"
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThePointerIsOnlyBroughtUpInThisGamesScenes()
        {
            System.Reflection.FieldInfo registered = typeof(BrocoliPointer).GetField(
                "registered",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
            );
            bool wasRegistered = (bool)registered.GetValue(null);
            try
            {
                registered.SetValue(null, false);

                BrocoliPointer.EnsureRegistered("GameLauncher");
                Assert.That(
                    (bool)registered.GetValue(null),
                    Is.False,
                    "the hub's launcher keeps whatever pointer it set for itself"
                );

                BrocoliPointer.EnsureRegistered("Brocoli_Dungeon");
                Assert.That((bool)registered.GetValue(null), Is.True);
            }
            finally
            {
                registered.SetValue(null, wasRegistered);
            }
        }

        [Test]
        public void EveryStyleNamesArtTheGameActuallyShips()
        {
            foreach (
                BrocoliPointer.PointerStyle style in System.Enum.GetValues(
                    typeof(BrocoliPointer.PointerStyle)
                )
            )
            {
                var art = BrocoliPointer.ArtFor(style);
                Assert.That(art.IsEmpty, Is.False, style.ToString());
                Assert.That(
                    Resources.Load<Texture2D>(art.PointerResource),
                    Is.Not.Null,
                    $"{style} names {art.PointerResource}, which is not in Resources; "
                        + "switching to it would leave the player with the system pointer"
                );
            }
        }

        [Test]
        public void TheShippedPointerIsReadableSoItCanBeRecoloured()
        {
            var art = BrocoliPointer.ArtFor(BrocoliPointer.ActiveStyle);
            Texture2D pointer = Resources.Load<Texture2D>(art.PointerResource);
            Assert.That(pointer, Is.Not.Null);
            Assert.That(
                pointer.isReadable,
                Is.True,
                "the tint is applied by reading the pixels, so an unreadable import would "
                    + "ship the pointer in its source colour"
            );
            Assert.That(art.Tint, Is.Not.EqualTo(Color.white));
        }

        private static Canvas BuildCanvas()
        {
            GameObject host = new(
                "Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster)
            );
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
