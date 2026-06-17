using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    public GameObject enemyBulletPrefab;
    public Transform player;

    [Header("Shooting Timers")]
    public float fireRate = 0.5f; 
    private float fireTimer = 0f; // CHANGED: Using a local timer to prevent post-ceasefire spam

    private ArenaManager arenaManager; // NEW: Reference to the referee

    void Start()
    {
        // Look inside the local arena instead of the whole scene
        PlayerAgent localPlayer = transform.parent.GetComponentInChildren<PlayerAgent>();
        if (localPlayer != null) player = localPlayer.transform;

        // Grab the referee
        arenaManager = transform.parent.GetComponent<ArenaManager>();
    }

    void Update()
    {
        // CEASEFIRE CHECK: If the player is respawning/invulnerable, freeze the timer and don't shoot!
        if (arenaManager != null && arenaManager.isCeasefire) return;

        fireTimer += Time.deltaTime;
        if (fireTimer >= fireRate)
        {
            int randomPattern = Random.Range(0, 3);
            
            if (randomPattern == 0) FireWide();
            else if (randomPattern == 1) FireWall(); 
            else if (randomPattern == 2) FireTargetSpread(); 

            fireTimer = 0f; // Reset our local timer
        }
    }

    private void SpawnBullet(Vector3 spawnPos, Quaternion rotation)
    {
        // ✅ Ask the pool for a bullet instead of instantiating!
        if (arenaManager != null && arenaManager.arenaPool != null)
        {
            arenaManager.arenaPool.SpawnBullet(spawnPos, rotation);
        }
    }

    // --- PATTERN 1: WIDE ---
    void FireWide()
    {
        int bulletCount = 9; 
        float spreadAngle = 12f; 
        float startAngle = 180f - ((bulletCount - 1) * spreadAngle / 2f);

        for (int i = 0; i < bulletCount; i++)
        {
            float currentAngle = startAngle + (i * spreadAngle);
            SpawnBullet(transform.position, Quaternion.Euler(0, 0, currentAngle));
        }
    }

    // --- PATTERN 2: WALL ---
    void FireWall()
    {
        float spreadDistance = 0.6f; 
        float[] positions = { -1.5f, -0.5f, 0.5f, 1.5f };

        foreach (float pos in positions)
        {
            Vector3 spawnPos = transform.position + new Vector3(pos * spreadDistance, 0, 0);
            SpawnBullet(spawnPos, Quaternion.Euler(0, 0, 180f));
        }
    }

    // --- PATTERN 3: TARGET SPREAD ---
    void FireTargetSpread()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        float[] spreadAngles = { -15f, 0f, 15f };

        foreach (float offset in spreadAngles)
        {
            SpawnBullet(transform.position, Quaternion.Euler(0, 0, angle - 90f + offset));
        }
    }
}