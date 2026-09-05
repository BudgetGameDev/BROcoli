#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerMovement
    {
        // Executes the same collision query as movement without moving the body.
        internal Vector2 PreviewNavigationDelta(Vector2 desiredDelta) =>
            ResolveNavigationCollisions(desiredDelta);
    }
}
#endif
