using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BulletBehavior : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 80f; 
    public float failsafeLifetime = 1f; 
    private Rigidbody2D rb;
    private ArenaPool arenaPool; // Reference to our pool

    private bool isReturning = false; // ✅ Prevents double-triggering

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        isReturning = false; // ✅ Reset the lock every time it wakes up
        rb.linearVelocity = transform.up * speed;

        // Try to grab the pool if we don't have it
        if (arenaPool == null && transform.parent != null)
        {
            arenaPool = transform.parent.GetComponent<ArenaPool>();
        }

        // Start the lifetime timer
        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(failsafeLifetime);
        ReturnToPool();
    }

private void OnTriggerEnter2D(Collider2D collision)
    {
        // Safety check to prevent double-triggering
        if (!gameObject.activeInHierarchy || isReturning) return;

        if (collision.CompareTag("WallTop"))
        {
            // Stop and do the natural phasing delay at the top
            rb.linearVelocity = Vector2.zero; 
            StartCoroutine(DelayedReturn(0.1f));
        }
        else if (collision.CompareTag("Wall"))
        {
            // Check if the bullet is traveling upwards (Y velocity is positive)
            if (rb.linearVelocity.y > 0)
            {
                // Ignore the delayed return completely and let it keep flying!
                return; 
            }
            
            // Fallback: If it hits a side wall and is NOT going up, stop and return it
            rb.linearVelocity = Vector2.zero; 
            StartCoroutine(DelayedReturn(0.1f));
        }
    }

    private IEnumerator DelayedReturn(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    // Made public so enemies/bosses can force the bullet back into the pool when it hits them
    public void ReturnToPool()
    {
        if (isReturning) return; // ✅ If already returning, do nothing!
        isReturning = true;
        if (arenaPool != null)
        {
            arenaPool.ReturnPlayerBullet(gameObject);
        }
        else
        {
            Destroy(gameObject); // Fallback
        }
    }
}