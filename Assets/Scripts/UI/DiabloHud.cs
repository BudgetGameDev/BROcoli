using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MenuTheme;

/// <summary>
/// Screen-space combat HUD inspired by action RPGs. Player resources stay at
/// the bottom corners, XP spans the bottom edge, and the currently engaged
/// enemy owns one stable health bar at the top of the screen.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class DiabloHud : MonoBehaviour
{
    private const float EnemyDisplayDuration = 5f;
    private const float EnemyDefeatedDisplayDuration = 0.65f;
    private const float EnemyTargetLockDuration = 0.35f;

    private static readonly Color HealthBackground = Hex("#301719");
    private static readonly Color HealthFill = Hex("#C9444E");
    private static readonly Color ManaBackground = Hex("#14243D");
    private static readonly Color ManaFill = Hex("#3478C9");
    private static readonly Color ExperienceBackground = Hex("#192A20");
    private static readonly Color ExperienceFill = Hex("#D2A944");
    private static readonly Color EnemyBackground = new(0.06f, 0.07f, 0.065f, 0.94f);
    private static readonly Color EnemyFill = Hex("#B92F3B");

    private static DiabloHud instance;

    private RectTransform safeArea;
    private RectTransform enemyPanel;
    private RectTransform enemyFillRect;
    private Image enemyFill;
    private TMP_Text enemyLabel;
    private Bar playerHealthBar;
    private Slider playerHealthSlider;
    private TMP_Text playerHealthLabel;
    private RectTransform manaPanel;
    private TMP_Text manaLabel;
    private Bar experienceBar;
    private Slider experienceSlider;
    private TMP_Text experienceLabel;
    private PlayerStats playerStats;
    private EnemyBase enemyTarget;
    private string cachedEnemyName = "ENEMY";
    private float cachedEnemyHealth;
    private float cachedEnemyMaxHealth = 1f;
    private float enemyVisibleUntil = float.NegativeInfinity;
    private float enemyTargetLockedUntil = float.NegativeInfinity;
    private Rect lastSafeArea;
    private Vector2 lastRootSize;

    public static DiabloHud EnsurePresent()
    {
        if (instance != null)
            return instance;

        Canvas canvas = ScreenCanvasLocator.Find();
        if (canvas == null)
            return null;

        DiabloHud existing = canvas.GetComponent<DiabloHud>();
        return existing != null ? existing : canvas.gameObject.AddComponent<DiabloHud>();
    }

    public static void ReportEnemyHealth(EnemyBase enemy)
    {
        if (enemy == null)
            return;

        EnsurePresent()?.ShowEnemy(enemy);
    }

    public static void NotifyEnemyDefeated(EnemyBase enemy)
    {
        if (instance == null || enemy == null || instance.enemyTarget != enemy)
            return;

        instance.cachedEnemyHealth = 0f;
        instance.enemyTarget = null;
        instance.enemyVisibleUntil = Time.unscaledTime + EnemyDefeatedDisplayDuration;
        instance.UpdateEnemyPresentation(true);
    }

    public static void NotifyEnemyUnavailable(EnemyBase enemy)
    {
        if (instance == null || enemy == null || instance.enemyTarget != enemy)
            return;

        instance.enemyTarget = null;
        instance.enemyVisibleUntil = float.NegativeInfinity;
        if (instance.enemyPanel != null)
            instance.enemyPanel.gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        BuildInterface();
        ApplyResponsiveLayout(true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        ApplyResponsiveLayout(false);
        UpdatePlayerResources();
        UpdateEnemyPresentation(false);
    }

    private void UpdatePlayerResources()
    {
        if (playerStats == null)
            playerStats = PlayerStats.Resolve();
        if (playerStats == null)
            return;

        float health = playerStats.CurrentHealth;
        float maxHealth = Mathf.Max(1f, playerStats.CurrentMaxHealth);
        playerHealthSlider?.SetValueWithoutNotify(Mathf.Clamp01(health / maxHealth));
        if (playerHealthLabel != null)
            playerHealthLabel.text =
                $"HEALTH  ·  {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}";

        float experience = playerStats.CurrentExperience;
        float maxExperience = Mathf.Max(1f, playerStats.CurrentMaxExperience);
        experienceSlider?.SetValueWithoutNotify(Mathf.Clamp01(experience / maxExperience));
        if (experienceLabel != null)
        {
            experienceLabel.text =
                $"LEVEL {Mathf.RoundToInt(playerStats.CurrentLevel)}  ·  "
                + $"XP {Mathf.FloorToInt(experience)} / {Mathf.CeilToInt(maxExperience)}";
        }
    }

    private void ShowEnemy(EnemyBase enemy)
    {
        float now = Time.unscaledTime;
        if (enemyTarget != null && enemyTarget != enemy && now < enemyTargetLockedUntil)
        {
            if (!enemy.isElite || enemyTarget.isElite)
                return;
        }

        enemyTarget = enemy;
        cachedEnemyName = FormatEnemyName(enemy);
        cachedEnemyHealth = Mathf.Max(0f, enemy.Health);
        cachedEnemyMaxHealth = Mathf.Max(1f, enemy.MaxHealth);
        enemyVisibleUntil = now + EnemyDisplayDuration;
        enemyTargetLockedUntil = now + EnemyTargetLockDuration;
        UpdateEnemyPresentation(true);
    }

    private void UpdateEnemyPresentation(bool forceVisible)
    {
        if (enemyPanel == null)
            return;

        float now = Time.unscaledTime;
        if (enemyTarget != null && enemyTarget.isActiveAndEnabled && !enemyTarget.IsDying)
        {
            cachedEnemyHealth = Mathf.Max(0f, enemyTarget.Health);
            cachedEnemyMaxHealth = Mathf.Max(1f, enemyTarget.MaxHealth);
        }

        bool visible = forceVisible || now <= enemyVisibleUntil;
        enemyPanel.gameObject.SetActive(visible);
        if (!visible)
        {
            enemyTarget = null;
            return;
        }

        float healthFraction = Mathf.Clamp01(cachedEnemyHealth / cachedEnemyMaxHealth);
        enemyFillRect.anchorMax = new Vector2(healthFraction, 1f);
        string state =
            cachedEnemyHealth <= 0f
                ? "DEFEATED"
                : $"{Mathf.CeilToInt(cachedEnemyHealth)} / {Mathf.CeilToInt(cachedEnemyMaxHealth)}";
        enemyLabel.text = $"{cachedEnemyName}  ·  {state}";
    }

    private static string FormatEnemyName(EnemyBase enemy)
    {
        string raw = enemy
            .gameObject.name.Replace("(Clone)", string.Empty)
            .Replace("(Pooled)", string.Empty)
            .Trim();
        if (raw.StartsWith("Enemy"))
            raw = raw.Substring("Enemy".Length);
        if (string.IsNullOrWhiteSpace(raw))
            raw = "Enemy";

        StringBuilder formatted = new();
        for (int i = 0; i < raw.Length; i++)
        {
            char character = raw[i];
            if (i > 0 && char.IsUpper(character) && !char.IsWhiteSpace(raw[i - 1]))
                formatted.Append(' ');
            formatted.Append(character);
        }

        string label = formatted.ToString().Trim().ToUpperInvariant();
        return enemy.isElite ? $"ELITE {label}" : label;
    }
}
