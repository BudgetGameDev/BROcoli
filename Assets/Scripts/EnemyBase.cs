using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public abstract class EnemyBase : MonoBehaviour
{
    [SerializeField] Bar healthBar;
    [SerializeField] ExpGain expGainPrefab;

    public bool healthBarVisable = false;
    public bool alwaysShowHealthBar = false;
    public float TimeToStartSpawning = 0f;
    public float TimeToEndSpawning = 60f;
    public int ScoreValue = 100;
    public float Damage = 0f;
    float healthBarTimer = 0f;
    float healthBarDisplayDuration = 2f;
    
    [Header("Enemy Stats")]
    public float Speed = 2f;
    public float Health = 50f;
    public float MaxHealth = 50f;

    [Header("Physics Tuning")]
    public float acceleration = 25f; // how quickly they reach max speed

    public Transform player;

    private GameStates gameStates;

    public Rigidbody2D rb;

    void Awake()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    public void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0f)
            Destroy(gameObject);

        if (alwaysShowHealthBar) return;

        healthBar.UpdateBar(Health, MaxHealth);
        healthBarVisable = true;
        healthBarTimer = healthBarDisplayDuration;
        healthBar.ShowBar();
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Bar _healthBar = FindFirstObjectByType<Bar>();

        if (playerObj != null)
            player = playerObj.transform;

        if (_healthBar != null)
            healthBar = _healthBar;

        if (healthBar != null)
        {
            healthBar.UpdateBar(Health, MaxHealth);
            if (alwaysShowHealthBar)
            {
                healthBarVisable = true;
                healthBar.ShowBar();
            }
            else
            {
                healthBarVisable = false;
                healthBar.HideBar();
            }
        }
    }

    public virtual void Update()
    {
        if (player == null) return;

        // Health bar timer logic
        if (healthBarVisable && !alwaysShowHealthBar && healthBar != null)
        {
            healthBarTimer -= Time.deltaTime;
            if (healthBarTimer <= 0f)
            {
                healthBarVisable = false;
                healthBar.HideBar();
            }
        }
    }

    void OnDestroy()
    {
        Debug.Log("Destroyed enemy, adding score: " + ScoreValue);

        if (gameStates)
        {
            gameStates.score += ScoreValue;
        }
        else
        {
            gameStates = FindFirstObjectByType<GameStates>();
            if (gameStates == null) return;
            gameStates.score += ScoreValue;
        }
        
        // Initialize and spawn experience gain object
        if (expGainPrefab != null)
        {
            GameObject expGainObj = Instantiate(expGainPrefab.gameObject, transform.position, Quaternion.identity);
            ExpGain expGainComp = expGainObj.GetComponent<ExpGain>();
            if (expGainComp != null)
            {
                int expAmount = ScoreValue; // Example: 1 exp per 10 max health
                expGainComp.Init(expAmount);
            }
        }
    }
}