using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBulletFly : MonoBehaviour
{
    public float speed = 5f;
    public float lifetime = 5f;
    private Rigidbody2D rb;
    private ArenaPool arenaPool; // Reference to the pool
    private bool isReturning = false; // ✅ The lock to prevent double-triggering!

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Replace Start() with OnEnable()
    // Start only runs ONCE when instantiated. OnEnable runs every time the pool sets it Active!
    void OnEnable() 
    { 
        isReturning = false; // ✅ Reset the lock when the bullet wakes up
        rb.linearVelocity = transform.up * speed;
        
        // Try to find the pool if we don't have it yet
        if (arenaPool == null && transform.parent != null)
        {
            arenaPool = transform.parent.GetComponent<ArenaPool>();
        }

        // Start the lifetime timer
        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!gameObject.activeInHierarchy || isReturning) return;
        if (collision.CompareTag("Wall"))
        {
            // Instead of Destroy(0.3f), we wait 0.3s then return to pool
            StartCoroutine(DelayedReturn(0.3f));
        }
        else if (collision.CompareTag("WallTop"))
        {
            if (rb.linearVelocity.y > 0) 
            {
                StartCoroutine(DelayedReturn(0.3f));
            }
        }
    }

    private IEnumerator DelayedReturn(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturning) return; // ✅ If already returning, stop here.
        if (arenaPool != null)
        {
            // The pool handles SetActive(false)
            arenaPool.ReturnBullet(gameObject); 
        }
        else
        {
            // Fallback just in case the bullet somehow gets separated from the arena
            Destroy(gameObject); 
        }
    }
}