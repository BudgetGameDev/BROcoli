using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    private float _nextAllowedAttack = 0.0f;
    private float _nextAllowedDamage = 0.0f;

    [SerializeField] private float _invulnerabilityDuration = 5.0f;

    [SerializeField] private int walkSpeed = 100;
    private Rigidbody2D body;
    public Animator animator;

    public AudioSource audio;
    public AudioSource audio1;
    public AudioSource ambientAudio0;
    public AudioSource ambientAudio1;
    public AudioSource windAudio;
    public AudioSource lavaAudio;
    public AudioSource gameOverAudio;

    public AudioClip audioWalk;

    public AudioClip waterAudio;
    public AudioClip pestAudio;
    public AudioClip collideAudio;
    public AudioClip growAudio;
    public AudioClip shrinkAudio;

    public Vector2 movement;
    public Vector2 RawInput { get; private set; }
    
    [SerializeField] private float inputSmoothSpeed = 15f;
    private Vector2 smoothedInput;
    private Vector2 lastNonZeroInput;

    [SerializeField] private BoxCollider2D playerCollider;
    [SerializeField] private ShuffleWalkVisual hopVisual;
    [SerializeField] private ProceduralGunAudio gunAudio;
    [SerializeField] private int speed = 10;
    [SerializeField] private float enemyDetectionRadius = 12f;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private PlayerStats playerStats;
    public bool gameOver;

    public bool getGameOver() {
        return gameOver;
    }

    public void setGameOver() {
        Debug.Log("Game over");
        gameOver = true;

        if (ambientAudio0 != null) ambientAudio0.volume = 0f;
        if (ambientAudio1 != null) ambientAudio1.volume = 0f;
        if (windAudio != null) windAudio.volume = 0f;
        if (lavaAudio != null) lavaAudio.volume = 0f;

        if (gameOverAudio != null) gameOverAudio.Play();

        // Save the final score for the EndGame screen CTA system
        var gameStates = FindAnyObjectByType<GameStates>();
        if (gameStates != null)
        {
            PlayerPrefs.SetInt("LastScore", gameStates.score);
            PlayerPrefs.Save();
            Debug.Log($"Saved final score: {gameStates.score}");
        }

        SceneManager.LoadScene("EndGame");
        // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
    }

    public void ExecMove() {
        Vector2 keyboardInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (keyboardInput.sqrMagnitude > 1f)
            keyboardInput = keyboardInput.normalized;
        
        // Check virtual controller input
        Vector2 virtualInput = Vector2.zero;
        if (VirtualController.Instance != null)
        {
            virtualInput = VirtualController.Instance.JoystickInput;
        }
        
        // Get target input - NO smoothing, pass raw input directly
        // ShuffleWalkVisual handles all movement timing and animation sync
        Vector2 targetInput;
        if (keyboardInput.sqrMagnitude > 0.01f) {
            targetInput = keyboardInput;
        } else if (virtualInput.sqrMagnitude > 0.01f) {
            targetInput = virtualInput;
        } else {
            targetInput = Vector2.zero;
        }
        
        RawInput = targetInput;
    }

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackDuration = 0.15f;
    private float knockbackTimer = 0f;
    private bool isKnockedBack = false;

    public bool TakeMeleeDamage(float damage)
    {
        return TakeMeleeDamage(damage, Vector2.zero);
    }

    public bool TakeMeleeDamage(float damage, Vector2 knockbackDirection)
    {
        if (Time.time < _nextAllowedDamage)
        {
            return false;
        }

        if (audio1 != null)
        {
            audio1.clip = pestAudio;
            audio1.Play();
        }
        _nextAllowedDamage = Time.time + _invulnerabilityDuration;

        if (playerStats != null)
        {
            playerStats.ApplyDamage(damage);
        }

        // Apply knockback
        if (knockbackDirection != Vector2.zero && body != null)
        {
            ApplyKnockback(knockbackDirection.normalized);
        }

        CheckIfGameIsOver();
        return true;
    }

    public void ApplyKnockback(Vector2 direction)
    {
        if (body == null) return;
        isKnockedBack = true;
        knockbackTimer = knockbackDuration;
        body.linearVelocity = direction * knockbackForce;
    }

    // Start is called before the first frame update
    void Start()
    {
        body = gameObject.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            Debug.LogError("PlayerController: No Rigidbody2D found on player!");
        }
        
        if (enemyLayer == 0)
        {
            Debug.LogWarning("PlayerController: enemyLayer is not set! Player won't be able to detect enemies.");
        }
        
        // Auto-find PlayerStats if not assigned
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                playerStats = GetComponentInChildren<PlayerStats>();
            }
            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }
            if (playerStats == null)
            {
                Debug.LogWarning("PlayerController: playerStats could not be found!");
            }
        }
        
        if (_projectilePrefab == null)
        {
            Debug.LogWarning("PlayerController: _projectilePrefab is not assigned!");
        }
        
        gameOver = false;
        
        // Get original camera offset before moving anything
        Camera mainCam = Camera.main;
        Vector3 cameraOffset = Vector3.zero;
        if (mainCam != null)
        {
            cameraOffset = mainCam.transform.position - transform.position;
        }
        
        // Spawn player at world center
        Vector3 spawnCenter = new Vector3(0f, 0f, transform.position.z);
        transform.position = spawnCenter;
        
        // Move camera maintaining the same relative offset
        if (mainCam != null)
        {
            mainCam.transform.position = spawnCenter + cameraOffset;
        }
    }

    void FixedUpdate()
    {
        if (gameOver)
            return;
        
        // Handle knockback timer
        if (isKnockedBack)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
            }
            // During knockback, don't process normal movement - let physics handle it
            HandleEnemyDetection();
            return;
        }

        HandleEnemyDetection();

        ExecMove(); // sets RawInput

        // Get movement from hop visual (smoothed)
        Vector2 moveDir = Vector2.zero;
        if (hopVisual != null)
        {
            moveDir = hopVisual.MovementDirection;
        }
        else
        {
            // Use RawInput directly - it already has correct magnitude
            // (keyboard gives 1.0, virtual joystick gives 0-1 based on how far pushed)
            moveDir = RawInput;
        }

        // Prevent faster diagonal movement, but preserve magnitude for analog input
        float magnitude = moveDir.magnitude;
        if (magnitude > 1f)
        {
            moveDir = moveDir.normalized;
            magnitude = 1f;
        }

        Vector2 delta = moveDir * speed * Time.fixedDeltaTime;
        
        if (body != null)
        {
            Vector2 targetPos = body.position + delta;
            body.MovePosition(targetPos);
        }

        // Update animator with actual movement
        if (animator != null)
        {
            animator.SetFloat("Horizontal", moveDir.x);
            animator.SetFloat("Vertical", moveDir.y);
            animator.SetFloat("Speed", moveDir.sqrMagnitude);
        }

        if (lavaAudio != null && body != null)
        {
            lavaAudio.volume = Mathf.Abs(body.position.y) / 100f;
        }
    }

    private void HandleEnemyDetection()
    {
        // Cooldown before checking for a new enemy
        if (Time.time < _nextAllowedAttack)
        {
            return;
        }

        if (body == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            body.position,
            enemyDetectionRadius,
            enemyLayer
        );

        if (hits.Length == 0)
        {
            return;
        }

        // Find the closest enemy
        Transform closestEnemy = null;
        float closestSqrDistance = float.MaxValue;
        Vector2 playerPos = body.position;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            float sqrDist = ((Vector2)hit.transform.position - playerPos).sqrMagnitude;
            if (sqrDist < closestSqrDistance)
            {
                closestSqrDistance = sqrDist;
                closestEnemy = hit.transform;
            }
        }

        if (closestEnemy == null)
        {
            Debug.Log("No valid enemy found in range");
            return;
        }

        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats is null - cannot attack!");
            return;
        }

        Debug.Log("Closest enemy found: " + closestEnemy.name);

        _nextAllowedAttack = Time.time + playerStats.CurrentAttackSpeed;
        FireAtEnemy(closestEnemy);
    }

    [SerializeField] private float projectileSpawnForwardOffset = 0.4f; // Forward offset from body
    [SerializeField] private float projectileSpawnSideOffset = 0.25f; // Side offset from body
    [SerializeField] private float projectileVisualHeight = -0.5f; // Z offset for visual "height" (negative = in front)

    private void FireAtEnemy(Transform enemy)
    {
        Debug.Log("Firing at enemy!");

        Collider2D col = enemy.GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        Vector2 targetPoint = col.bounds.center;
        Vector2 playerPos = (Vector2)transform.position;
        Vector2 direction = (targetPoint - playerPos).normalized;

        // Calculate spawn position offset from player (stays on same 2D collision plane)
        Vector2 spawnPos2D = playerPos;
        
        // Offset to the side (perpendicular to firing direction)
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        spawnPos2D += perpendicular * projectileSpawnSideOffset;
        
        // Offset forward in the firing direction (away from body)
        spawnPos2D += direction * projectileSpawnForwardOffset;

        // Use Z position for visual "height" - this doesn't affect 2D collision
        Vector3 spawnPos = new Vector3(spawnPos2D.x, spawnPos2D.y, projectileVisualHeight);

        if (_projectilePrefab == null)
        {
            Debug.LogWarning("Projectile prefab not assigned!");
            return;
        }

        GameObject proj = Instantiate(
            _projectilePrefab,
            spawnPos,
            Quaternion.identity
        );

        if (proj != null)
        {
            Projectile projectile = proj.GetComponent<Projectile>();
            if (projectile != null && playerStats != null)
            {
                projectile.Init(direction, playerStats.CurrentDamage);
            }
        }

        // Play procedural gun sound
        if (gunAudio != null)
        {
            gunAudio.PlayGunSound();
        }
    }

    void OnTriggerEnter2D(Collider2D other) {
        Debug.Log("Collided with " + other.name);
        if (_nextAllowedDamage <= Time.time)
        {
            switch (other.tag)
            {
                case "Enemy":
                    if (audio1 != null)
                    {
                        audio1.clip = pestAudio;
                        audio1.Play();
                    }

                    playerStats?.ApplyDamage(other.GetComponent<EnemyBase>()?.Damage ?? 0f);

                    _nextAllowedDamage = Time.time + _invulnerabilityDuration;
                    break;
                case "Projectile":
                    if (audio1 != null)
                    {
                        audio1.clip = collideAudio;
                        audio1.Play();
                    }

                    playerStats?.ApplyDamage(other.GetComponent<EnemyBase>()?.Damage ?? 0f);

                    _nextAllowedDamage = Time.time + _invulnerabilityDuration;
                    break;
                case "Experience":
                    if (audio1 != null)
                    {
                        audio1.clip = waterAudio;
                        audio1.Play();
                    }

                    playerStats?.ApplyExperience(other.GetComponent<ExpGain>()?.expAmountGain ?? 0f);
                    break;
            }
        }

        CheckIfGameIsOver();
    }

    private void CheckIfGameIsOver()
    {
        if (playerStats == null)
        {
            return;
        }
        
        if (playerStats.IsAlive == false)
        {
            Destroy(gameObject);
            setGameOver();
        }
    }
}
