using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class SprayDamageHandler
    {
        public void SetWeaponKnockbackMultiplier(float multiplier)
        {
            weaponKnockbackMultiplier = Mathf.Max(0f, multiplier);
        }
    }
}
