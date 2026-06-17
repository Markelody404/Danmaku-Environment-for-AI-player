using UnityEngine;
using System.Collections;
using Unity.MLAgents; // NEW: Needed for the UI toggle check

public class BossController : MonoBehaviour
{
    [Header("Boss Stats")]
    public float maxHp = 1200f;
    private float currentHp;
    
    // DELETED: lastReportedHpPercent. We are using the stabilized +0.0833 per hit instead!

    [Header("Visuals & Rotation")]
    public float rotationSpeed = 100f;

    [Header("Yellow Zone Boundaries")]
    public float minX = -5f;
    public float maxX = 5f;
    public float minY = 5f;
    public float maxY = 7.5f;

    [Header("Movement Behavior")]
    public float moveInterval = 20f; 
    public float moveDuration = 2f;

    [Header("Idle Hover Settings")]
    public float hoverDistance = 0.5f;
    public float hoverSpeed = 1.5f;

    private float timeSinceLastMove = 0f;
    private Vector3 currentAnchorPosition;
    private bool isRelocating = false;

    // References
    private PlayerAgent playerAgent; 
    private ArenaManager arenaManager; // NEW: The Boss needs to talk to the Referee

    [Header("Damage Taken Feedback")]
    public float damageTaken = 1.0f; // CHANGED: Standardized to 1.0 per bullet to match 1200 hits

    [Header("Bullet Hell Patterns")]
    public GameObject enemyBulletPrefab; 
    public float attackCycleInterval = 10f; 
    public float attackDuration = 7f; 
    private float attackTimer = 10f; 

    [Header("Dynamic Difficulty")]
    public float intensityMultiplier = 2f; 

    private void CheckBossPhase(int hpPercent)
    {
        if (hpPercent <= 20) intensityMultiplier = 20f;
        else if (hpPercent <= 40) intensityMultiplier = 15f;
        else if (hpPercent <= 60) intensityMultiplier = 10f;
        else if (hpPercent <= 80) intensityMultiplier = 7f;
        else intensityMultiplier = 2f; 
    }

    private void Start()
    {
        currentHp = maxHp;
        
        // Grab references locally within this specific arena
        playerAgent = transform.parent.GetComponentInChildren<PlayerAgent>();
        arenaManager = transform.parent.GetComponent<ArenaManager>();

        // Pick a starting position immediately
        currentAnchorPosition = GetRandomYellowZonePosition();
        transform.localPosition = currentAnchorPosition;

        // ✅ NEW: Smart UI Update via localized ArenaManager
        if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
        {
            arenaManager.uiManager.StartPhaseTwo(maxHp);
        }
    }

    // HELPER METHOD: Automatically parents bullets to the arena
    private void SpawnBullet(Vector3 spawnPos, Quaternion rotation)
    {
        if (arenaManager != null && arenaManager.isCeasefire) return;

        // ✅ NEW: Ask the pool for a bullet instead of instantiating!
        if (arenaManager != null && arenaManager.arenaPool != null)
        {
            arenaManager.arenaPool.SpawnBullet(spawnPos, rotation);
        }
    }

    // --- PATTERN 1 NEEDS A TINY TWEAK ---
    // Because Pattern 1 manually instantiates bullets instead of using SpawnBullet, 
    // we just wrap its shooting logic in a Ceasefire check.
    private IEnumerator Pattern1_Rain(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // CEASEFIRE CHECK: Shoot blanks if active!
            if (arenaManager != null && !arenaManager.isCeasefire)
            {
                for(int i=0; i<3; i++) {
                    Vector3 localSpawnPos = new Vector3(Random.Range(-5.8f, 5.8f), 7.8f, -8f); 
                    
                    // ✅ NEW: Convert local position to world position, then use the pool
                    Vector3 worldSpawnPos = transform.parent.TransformPoint(localSpawnPos);
                    Quaternion rot = Quaternion.Euler(0, 0, 180f);
                    
                    if (arenaManager.arenaPool != null)
                    {
                        arenaManager.arenaPool.SpawnBullet(worldSpawnPos, rot);
                    }
                }
            }
            
            float currentDelay = 0.05f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern2_PulseRing(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            int bulletCount = Mathf.Max(4, Mathf.RoundToInt(24f * (intensityMultiplier / 20f)));
            float angleStep = 360f / bulletCount;
            for (int i = 0; i < bulletCount; i++) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, i * angleStep)); }
            float currentDelay = 0.4f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern3_TargetBurst(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (playerAgent != null)
            {
                Vector3 dir = playerAgent.transform.position - transform.position;
                float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; 
                float[] spreads = { -20f, -10f, 0f, 10f, 20f };
                foreach (float spread in spreads) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, targetAngle + spread)); }
            }
            float currentDelay = 0.15f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern4_Shotgun(float duration)
    {
        float elapsedTime = 0f;
        float[] angles = { 130f, 142.5f, 155f, 167.5f, 180f, 192.5f, 205f, 217.5f, 230f }; 
        while (elapsedTime < duration)
        {
            foreach (float angle in angles) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, angle)); }
            float currentDelay = 0.25f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern5_Spiral(float duration)
    {
        float elapsedTime = 0f;
        float currentAngle = 0f;
        while (elapsedTime < duration)
        {
            int arms = Mathf.Max(2, Mathf.RoundToInt(4f * (intensityMultiplier / 20f)));
            float angleStep = 360f / arms;
            for (int i = 0; i < arms; i++) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, currentAngle + (i * angleStep))); }
            currentAngle += 15f; 
            float currentDelay = 0.03f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern6_SpinningCross(float duration)
    {
        float elapsedTime = 0f;
        float currentAngle = 0f;
        while (elapsedTime < duration)
        {
            float[] arms = { 0f, 90f, 180f, 270f };
            foreach (float arm in arms) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, currentAngle + arm)); }
            currentAngle -= 8f; 
            float currentDelay = 0.04f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern7_DoubleRingPulsar(float duration)
    {
        float elapsedTime = 0f;
        bool toggle = false;
        while (elapsedTime < duration)
        {
            int bulletCount = Mathf.Max(6, Mathf.RoundToInt(18f * (intensityMultiplier / 20f)));
            float angleStep = 360f / bulletCount;
            float offset = toggle ? 10f : 0f; 
            for (int i = 0; i < bulletCount; i++) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, (i * angleStep) + offset)); }
            toggle = !toggle;
            float currentDelay = 0.2f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern8_Sweep(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float sweepAngle = Mathf.PingPong(Time.time * 150f, 100f) + 130f; 
            SpawnBullet(transform.position, Quaternion.Euler(0, 0, sweepAngle));
            float currentDelay = 0.05f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay; 
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern9_OctagonMachinegun(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            for (int i = 0; i < 8; i++) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, i * 45f)); }
            float currentDelay = 0.1f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private IEnumerator Pattern10_RandomSpam(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            int spamCount = Mathf.Max(1, Mathf.RoundToInt(5f * (intensityMultiplier / 20f)));
            for(int i=0; i < spamCount; i++) { SpawnBullet(transform.position, Quaternion.Euler(0, 0, Random.Range(0f, 360f))); }
            float currentDelay = 0.04f * (20f / Mathf.Max(1f, intensityMultiplier));
            elapsedTime += currentDelay;
            yield return new WaitForSeconds(currentDelay);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        if (!isRelocating)
        {
            timeSinceLastMove += Time.deltaTime;
            float hoverOffsetX = Mathf.Sin(Time.time * hoverSpeed) * hoverDistance;
            float hoverOffsetY = Mathf.Cos(Time.time * hoverSpeed * 0.8f) * hoverDistance; 
            transform.localPosition = currentAnchorPosition + new Vector3(hoverOffsetX, hoverOffsetY, 0f);

            if (timeSinceLastMove >= moveInterval)
            {
                timeSinceLastMove = 0f;
                StartCoroutine(MoveToNewPosition(GetRandomYellowZonePosition()));
            }
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCycleInterval)
        {
            attackTimer = 0f;
            ExecuteDualAttacks();
        }
    }

    private void ExecuteDualAttacks()
    {
        int pattern1 = Random.Range(1, 11);
        int pattern2 = Random.Range(1, 11);
        while (pattern2 == pattern1) { pattern2 = Random.Range(1, 11); }
        StartPattern(pattern1);
        StartPattern(pattern2);
    }

    private void StartPattern(int patternId)
    {
        switch (patternId)
        {
            case 1: StartCoroutine(Pattern1_Rain(attackDuration)); break;
            case 2: StartCoroutine(Pattern2_PulseRing(attackDuration)); break;
            case 3: StartCoroutine(Pattern3_TargetBurst(attackDuration)); break;
            case 4: StartCoroutine(Pattern4_Shotgun(attackDuration)); break;
            case 5: StartCoroutine(Pattern5_Spiral(attackDuration)); break;
            case 6: StartCoroutine(Pattern6_SpinningCross(attackDuration)); break;
            case 7: StartCoroutine(Pattern7_DoubleRingPulsar(attackDuration)); break;
            case 8: StartCoroutine(Pattern8_Sweep(attackDuration)); break;
            case 9: StartCoroutine(Pattern9_OctagonMachinegun(attackDuration)); break;
            case 10: StartCoroutine(Pattern10_RandomSpam(attackDuration)); break;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Playerbullet"))
        {
            TakeDamage(damageTaken); 
            
            // ✅ NEW: Tell the bullet to return itself to the pool!
            BulletBehavior pBulletScript = collision.GetComponent<BulletBehavior>();
            if (pBulletScript != null)
            {
                pBulletScript.ReturnToPool();
            }
        }
    }

    public void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;

        // CHANGED: The Stabilized Math Reward! Send exactly 0.00833 per hit directly to the brain.
        if (playerAgent != null && currentHp >= 0)
        {
            playerAgent.ReceiveExternalReward(0.00833f);    // #Rewards
        }

        // Smart UI Update
        if (arenaManager != null && arenaManager.forceUIOn && arenaManager.uiManager != null)
        {
            arenaManager.uiManager.UpdateBossHp(currentHp);
        }

        int currentHpPercent = Mathf.CeilToInt((currentHp / maxHp) * 100f);
        CheckBossPhase(currentHpPercent);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // CHANGED: We removed the time/speed math from here. 
        // The ArenaManager is the Referee, so we just tell it the Boss is dead!
        if (arenaManager != null)
        {
            arenaManager.HandleBossKill();
        }

        Destroy(gameObject);
    }
    
    private IEnumerator MoveToNewPosition(Vector3 targetPosition)
    {
        isRelocating = true;
        Vector3 startPosition = transform.localPosition;
        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / moveDuration);
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null; 
        }

        currentAnchorPosition = targetPosition;
        isRelocating = false;
    }

    private Vector3 GetRandomYellowZonePosition()
    {
        return new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);
    }
}