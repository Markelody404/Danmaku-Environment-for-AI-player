using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent : Agent
{
    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.white;
    public Color ceasefireColor = Color.blue; 
    [Header("Life System")] // This is a simple life system that can be expanded later for more complex mechanics (like temporary invulnerability, multiple lives, etc.)
    public int maxLives = 3;
    private int currentLives;
    private bool isInvulnerable = false;

    [Header("Movement")]
    public float speed = 8f;
    private Rigidbody2D rb;

    [Header("Shooting Setup")]
    public GameObject bulletPrefab; 
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;
    private bool isDead = false; // Handles the "death state" to prevent multiple triggers before the episode resets

    // NEW: Reference to the referee (ArenaManager)
    private ArenaManager arenaManager;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        // ✅ NEW: Automatically find the SpriteRenderer if not assigned
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        
        // NEW: Find the referee for this specific arena
        arenaManager = transform.parent.GetComponent<ArenaManager>();
        
        if (arenaManager == null)
        {
            Debug.LogError($"PlayerAgent in {transform.parent.name} cannot find its ArenaManager!");
        }
    }

    public override void OnEpisodeBegin()
    {

        isDead = false; // Reset the death state at the start of each 
        isInvulnerable = false; // Reset invulnerability
        currentLives = maxLives; // Reset lives

        // ✅ NEW: Ensure player starts with the normal color
        SetCeasefireVisual(false);

        if (arenaManager != null) arenaManager.ResetArena();        // Reason: All of that heavy lifting is now handled centrally by the ArenaManager!
        // ✅ NEW: Tell the UI we have 3 lives the exact millisecond the game starts!
        if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
        {
            arenaManager.uiManager.UpdateLives(currentLives);
        }
        // Keep the physical position reset so the AI always starts in the right spot
        transform.localPosition = new Vector3(-0.06f, -5f, 0f); 
        rb.linearVelocity = Vector2.zero;
    }

    // ✅ NEW: Public method for ArenaManager to call during Intermissions
    public void SetCeasefireVisual(bool isCeasefire)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isCeasefire ? ceasefireColor : normalColor;
        }
    }

    private void FixedUpdate()
    {
        // PENALTY BOX: Only allow shooting if the AI is NOT dead and NOT invulnerable
        if (!isDead && !isInvulnerable && Time.fixedTime >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.fixedTime + fireRate;
        }
    }

    private void Shoot()
    {
        // ✅ Ask the pool for a player bullet!
        if (arenaManager != null && arenaManager.arenaPool != null)
        {
            arenaManager.arenaPool.SpawnPlayerBullet(transform.position, Quaternion.identity);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {   
        // 1. EXACT POSITION (2 floats)
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);

        // 2. MOMENTUM / VELOCITY (2 floats)
        // ✅ FIXED: Changed 'velocity' to 'linearVelocity' to remove the obsolete warning
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.y);
        
        // 3. STATUS CONDITION (1 float)
        sensor.AddObservation(isInvulnerable ? 1f : 0f);
        
        // 4. PHASE TRACKER (1 float)
        // 0f = Phase 1 & Intermission, 1f = Phase 2 (Boss)
        sensor.AddObservation(arenaManager.isPhaseTwo ? 1f : 0f);

        // Normalized between 0.0 and 1.0 for optimal training
        sensor.AddObservation((float)currentLives / maxLives);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];
        Vector2 moveInput = new Vector2(moveX, moveY);

        // --- THE DEADZONE FIX ---
        if (moveInput.magnitude < 0.2f)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            rb.linearVelocity = moveInput.normalized * speed;
        }

        // Apply the passive survival dopamine drip (+0.0005 per frame)
        AddReward(0.00005f); //#Rewards

        
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore hits if dead OR currently invulnerable
        if (isDead || isInvulnerable) return; 

        if (collision.gameObject.CompareTag("EnemyBullet") || collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Boss"))
        {
            SetReward(-2f); // Ouch!
            currentLives--;

            if (currentLives <= 0)
            {
                isDead = true; // Out of lives, lock it down
                if (arenaManager != null) arenaManager.HandlePlayerDeath();
                else EndEpisode();
            }
            else
            {
                // ✅ NEW: Tell the localized UI that we lost a life!
                if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
                {
                    arenaManager.uiManager.UpdateLives(currentLives);
                }
                // We still have lives! Trigger the ceasefire and respawn mechanics
                if (arenaManager != null) arenaManager.TriggerCeasefire(3.3f);  // This is where we edit I-frame secs
                StartCoroutine(RespawnAndInvulnerabilityRoutine(3.3f));
            }
        }
    }

    private System.Collections.IEnumerator RespawnAndInvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;

        // ✅ NEW: Turn Blue immediately when hit
        SetCeasefireVisual(true);
        
        // Snap the player back to the safe starting position
        transform.localPosition = new Vector3(-0.06f, -5f, 0f);
        rb.linearVelocity = Vector2.zero;

        // Optional: You can disable your player's SpriteRenderer here to make them flash!

        yield return new WaitForSeconds(duration);

        // ✅ NEW: Revert to Normal color when invulnerability ends
        SetCeasefireVisual(false);
        isInvulnerable = false;
    }

    // NEW: The "Bank Account" method!
    // The ArenaManager will call this to deposit points for killing mobs, hitting the boss, and speedrunning.
    public void ReceiveExternalReward(float rewardAmount)
    {
        AddReward(rewardAmount);
    }
}