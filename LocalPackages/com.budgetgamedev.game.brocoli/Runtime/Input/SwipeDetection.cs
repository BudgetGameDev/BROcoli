using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Retained for scene compatibility. Movement is handled by
    /// <see cref="PlayerInputHandler"/>; the old swipe callbacks only logged each
    /// direction and made Editor input unnecessarily expensive.
    /// </summary>
    public sealed class SwipeDetection : MonoBehaviour { }
}
