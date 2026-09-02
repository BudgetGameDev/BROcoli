using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Tells the shared pointer what BROcoli's screens are, and what its pointer looks like.
    ///
    /// The rule the player experiences is simple: a screen you click at keeps the pointer, and
    /// during play the pointer appears when you move the mouse and withdraws again shortly
    /// after. Everything specific to this game -- which screens those are, and which image the
    /// pointer wears -- is decided here, so the shared layer stays free of BROcoli's inventory,
    /// its map, and its dungeon.
    /// </summary>
    internal static class BrocoliPointer
    {
        private const string MainMenuScene = "Brocoli_MainMenu";
        private const string DungeonScene = "Brocoli_Dungeon";

        /// <summary>
        /// The pointers this game ships art for. Both come from Kenney's CC0 cursor pack, whose
        /// whole 182-cursor set is kept under the package's <c>Cursors~/</c> folder;
        /// <c>scripts/select-cursor.sh</c> promotes any other cursor from there into one of
        /// these two slots, which is what keeps swapping the pointer a small job later.
        /// </summary>
        internal enum PointerStyle
        {
            /// <summary>A plain arrow.</summary>
            SteelArrow,

            /// <summary>An armoured fist, pointing.</summary>
            Gauntlet,
        }

        /// <summary>
        /// The pointer the game wears. Switching styles is this one line: both images are
        /// already imported and shipped.
        /// </summary>
        internal const PointerStyle ActiveStyle = PointerStyle.SteelArrow;

        /// <summary>
        /// The source art is white with a black outline; this drops the lit part to a cold
        /// steel that still reads against the dungeon's warm torchlight, while leaving the
        /// outline dark enough to stay legible over the map's pale panels.
        /// </summary>
        private static readonly Color SteelTint = new(0.78f, 0.86f, 1f);

        private static bool registered;

        /// <summary>
        /// The art for a style. Where each one points is measured from its own image, so a
        /// style is nothing but a file name and a tint.
        /// </summary>
        internal static GameCursor.PointerArt ArtFor(PointerStyle style) =>
            style switch
            {
                PointerStyle.Gauntlet => new GameCursor.PointerArt(
                    "Brocoli/Cursors/PointerGauntlet",
                    SteelTint
                ),
                _ => new GameCursor.PointerArt("Brocoli/Cursors/PointerSteel", SteelTint),
            };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            registered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRegistered(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRegistered(scene.name);
        }

        /// <summary>
        /// Brings the pointer up the first time a BROcoli scene loads. The hub's launcher and
        /// any other game keep whatever pointer they set for themselves.
        /// </summary>
        internal static void EnsureRegistered(string sceneName)
        {
            if (registered || (sceneName != MainMenuScene && sceneName != DungeonScene))
                return;

            registered = true;
            GameCursor.EnsurePresent();
            GameCursor.SetArt(ArtFor(ActiveStyle));
            GameCursor.AddVisibilityHold(HoldsPointer);
        }

        /// <summary>
        /// The screens BROcoli is played with a mouse on. Each is a cheap static read, because
        /// this is polled every frame the pointer is decided on.
        /// </summary>
        internal static bool HoldsPointer() =>
            MainMenu.AnyOpen
            || PauseMenu.AnyPaused
            || ExplorationOverlay.AnyOpen
            || LevelUpScreen.AnyShowing
            || GameOverOverlay.AnyVisible;
    }
}
