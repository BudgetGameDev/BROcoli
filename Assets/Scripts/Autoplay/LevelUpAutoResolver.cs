using UnityEngine;

/// <summary>
/// Autoplay helper: when the <see cref="LevelUpScreen"/> appears, picks the
/// strongest upgrade for a survival-focused meta build (instead of choosing at
/// random), so an unattended run both stays alive and snowballs. Runs in Update
/// (which still ticks at <c>Time.timeScale == 0</c>) and uses unscaled time.
///
/// Meta priorities (survivor-style, short-range spray): mobility &amp; sustain first
/// (Speed, LifeSteal, Regen, Armor, Dodge, MaxHP), then reach &amp; DPS
/// (SprayRange/Width, AttackSpeed, Damage, Crit); Detection is near-worthless since
/// combat is already range-bound. Trade-off ("troll") upgrades are scored net of
/// their penalty.
/// </summary>
public class LevelUpAutoResolver : MonoBehaviour
{
    private LevelUpScreen _screen;
    private float _cooldown;

    private static float StatWeight(UpgradeOption.StatType t) =>
        t switch
        {
            UpgradeOption.StatType.Speed => 10f,
            UpgradeOption.StatType.LifeSteal => 9f,
            UpgradeOption.StatType.SprayRange => 8f,
            UpgradeOption.StatType.HealthRegen => 8f,
            UpgradeOption.StatType.Armor => 8f,
            UpgradeOption.StatType.Dodge => 8f,
            UpgradeOption.StatType.MaxHealth => 7f,
            UpgradeOption.StatType.AttackSpeed => 7f,
            UpgradeOption.StatType.Damage => 6f,
            UpgradeOption.StatType.SprayWidth => 6f,
            UpgradeOption.StatType.CritChance => 5f,
            UpgradeOption.StatType.CritDamage => 4f,
            UpgradeOption.StatType.DetectionRadius => 2f,
            _ => 3f,
        };

    private static float RarityFactor(UpgradeOption.Rarity r) =>
        r switch
        {
            UpgradeOption.Rarity.Common => 1f,
            UpgradeOption.Rarity.Uncommon => 1.5f,
            UpgradeOption.Rarity.Rare => 2.5f,
            UpgradeOption.Rarity.Epic => 4f,
            UpgradeOption.Rarity.Legendary => 6f,
            _ => 1f,
        };

    private static float Score(UpgradeOption o)
    {
        if (o == null)
            return float.NegativeInfinity;
        float s = StatWeight(o.Type) * RarityFactor(o.RarityLevel);
        if (o.IsTrollUpgrade)
            s -= StatWeight(o.PenaltyType) * RarityFactor(o.RarityLevel) * 0.7f; // penalty ~60-80% magnitude
        return s;
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

        int best = 0;
        float bestScore = float.NegativeInfinity;
        int n = _screen.OptionCount;
        for (int i = 0; i < n; i++)
        {
            float sc = Score(_screen.GetOption(i));
            if (sc > bestScore)
            {
                bestScore = sc;
                best = i;
            }
        }

        _screen.AutoSelectUpgrade(best);
        Debug.Log($"[Autoplay] Picked meta upgrade slot {best}/{n} (score {bestScore:0.0}).");
        _cooldown = 0.25f;
    }
}
