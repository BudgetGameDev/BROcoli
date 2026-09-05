using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autoplay helper: when the <see cref="LevelUpScreen"/> appears, picks the
    /// strongest upgrade for the agent's current health, build, and threat pressure.
    /// Runs in Update
    /// (which still ticks at <c>Time.timeScale == 0</c>) and uses unscaled time.
    ///
    /// Trade-off upgrades are evaluated from their actual bonus and penalty magnitudes,
    /// and capped stats receive diminishing value.
    /// </summary>
    public class LevelUpAutoResolver : MonoBehaviour
    {
        private LevelUpScreen _screen;
        private PlayerStats _stats;
        private float _cooldown;

        /// <summary>
        /// What a common upgrade of each stat is worth, on one scale. Speed used to
        /// sit at the top of this list, above damage and above health, which made it
        /// the pick whenever it was offered: a run that kept being shown it stacked
        /// movement and nothing else, and finished a quarter-hour no stronger in a
        /// fight than it started. It is worth having and it is not worth everything.
        /// </summary>
        private static float StatWeight(UpgradeOption.StatType t) =>
            t switch
            {
                UpgradeOption.StatType.Speed => 7f,
                UpgradeOption.StatType.LifeSteal => 8f,
                UpgradeOption.StatType.SprayRange => 8f,
                UpgradeOption.StatType.HealthRegen => 8f,
                UpgradeOption.StatType.Armor => 8f,
                UpgradeOption.StatType.Dodge => 7f,
                UpgradeOption.StatType.MaxHealth => 8f,
                UpgradeOption.StatType.AttackSpeed => 8f,
                UpgradeOption.StatType.Damage => 8f,
                UpgradeOption.StatType.SprayWidth => 7f,
                UpgradeOption.StatType.CritChance => 6f,
                UpgradeOption.StatType.CritDamage => 5f,
                UpgradeOption.StatType.DetectionRadius => 1.5f,
                _ => 3f,
            };

        private static float CommonAmount(UpgradeOption.StatType type) =>
            type switch
            {
                UpgradeOption.StatType.MaxHealth => 15f,
                UpgradeOption.StatType.Damage => 8f,
                UpgradeOption.StatType.Speed => 0.2f,
                UpgradeOption.StatType.AttackSpeed => 0.05f,
                UpgradeOption.StatType.SprayRange => 0.1f,
                UpgradeOption.StatType.SprayWidth => 2f,
                UpgradeOption.StatType.DetectionRadius => 1f,
                UpgradeOption.StatType.CritChance => 3f,
                UpgradeOption.StatType.CritDamage => 0.15f,
                UpgradeOption.StatType.Dodge => 2f,
                UpgradeOption.StatType.Armor => 3f,
                UpgradeOption.StatType.HealthRegen => 1f,
                UpgradeOption.StatType.LifeSteal => 2f,
                _ => 1f,
            };

        internal static float Score(UpgradeOption option, UpgradeDecisionContext context)
        {
            if (option == null)
                return float.NegativeInfinity;

            float score = ScoreChange(option.Type, option.Amount, context);
            if (option.IsTrollUpgrade)
                score -= ScoreChange(option.PenaltyType, option.PenaltyAmount, context) * 1.2f;
            return score;
        }

        private static float ScoreChange(
            UpgradeOption.StatType type,
            float amount,
            UpgradeDecisionContext context
        )
        {
            float score = StatWeight(type) * Mathf.Max(0f, amount) / CommonAmount(type);
            bool lowHealth = context.HealthFraction < 0.5f;
            bool crowded = context.NearbyEnemies >= 5;

            if (
                lowHealth
                && type
                    is UpgradeOption.StatType.MaxHealth
                        or UpgradeOption.StatType.HealthRegen
                        or UpgradeOption.StatType.Armor
                        or UpgradeOption.StatType.Dodge
                        or UpgradeOption.StatType.LifeSteal
            )
                score *= 1.75f;
            if (
                crowded
                && type
                    is UpgradeOption.StatType.Speed
                        or UpgradeOption.StatType.SprayWidth
                        or UpgradeOption.StatType.Armor
                        or UpgradeOption.StatType.Dodge
            )
                score *= 1.3f;

            if (type == UpgradeOption.StatType.Dodge && context.DodgeChance >= 70f)
                score *= 0.15f;
            else if (type == UpgradeOption.StatType.CritChance && context.CritChance >= 95f)
                score *= 0.15f;
            else if (type == UpgradeOption.StatType.LifeSteal && context.LifeSteal >= 95f)
                score *= 0.15f;

            return score;
        }

        private void Update()
        {
            if (_cooldown > 0f)
                _cooldown -= Time.unscaledDeltaTime;

            if (_screen == null)
            {
                _screen = FindAnyObjectByType<LevelUpScreen>();
                if (_screen == null)
                    return;
            }

            if (_cooldown > 0f || !_screen.IsShowing())
                return;

            if (_stats == null)
                _stats = FindAnyObjectByType<PlayerStats>();
            UpgradeDecisionContext context = UpgradeDecisionContext.From(
                _stats,
                BotDriver.NearbyEnemyCount
            );

            int best = 0;
            float bestScore = float.NegativeInfinity;
            int n = _screen.OptionCount;
            for (int i = 0; i < n; i++)
            {
                float sc = Score(_screen.GetOption(i), context);
                if (sc > bestScore)
                {
                    bestScore = sc;
                    best = i;
                }
            }

            UpgradeOption selected = _screen.GetOption(best);
            _screen.AutoSelectUpgrade(best);
            AutoplayFeatureLog.Record(AutoplayFeatures.UpgradeChosen);
            Debug.Log(
                $"[Autoplay] Picked {selected?.DisplayName ?? "upgrade"} in slot {best}/{n} "
                    + $"(utility {bestScore:0.0}, hp {context.HealthFraction:P0})."
            );
            _cooldown = 0.25f;
        }
    }

    internal readonly struct UpgradeDecisionContext
    {
        internal readonly float HealthFraction;
        internal readonly int NearbyEnemies;
        internal readonly float DodgeChance;
        internal readonly float CritChance;
        internal readonly float LifeSteal;

        internal UpgradeDecisionContext(
            float healthFraction,
            int nearbyEnemies,
            float dodgeChance,
            float critChance,
            float lifeSteal
        )
        {
            HealthFraction = healthFraction;
            NearbyEnemies = nearbyEnemies;
            DodgeChance = dodgeChance;
            CritChance = critChance;
            LifeSteal = lifeSteal;
        }

        internal static UpgradeDecisionContext From(PlayerStats stats, int nearbyEnemies)
        {
            if (stats == null)
                return new UpgradeDecisionContext(1f, nearbyEnemies, 0f, 0f, 0f);
            float healthFraction =
                stats.CurrentMaxHealth > 0f ? stats.CurrentHealth / stats.CurrentMaxHealth : 1f;
            return new UpgradeDecisionContext(
                healthFraction,
                nearbyEnemies,
                stats.CurrentDodgeChance,
                stats.CurrentCritChance,
                stats.CurrentLifeSteal
            );
        }
    }
}
