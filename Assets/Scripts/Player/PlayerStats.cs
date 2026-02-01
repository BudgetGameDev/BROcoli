using System.Linq;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public bool IsAlive => CurrentHealth > 0f;

    public float CurrentHealth = 200f;
    public float CurrentMaxHealth = 200f;
    public float CurrentAttackSpeed  = 0.6f;
    public float CurrentDamage  = 10f;
    public float CurrentMovementSpeed  = 10f;
    public float CurrentExperience  = 0f;
    public float CurrentMaxExperience = 100f;
    public float CurrentLevel  = 1f;
    public float CurrentDetectionRadius  = 12f;
    
    // Spray weapon stats
    public float CurrentSprayRange = 8.0f;      // How far the spray reaches
    public float CurrentSprayWidth = 60f;       // Cone angle in degrees
    public float CurrentSprayDamageMultiplier = 1f;  // Multiplier for particle hit damage

    [SerializeField] private Bar _healthBar;
    [SerializeField] private Bar _experienceBar;

    public void ApplyBoost(BoostBase boost)
    {
        switch (boost)
        {
            case HealthBoost healthBoost:
                AddHealth(healthBoost.Amount);
                break;
            case AttackSpeedBoost attackSpeedBoost:
                AddAttackSpeed(attackSpeedBoost.Amount);
                break;
            case DamageBoost damageBoost:
                AddDamage(damageBoost.Amount);
                break;
            case MovementSpeedBoost movementSpeedBoost:
                AddMovementSpeed(movementSpeedBoost.Amount);
                break;
            case ExperienceBoost experienceBoost:
                AddExperience(experienceBoost.Amount);
                break;
            case DetectionRadiusBoost detectionRadiusBoost:
                AddDetectionRadius(detectionRadiusBoost.Amount);
                break;
            case SprayRangeBoost sprayRangeBoost:
                AddSprayRange(sprayRangeBoost.Amount);
                break;
            case SprayWidthBoost sprayWidthBoost:
                AddSprayWidth(sprayWidthBoost.Amount);
                break;
            default:
                Debug.LogWarning("Unknown boost type applied.");
                break;
        }
    }

    public void ApplyDamage(float damage)
    {
        AddHealth(-damage);
    }

    public void ApplyExperience(float experience)
    {
        AddExperience(experience);
    }

    public void ResetStats()
    {
        CurrentHealth = 100f;
        CurrentMaxHealth = 100f;
        CurrentAttackSpeed = 0.6f;
        CurrentDamage = 10f;
        CurrentMovementSpeed = 10f;
        CurrentExperience = 0f;
        CurrentMaxExperience = 100f;
        CurrentLevel = 1f;
        CurrentDetectionRadius = 12f;
        CurrentSprayRange = 8.0f;
        CurrentSprayWidth = 60f;
        CurrentSprayDamageMultiplier = 1f;

        _healthBar.UpdateBar(CurrentHealth, CurrentMaxHealth);
        _experienceBar.UpdateBar(CurrentExperience, CurrentMaxExperience);
    }

    private void Start()
    {
        ResetStats();
    }

    private void LevelUp()
    {
        CurrentLevel += 1f;

        CurrentHealth += 20f;
        CurrentMaxHealth += 20f;
        CurrentAttackSpeed *= 0.9f;
        CurrentDamage += 10f;
        CurrentMovementSpeed += 10f;
        CurrentExperience -= CurrentMaxExperience;
        CurrentMaxExperience *= 1.2f;
        CurrentLevel += 1f;
        CurrentDetectionRadius += 2f;
        CurrentSprayRange += 0.15f;  // Spray gets slightly longer each level
        CurrentSprayWidth += 3f;      // Spray gets slightly wider each level
        CurrentSprayDamageMultiplier += 0.1f;  // More damage per particle hit

        _healthBar.UpdateBar(CurrentHealth, CurrentMaxHealth);
        _experienceBar.UpdateBar(CurrentExperience, CurrentMaxExperience);
    }

    private void AddHealth(float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, CurrentMaxHealth);
        _healthBar?.UpdateBar(CurrentHealth, CurrentMaxHealth);
    }

    private void AddAttackSpeed(float amount)
    {
        CurrentAttackSpeed *= amount;
    }

    private void AddDamage(float amount)
    {
        CurrentDamage += amount;
    }

    private void AddMovementSpeed(float amount)
    {
        CurrentMovementSpeed += amount;
    }

    private void AddExperience(float amount) {
        CurrentExperience += amount;
        if (CurrentExperience >= CurrentMaxExperience)
        {
            LevelUp();
        }
        else
        {
            _experienceBar.UpdateBar(CurrentExperience, CurrentMaxExperience);
        }
    }

    private void AddDetectionRadius(float amount)
    {
        CurrentDetectionRadius += amount;
    }

    public void AddSprayRange(float amount)
    {
        CurrentSprayRange += amount;
    }

    public void AddSprayWidth(float amount)
    {
        CurrentSprayWidth = Mathf.Clamp(CurrentSprayWidth + amount, 20f, 120f); // Limit between 20-120 degrees
    }

    public void AddSprayDamageMultiplier(float amount)
    {
        CurrentSprayDamageMultiplier += amount;
    }
}
