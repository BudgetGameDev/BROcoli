using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// The set of games present in this build.
    /// </summary>
    /// <remarks>
    /// Discovery is by Resources scan rather than a checked-in list, because the
    /// list has to survive a game package being added to or dropped from
    /// <c>Packages/manifest.json</c>. A dropped package takes its Resources folder
    /// with it, so it simply stops appearing — no central file to forget to edit.
    /// </remarks>
    public static class GameCatalog
    {
        private static GameDefinition[] cached;

        /// <summary>Registered games, ordered as the launcher should list them.</summary>
        public static IReadOnlyList<GameDefinition> All => cached ??= Load();

        /// <summary>Drops the cache so a newly imported game shows up in play mode.</summary>
        public static void Invalidate() => cached = null;

        public static GameDefinition Find(string id) =>
            All.FirstOrDefault(game =>
                string.Equals(game.Id, id, System.StringComparison.OrdinalIgnoreCase)
            );

        private static GameDefinition[] Load()
        {
            GameDefinition[] found = Resources.LoadAll<GameDefinition>(
                GameDefinition.ResourceFolder
            );

            foreach (
                IGrouping<string, GameDefinition> duplicate in found
                    .GroupBy(game => game.Id, System.StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
            )
                Debug.LogError(
                    $"[GameCatalog] Duplicate game id '{duplicate.Key}' in: "
                        + string.Join(", ", duplicate.Select(game => game.name))
                );

            return found
                .OrderBy(game => game.SortOrder)
                .ThenBy(game => game.DisplayName, System.StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }
}
