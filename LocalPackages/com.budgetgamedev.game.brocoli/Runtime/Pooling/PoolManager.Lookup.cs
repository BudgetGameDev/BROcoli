using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PoolManager
    {
        internal static PoolManager Existing =>
            _instance != null ? _instance : FindAnyObjectByType<PoolManager>();
    }
}
