using System.Linq;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public bool IsAlive => CurrentHealth > 0f;

    public float CurrentHealth = 100f;
    public float CurrentMaxHealth = 100f;
    public float CurrentAttackSpeed  = 0.6f;
    public float CurrentDamage  = 10f;
    public float CurrentMovementSpeed  = 10f;
    public float CurrentExperience  = 0f;
    public float CurrentMaxExperience = 100f;
    public float CurrentLevel  = 1f;
    public float CurrentDetectionRadius  = 12f;

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
    }

    private void AddHealth(float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, CurrentMaxHealth);
        _healthBar.UpdateBar(CurrentHealth, CurrentMaxHealth);
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

        _experienceBar.UpdateBar(CurrentExperience, CurrentMaxExperience);
    }

    private void AddDetectionRadius(float amount)
    {
        CurrentDetectionRadius += amount;
    }
}
