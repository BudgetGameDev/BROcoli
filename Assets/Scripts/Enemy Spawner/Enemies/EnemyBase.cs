using System;
using UnityEngine;
using Pooling;

public abstract class EnemyBase : MonoBehaviour
{
    [SerializeField] Bar healthBar;
    [SerializeField] protected ExpGain expGainPrefab;

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

    [Header("Physics Tuning")]
    public float acceleration = 25f; // how quickly they reach max speed
    
    [Header("Separation")]
    [SerializeField] protected float separationRadius = 2.5f;   // How close before pushing away
    [SerializeField] protected float separationForce = 50f;     // Strength of push (higher for stronger effect)
    [SerializeField] protected float playerSeparationRadius = 0.4f; // Minimum distance from player (reduced for close melee)
    [SerializeField] protected float playerSeparationForce = 40f; // How hard to avoid player overlap (reduced for melee)

    [Header("Walk Audio (Optional)")]
    [SerializeField] protected ProceduralEnemyWalkAudio walkAudio;

    public Transform player;

    public Rigidbody2D rb;
    
    // Cached references for performance
    protected SpriteRenderer cachedSpriteRenderer;
    protected Color originalSpriteColor;
    protected MeshRenderer cachedMeshRenderer;
    protected Color originalMeshColor;
    private bool _isPooled = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Cache renderer for hit flash - enemies may use SpriteRenderer or MeshRenderer (FBX models)
        // First check for enabled SpriteRenderer
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.enabled)
            {
                cachedSpriteRenderer = sr;
                break;
            }
        }
        
        // If no SpriteRenderer, find MeshRenderer and cache its material for color changes
        if (cachedSpriteRenderer == null)
        {
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.enabled)
                {
                    cachedMeshRenderer = mr;
                    if (mr.material != null)
                    {
                        originalMeshColor = mr.material.color;
                    }
                    break;
                }
            }
        }
        else
        {
            originalSpriteColor = cachedSpriteRenderer.color;
        }
        
        // Try to get walk audio component if not assigned
        if (walkAudio == null)
            walkAudio = GetComponent<ProceduralEnemyWalkAudio>();
    }
    
    protected virtual void OnEnable()
    {
        // Get cached references from GameContext (single lookup, not per-enemy)
        var context = GameContext.Instance;
        if (context != null)
        {
            player = context.PlayerTransform;
        }
        
        // Register with spatial hash for efficient neighbor queries
        EnemySpatialHash.Instance?.Register(this);
    }
    
    protected virtual void OnDisable()
    {
        // Unregister from spatial hash
        EnemySpatialHash.Instance?.Unregister(this);
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
            Die();
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
        if (cachedSpriteRenderer != null)
        {
            cachedSpriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            cachedSpriteRenderer.color = originalSpriteColor;
        }
        else if (cachedMeshRenderer != null)
        {
            // MeshRenderer uses material color
            cachedMeshRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            cachedMeshRenderer.material.color = originalMeshColor;
        }
        else
        {
            yield break;
        }
    }

    void Start()
    {
        // Player reference is set in OnEnable via GameContext
        // Health bar setup
        if (healthBar == null)
        {
            healthBar = FindFirstObjectByType<Bar>();
        }

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
        // Update position in spatial hash for efficient neighbor queries
        EnemySpatialHash.Instance?.UpdatePosition(this);
        
        // Apply separation from other enemies and player in physics step
        ApplySeparation();
    }

    /// <summary>
    /// Called when enemy dies. Handles score, XP drop, and pooling.
    /// </summary>
    public virtual void Die()
    {
        if (isQuitting) return;
        if (!gameObject.scene.isLoaded) return;
        
        // Add score via GameContext
        var context = GameContext.Instance;
        if (context?.GameStates != null)
        {
            context.GameStates.score += ScoreValue;
        }
        
        // Spawn experience gain object (use pool if available)
        SpawnExpGain();
        
        // Invoke death event before returning to pool
        OnDeath?.Invoke(this);
        
        // Return to pool or destroy
        if (_isPooled)
        {
            PoolManager.Instance?.ReturnEnemy(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Spawn experience orb at death position.
    /// </summary>
    protected virtual void SpawnExpGain()
    {
        if (expGainPrefab == null) return;
        
        // Try to get from pool first
        ExpGain expGain = PoolManager.Instance?.GetExpGain(transform.position);
        if (expGain != null)
        {
            expGain.Init(ScoreValue);
        }
        else
        {
            // Fallback to instantiate if pool not available
            GameObject expGainObj = Instantiate(expGainPrefab.gameObject, transform.position, Quaternion.identity);
            ExpGain expGainComp = expGainObj.GetComponent<ExpGain>();
            if (expGainComp != null)
            {
                expGainComp.Init(ScoreValue);
            }
        }
    }
    
    /// <summary>
    /// Mark this enemy as pooled (affects death behavior).
    /// </summary>
    public void SetPooled(bool pooled)
    {
        _isPooled = pooled;
    }
    
    /// <summary>
    /// Reset enemy state for reuse from pool.
    /// </summary>
    public virtual void ResetForPool()
    {
        Health = MaxHealth;
        healthBarVisable = false;
        isKnockedBack = false;
        knockbackTimer = 0f;
        
        if (cachedSpriteRenderer != null)
        {
            cachedSpriteRenderer.enabled = true;  // Ensure sprite is enabled
            cachedSpriteRenderer.color = originalSpriteColor;
        }
        else if (cachedMeshRenderer != null)
        {
            cachedMeshRenderer.enabled = true;  // Ensure mesh is enabled
            cachedMeshRenderer.material.color = originalMeshColor;
        }
        
        if (healthBar != null)
        {
            healthBar.UpdateBar(Health, MaxHealth);
            healthBar.HideBar();
        }
    }
    
    void OnApplicationQuit()
    {
        isQuitting = true;
    }
    
    /// <summary>
    /// Apply separation forces to prevent enemies from overlapping each other and the player.
    /// Uses spatial hash for O(N) performance instead of O(N²) Physics2D.OverlapCircleAll.
    /// </summary>
    protected virtual void ApplySeparation()
    {
        if (rb == null) return;
        
        Vector2 separationVelocity = Vector2.zero;
        Vector2 myPos = rb.position;
        
        // Separation from other enemies using spatial hash (O(N) instead of O(N²))
        var spatialHash = EnemySpatialHash.Instance;
        if (spatialHash != null)
        {
            var nearbyEnemies = spatialHash.GetNearbyEnemies(myPos, separationRadius);
            for (int i = 0; i < nearbyEnemies.Count; i++)
            {
                EnemyBase other = nearbyEnemies[i];
                if (other == null || other == this) continue;
                
                Vector2 otherPos = other.rb != null ? other.rb.position : (Vector2)other.transform.position;
                Vector2 toMe = myPos - otherPos;
                float dist = toMe.magnitude;
                
                if (dist > 0.001f && dist < separationRadius)
                {
                    // Stronger push when closer (quadratic falloff for more pronounced effect)
                    float t = 1f - (dist / separationRadius);
                    float strength = t * t; // Quadratic for stronger close-range push
                    separationVelocity += toMe.normalized * strength * separationForce;
                }
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