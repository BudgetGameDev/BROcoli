#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonManager
    {
        // Read-only boundary; the harness can await jobs without changing streaming.
        internal bool NavigationUpdatePending => isRoomStreaming;
    }
}
#endif
