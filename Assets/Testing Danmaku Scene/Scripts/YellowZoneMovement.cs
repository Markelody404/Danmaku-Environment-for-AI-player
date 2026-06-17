using UnityEngine;

public class YellowZoneMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 3f;
    public float rotationSpeed = 250f; 

    [Header("Yellow Zone Boundaries")]
    public float minX = -7.03f;
    public float maxX = 5.31f;
    public float minY = 5.2f;
    public float maxY = 9.45f;

    [Header("Life Cycle")]
    public float lifetime = 25f; 
    public float retreatY = 8f;  

    [Header("Health Settings")]
    public int maxHealth = 5; // CHANGED: Set exactly to 5 as requested
    private int currentHealth;

    private Vector3 targetPosition;
    private float timeAlive = 0f;
    private bool isRetreating = false;

    private PlayerAgent playerAgent;

    private void Start()
    {
        currentHealth = maxHealth;
        PickNewTarget();

        // Localized target finding
        playerAgent = transform.parent.GetComponentInChildren<PlayerAgent>();
    }

    private void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        timeAlive += Time.deltaTime;

        if (timeAlive >= lifetime && !isRetreating)
        {
            isRetreating = true;
            targetPosition = new Vector3(transform.localPosition.x, retreatY, 0f); 
        }

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, speed * Time.deltaTime);

        if (!isRetreating)
        {
            if (Vector3.Distance(transform.localPosition, targetPosition) < 0.1f)
            {
                PickNewTarget();
            }
        }
        else
        {
            if (transform.localPosition.y >= retreatY - 0.1f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void PickNewTarget()
    {
        float randomX = Random.Range(minX, maxX);
        float randomY = Random.Range(minY, maxY);
        targetPosition = new Vector3(randomX, randomY, 0f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Playerbullet"))
        {
            TakeDamage(1);
            
            // ✅ THE FIX: Ask the bullet to politely return to its pool instead of destroying it!
            BulletBehavior bullet = collision.GetComponent<BulletBehavior>();
            if (bullet != null)
            {
                bullet.ReturnToPool();
            }
        }
    }

    private void TakeDamage(int damage)
    {
        currentHealth -= damage;
        
        if (playerAgent != null)
        {
            // 1. The Breadcrumb Reward: Give 0.005 for every successful hit
            playerAgent.ReceiveExternalReward(0.005f); // #Rewards
            
            if (currentHealth <= 0)
            {
                // 2. The Kill Bonus: Give an extra 0.025 for finishing the job
                playerAgent.ReceiveExternalReward(0.025f); // #Rewards
            }
        }

        // 3. Cleanup the dead mob
        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }
}