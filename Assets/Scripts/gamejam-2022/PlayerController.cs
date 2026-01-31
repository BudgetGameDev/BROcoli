using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [SerializeField] HealthBar healthBar;
    [SerializeField] private GameObject _projectilePrefab;
    private float _nextAllowedAttack = 0.0f;
    private float _nextAllowedDamage = 0.0f;
    [SerializeField] private float _attackSpeed = 0.8f;
    [SerializeField] private float _invulnerabilityDuration = 5.0f;
    [SerializeField] private float _damage = 10.0f;
    [SerializeField] private float _health = 100;
    [SerializeField] private float _maxHealth = 100;

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
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float cooldownSeconds = 5f;

    private float nextAllowedTime;

    private bool gameOver;

    public bool getGameOver() {
        return gameOver;
    }

    public void setGameOver() {
        Debug.Log("Game over");
        gameOver = true;

        ambientAudio0.volume = 0f;
        ambientAudio1.volume = 0f;
        windAudio.volume = 0f;
        lavaAudio.volume = 0f;

        gameOverAudio.Play();

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

    public void AddHealth(int amount)
    {
        _health = Mathf.Min(_health + amount, _maxHealth);
    }

    public void AddDamage(int amount)
    {
        _damage += amount;

        CheckIfGameIsOver();
    }

    public void AddAttackSpeed(float multiplier)
    {
        _attackSpeed *= multiplier;
    }

    // Start is called before the first frame update
    void Start()
    {
        body = gameObject.GetComponent<Rigidbody2D>();
        gameOver = false;
        healthBar.updateHealthBar(_health, _maxHealth);
        
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

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        if (gameOver)
            return;

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
        Vector2 targetPos = body.position + delta;

        body.MovePosition(targetPos);

        // Update animator with actual movement
        animator.SetFloat("Horizontal", moveDir.x);
        animator.SetFloat("Vertical", moveDir.y);
        animator.SetFloat("Speed", moveDir.sqrMagnitude);

        lavaAudio.volume = Mathf.Abs(body.position.y) / 100f;
    }

    private void HandleEnemyDetection()
    {
        // Cooldown before checking for a new enemy
        if (Time.time < _nextAllowedAttack)
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
            float sqrDist = ((Vector2)hit.transform.position - playerPos).sqrMagnitude;
            if (sqrDist < closestSqrDistance)
            {
                closestSqrDistance = sqrDist;
                closestEnemy = hit.transform;
            }
        }

        Debug.Log("Closest enemy found: " + closestEnemy.name);

        _nextAllowedAttack = Time.time + _attackSpeed;
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

        GameObject proj = Instantiate(
            _projectilePrefab,
            spawnPos,
            Quaternion.identity
        );

        proj.GetComponent<Projectile>().Init(direction, _damage);

        // Play procedural gun sound
        if (gunAudio != null)
        {
            gunAudio.PlayGunSound();
        }
    }

    void OnTriggerEnter2D(Collider2D other) {
        Debug.Log("Collided with " + other.name);
        if (other.CompareTag("Enemy") == true)
        {
            float otherDamage = other.GetComponent<EnemyBase>()?.Damage ?? 0f;
            audio1.clip = pestAudio;
            audio1.Play();
            _health -= (int)otherDamage;
            _nextAllowedDamage = Time.time + _invulnerabilityDuration;
        }
        if (other.CompareTag("Projectile") == true)
        {
            float otherDamage = other.GetComponent<EnemyProjectile>()?.damage ?? 0f;
            audio1.clip = collideAudio;
            audio1.Play();
            _health -= (int)otherDamage;
            _nextAllowedDamage = Time.time + _invulnerabilityDuration;
        }

        healthBar.updateHealthBar(_health, _maxHealth);

        CheckIfGameIsOver();
    }

    private void CheckIfGameIsOver()
    {
        if (_health <= 0f)
        {
            Destroy(gameObject);
            setGameOver();
        }
    }
}
