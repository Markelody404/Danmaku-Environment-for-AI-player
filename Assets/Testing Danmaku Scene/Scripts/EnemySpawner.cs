using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject enemyPrefab;
    public GameObject bossPrefab;

    [Header("Phase Timings")]
    public float phaseOneDuration = 290f; // Mobs stop spawning after this
    public float phaseTwoStartTime = 300f; // Boss spawns at this time

    [Header("Mob Spawn Settings")]
    public float spawnRate = 2f;
    public float minX = -10f;
    public float maxX = 10f;
    public float spawnY = 6f; 

    private ArenaManager arenaManager;
    private float spawnTimer = 0f;
    private bool bossSpawned = false;

    private void Awake()
    {
        // Grab the referee so we can listen for Ceasefires
        arenaManager = transform.parent.GetComponent<ArenaManager>();
    }

    private void Update()
    {
        if (arenaManager == null) return;

        // 1. Phase One: Mob Spawning (Only spawn if NOT in phase 2 and NOT in intermission)
        if (!arenaManager.isPhaseTwo && !arenaManager.isIntermission)
        {
            if (!arenaManager.isCeasefire)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= spawnRate)
                {
                    GameObject newEnemy = Instantiate(enemyPrefab, transform.parent);
                    newEnemy.transform.localPosition = new Vector3(Random.Range(minX, maxX), spawnY, 0f);
                    spawnTimer = 0f; 
                }
            }
        }
        // 2. Phase Two: Boss Spawning (Only spawns when ArenaManager says Phase 2 starts)
        else if (arenaManager.isPhaseTwo)
        {
            if (!bossSpawned)
            {
                GameObject currentBoss = Instantiate(bossPrefab, transform.parent);
                currentBoss.transform.localPosition = new Vector3(0, 3f, 0);
                bossSpawned = true;
                Debug.Log("EnemySpawner: Boss deployed!");
            }
        }
        // Notice there is no "else if (isIntermission)" logic here! 
        // During the 10-second break, the spawner literally does nothing.
    }

    public void ResetSpawner()
    {
        bossSpawned = false;
        spawnTimer = 0f;
        // Note: We don't need to Destroy the boss here anymore, because ArenaManager.CleanupEntities() nukes the whole board for us!
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 center = transform.position; 
        Vector3 startPoint = center + new Vector3(minX, spawnY, 0f);
        Vector3 endPoint = center + new Vector3(maxX, spawnY, 0f);
        Gizmos.DrawLine(startPoint, endPoint);
        Gizmos.DrawLine(startPoint + Vector3.down * 0.5f, startPoint + Vector3.up * 0.5f);
        Gizmos.DrawLine(endPoint + Vector3.down * 0.5f, endPoint + Vector3.up * 0.5f);
    }
}