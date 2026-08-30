using System.Linq;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>Finds the loaded scene's pause screen, if it has one.</summary>
    public static class PauseControllerLocator
    {
        /// <summary>
        /// Returns the active <see cref="IPauseController"/>, or null in scenes
        /// that have none. Menus deliberately have none: pausing there would
        /// freeze the very UI needed to resume.
        /// </summary>
        public static IPauseController Find() =>
            Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IPauseController>()
                .FirstOrDefault();
    }
}
