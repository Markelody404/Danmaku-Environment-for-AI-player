using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Linq; // Required for sorting the Raycast hits!

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent2 : Agent
{
    // ==========================================
    // 1. SMART RAYCAST VARIABLES (NEW)
    // ==========================================
    [Header("Non-Occluding Raycast Settings")]
    public int numberOfRays = 36; 
    public float rayLength = 60f;
    public LayerMask detectableLayers; // Put Enemy, EnemyBullet, Boss, and Wall on this!
    public int maxHitsPerRay = 3;      // How many overlapping bullets to track

    // ==========================================
    // 2. EXISTING BASELINE VARIABLES
    // ==========================================
    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.white;
    public Color ceasefireColor = Color.blue;

    [Header("Life System")]
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
    private bool isDead = false;

    private ArenaManager arenaManager;

    // ==========================================
    // 3. INITIALIZATION & EPISODE LOGIC
    // ==========================================
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        
        arenaManager = transform.parent.GetComponent<ArenaManager>();
        if (arenaManager == null)
        {
            Debug.LogError($"PlayerAgent2 in {transform.parent.name} cannot find its ArenaManager!");
        }
    }

    public override void OnEpisodeBegin()
    {
        isDead = false; 
        isInvulnerable = false; 
        currentLives = maxLives; 

        SetCeasefireVisual(false);

        if (arenaManager != null) arenaManager.ResetArena();        
        
        if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
        {
            arenaManager.uiManager.UpdateLives(currentLives);
        }
        
        transform.localPosition = new Vector3(-0.06f, -5f, 0f); 
        rb.linearVelocity = Vector2.zero;
    }

    public void SetCeasefireVisual(bool isCeasefire)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isCeasefire ? ceasefireColor : normalColor;
        }
    }

    private void FixedUpdate()
    {
        if (!isDead && !isInvulnerable && Time.fixedTime >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.fixedTime + fireRate;
        }
    }

    private void Shoot()
    {
        if (arenaManager != null && arenaManager.arenaPool != null)
        {
            arenaManager.arenaPool.SpawnPlayerBullet(transform.position, Quaternion.identity);
        }
    }

    // ==========================================
    // 4. THE BRAIN: COLLECT OBSERVATIONS (UPDATED)
    // ==========================================
    public override void CollectObservations(VectorSensor sensor)
    {   
        // --- BASELINE OBSERVATIONS (7 Floats) ---
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.y);
        sensor.AddObservation(isInvulnerable ? 1f : 0f);
        sensor.AddObservation(arenaManager.isPhaseTwo ? 1f : 0f);
        sensor.AddObservation((float)currentLives / maxLives);

        // --- NEW SMART X-RAY OBSERVATIONS ---
        float angleStep = 360f / numberOfRays;

        for (int i = 0; i < numberOfRays; i++)
        {
            float currentAngle = angleStep * i;
            Vector2 direction = Quaternion.Euler(0, 0, currentAngle) * transform.up;

            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, rayLength, detectableLayers);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int hitsRecorded = 0;

            for (int j = 0; j < hits.Length && hitsRecorded < maxHitsPerRay; j++)
            {
                Collider2D hitCol = hits[j].collider;
                
                // Matched your exact tags from OnTriggerEnter2D!
                bool isEnemy = hitCol.CompareTag("Enemy");
                bool isBullet = hitCol.CompareTag("EnemyBullet"); 
                bool isBoss = hitCol.CompareTag("Boss");
                bool isWall = hitCol.CompareTag("Wall");

                // 5 Floats per hit
                sensor.AddObservation(hits[j].distance / rayLength);
                sensor.AddObservation(isEnemy ? 1f : 0f);
                sensor.AddObservation(isBullet ? 1f : 0f);
                sensor.AddObservation(isBoss ? 1f : 0f);
                sensor.AddObservation(isWall ? 1f : 0f);
                
                hitsRecorded++;

                // Stop penetrating if it hits a solid object
                if (isWall || isBoss || isEnemy) break; 
            }

            // Fill empty array slots
            for (int j = hitsRecorded; j < maxHitsPerRay; j++)
            {
                sensor.AddObservation(1f); // Max distance
                sensor.AddObservation(0f); 
                sensor.AddObservation(0f); 
                sensor.AddObservation(0f); 
                sensor.AddObservation(0f); 
            }
        }
    }

    // ==========================================
    // 5. ACTION & REWARD LOGIC
    // ==========================================
    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];
        Vector2 moveInput = new Vector2(moveX, moveY);

        if (moveInput.magnitude < 0.2f) rb.linearVelocity = Vector2.zero;
        else rb.linearVelocity = moveInput.normalized * speed;

        AddReward(0.00005f); 
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead || isInvulnerable) return; 

        if (collision.gameObject.CompareTag("EnemyBullet") || collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Boss"))
        {
            SetReward(-2f); 
            currentLives--;

            if (currentLives <= 0)
            {
                isDead = true; 
                if (arenaManager != null) arenaManager.HandlePlayerDeath();
                else EndEpisode();
            }
            else
            {
                if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
                {
                    arenaManager.uiManager.UpdateLives(currentLives);
                }
                if (arenaManager != null) arenaManager.TriggerCeasefire(3.3f);  
                StartCoroutine(RespawnAndInvulnerabilityRoutine(3.3f));
            }
        }
    }

    private System.Collections.IEnumerator RespawnAndInvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;
        SetCeasefireVisual(true);
        
        transform.localPosition = new Vector3(-0.06f, -5f, 0f);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(duration);

        SetCeasefireVisual(false);
        isInvulnerable = false;
    }

    public void ReceiveExternalReward(float rewardAmount)
    {
        AddReward(rewardAmount);
    }

    // ==========================================
    // 6. GIZMOS VISUALIZATION (NEW)
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        if (numberOfRays <= 0) return;

        float angleStep = 360f / numberOfRays;

        for (int i = 0; i < numberOfRays; i++)
        {
            float currentAngle = angleStep * i;
            Vector2 direction = Quaternion.Euler(0, 0, currentAngle) * transform.up;
            Vector2 startPos = transform.position;

            RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, direction, rayLength, detectableLayers);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int hitsRecorded = 0;
            Vector2 lastPos = startPos;
            bool hitSolid = false;

            for (int j = 0; j < hits.Length && hitsRecorded < maxHitsPerRay; j++)
            {
                Collider2D hitCol = hits[j].collider;
                
                bool isEnemy = hitCol.CompareTag("Enemy");
                bool isBullet = hitCol.CompareTag("EnemyBullet");
                bool isBoss = hitCol.CompareTag("Boss");
                bool isWall = hitCol.CompareTag("Wall");

                Gizmos.color = Color.white; 
                Gizmos.DrawLine(lastPos, hits[j].point);

                if (isBullet) Gizmos.color = Color.red;          
                else if (isBoss) Gizmos.color = Color.magenta;   
                else if (isEnemy) Gizmos.color = Color.yellow;   
                else if (isWall) Gizmos.color = Color.blue;      

                Gizmos.DrawWireSphere(hits[j].point, 0.5f);

                lastPos = hits[j].point;
                hitsRecorded++;

                if (isWall || isBoss || isEnemy)
                {
                    hitSolid = true;
                    break; 
                }
            }

            if (!hitSolid)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f); 
                Gizmos.DrawLine(lastPos, startPos + (direction * rayLength));
            }
        }
    }
}