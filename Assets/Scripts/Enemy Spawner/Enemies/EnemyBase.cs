using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

public abstract class EnemyBase : MonoBehaviour
{
    [SerializeField] Bar healthBar;
    [SerializeField] ExpGain expGainPrefab;

    public event Action<EnemyBase> OnDeath;

    public bool healthBarVisable = false;
    public bool alwaysShowHealthBar = false;
    public float TimeToStartSpawning = 0f;
    public float TimeToEndSpawning = 60f;
    public int ScoreValue = 100;
    public float Damage = 0f;
    float healthBarTimer = 0f;
    float healthBarDisplayDuration = 2f;
    
    private static bool isQuitting = false;
    
    [Header("Enemy Stats")]
    public float Speed = 2f;
    public float Health = 50f;
    public float MaxHealth = 50f;

    [Header("Elite Settings")]
    public bool isElite = false;
    public float eliteHealthMultiplier = 3f;
    public float eliteScaleMultiplier = 1.3f;
    
    // Callback for guaranteed powerup drop on elite death
    public event Action<Vector3> OnEliteDeath;

    [Header("Physics Tuning")]
    public float acceleration = 25f;
    
    [Header("Separation")]
    [SerializeField] protected float separationRadius = 2.5f;   // How close before pushing away
    [SerializeField] protected float separationForce = 50f;     // Strength of push (higher for stronger effect)
    [SerializeField] protected float playerSeparationRadius = 0.4f; // Minimum distance from player (reduced for close melee)
    [SerializeField] protected float playerSeparationForce = 40f; // How hard to avoid player overlap (reduced for melee)

    [Header("Walk Audio (Optional)")]
    [SerializeField] protected ProceduralEnemyWalkAudio walkAudio;

    public Transform player;

    private GameStates gameStates;

    public Rigidbody2D rb;

    protected virtual void Awake()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (walkAudio == null)
            walkAudio = GetComponent<ProceduralEnemyWalkAudio>();
    }
    
    /// <summary>
    /// Make this enemy an elite variant with increased HP and visual effects.
    /// </summary>
    public void MakeElite()
    {
        isElite = true;
        Health *= eliteHealthMultiplier;
        MaxHealth *= eliteHealthMultiplier;
        ScoreValue = Mathf.RoundToInt(ScoreValue * 2.5f);
        transform.localScale *= eliteScaleMultiplier;
        
        // Add and apply elite visual effects
        var effects = gameObject.AddComponent<EliteEnemyEffects>();
        effects.ApplyEliteVisuals();
        
        alwaysShowHealthBar = true;
    }

    [Header("Knockback")]
    [SerializeField] protected float enemyKnockbackForce = 5f;
    [SerializeField] protected float enemyKnockbackDuration = 0.12f;
    protected float knockbackTimer = 0f;
    protected bool isKnockedBack = false;
    
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector2.zero);
    }
    
    public void TakeDamage(float damage, Vector2 knockbackDirection)
    {
        Health -= damage;
        if (Health <= 0f)
        {
            // Invoke elite death for guaranteed powerup drop
            if (isElite)
            {
                OnEliteDeath?.Invoke(transform.position);
            }
            
            Destroy(gameObject);
            OnDeath?.Invoke(this);
            return;
        }
        
        // Apply knockback
        if (knockbackDirection != Vector2.zero && rb != null)
        {
            ApplyKnockback(knockbackDirection.normalized);
        }
        
        // Flash effect on hit
        StartCoroutine(HitFlash());

        if (alwaysShowHealthBar) return;

        healthBar.UpdateBar(Health, MaxHealth);
        healthBarVisable = true;
        healthBarTimer = healthBarDisplayDuration;
        healthBar.ShowBar();
    }
    
    public void ApplyKnockback(Vector2 direction)
    {
        if (rb == null) return;
        isKnockedBack = true;
        knockbackTimer = enemyKnockbackDuration;
        rb.linearVelocity = direction * enemyKnockbackForce;
    }
    
    private System.Collections.IEnumerator HitFlash()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color originalColor = sr.color;
            sr.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            sr.color = originalColor;
        }
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
        
        // Knockback timer
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
            }
        }

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
    
    protected virtual void FixedUpdate()
    {
        // Apply separation from other enemies and player in physics step
        ApplySeparation();
    }

    void OnDestroy()
    {
        // Don't spawn objects if the application is quitting or scene is unloading
        if (isQuitting) return;
        if (!gameObject.scene.isLoaded) return;
        
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
    
    void OnApplicationQuit()
    {
        isQuitting = true;
    }
    
    /// <summary>
    /// Apply separation forces to prevent enemies from overlapping each other and the player
    /// </summary>
    protected virtual void ApplySeparation()
    {
        if (rb == null) return;
        
        Vector2 separationVelocity = Vector2.zero;
        Vector2 myPos = rb.position;
        
        // Separation from other enemies (strong - prevent stacking)
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(myPos, separationRadius, LayerMask.GetMask("Enemy"));
        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.gameObject == gameObject) continue;
            
            Rigidbody2D otherRb = col.attachedRigidbody;
            if (otherRb == null) continue;
            
            Vector2 toMe = myPos - otherRb.position;
            float dist = toMe.magnitude;
            
            if (dist > 0.001f && dist < separationRadius)
            {
                // Stronger push when closer (quadratic falloff for more pronounced effect)
                float t = 1f - (dist / separationRadius);
                float strength = t * t; // Quadratic for stronger close-range push
                separationVelocity += toMe.normalized * strength * separationForce;
            }
        }
        
        // Gentle separation from player - just prevent complete overlap, allow melee attacks
        if (player != null)
        {
            Vector2 toMe = myPos - (Vector2)player.position;
            float dist = toMe.magnitude;
            
            // Only push if extremely close (inside player)
            if (dist > 0.001f && dist < playerSeparationRadius)
            {
                float t = 1f - (dist / playerSeparationRadius);
                float strength = t * t; // Quadratic - gentle push
                separationVelocity += toMe.normalized * strength * playerSeparationForce;
            }
        }
        
        // Apply separation as velocity change
        if (separationVelocity.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity += separationVelocity * Time.fixedDeltaTime;
        }
    }
}