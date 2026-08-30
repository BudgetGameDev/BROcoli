using UnityEditor;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>
    /// Resyncs build scenes when a game definition or scene is added, moved or
    /// deleted, which is exactly when installing or removing a game package
    /// changes the registry.
    /// </summary>
    public sealed class GameRegistryWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] movedTo,
            string[] movedFrom
        )
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo) || Touches(movedFrom))
            {
                GameCatalog.Invalidate();
                HubBuildScenes.Sync(false);
            }
        }

        private static bool Touches(string[] paths)
        {
            foreach (string path in paths)
                if (path.EndsWith(".unity") || path.Contains(GameDefinition.ResourceFolder))
                    return true;
            return false;
        }
    }
}
