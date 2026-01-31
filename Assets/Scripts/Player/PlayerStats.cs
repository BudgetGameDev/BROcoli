using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public bool IsAlive => _currentHealth > 0f;

    public float _currentHealth { get; private set; } = 100f;
    public float _maxHealth { get; private set; } = 100f;
    public float _attackSpeed { get; private set; }  = 0.6f;
    public float _damage { get; private set; }  = 10f;
    public float _movementSpeed { get; private set; }  = 10f;
    public float _experience { get; private set; }  = 0f;
    public float _level { get; private set; }  = 1f;
    public float _detectionRadius { get; private set; }  = 12f;

    [SerializeField] private HealthBar _healthBar;
    [SerializeField] private HealthBar _experienceBar;

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

    private void AddHealth(float amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        _healthBar.updateHealthBar(_currentHealth, _maxHealth);
    }

    private void AddAttackSpeed(float amount)
    {
        _attackSpeed *= amount;
    }

    private void AddDamage(float amount)
    {
        _damage += amount;
    }

    private void AddMovementSpeed(float amount)
    {
        _movementSpeed += amount;
    }

    private void AddExperience(float amount) {
        _experience += amount;
        float experienceForNextLevel = _level * 100f;
        if (_experience >= experienceForNextLevel) {
            _level += 1f;
            _experience -= experienceForNextLevel;
            // Optionally increase max health or other stats on level up
        }

        _experienceBar.updateHealthBar(_experience, experienceForNextLevel);
    }

    private void AddDetectionRadius(float amount)
    {
        _detectionRadius += amount;
    }
}
