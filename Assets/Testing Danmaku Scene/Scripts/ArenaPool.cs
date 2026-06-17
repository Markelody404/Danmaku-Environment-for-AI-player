using System.Collections.Generic;
using UnityEngine;

public class ArenaPool : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject enemyBulletPrefab;
    public int startingBullets = 100;

    // ✅ NEW: Keep track of how many bullets are currently flying
    [Header("Pool Stats")]
    public int activeBulletCount = 0; 

    private Queue<GameObject> bulletPool = new Queue<GameObject>();

    [Header("Player Bullet Pool")]
    public GameObject playerBulletPrefab;
    public int startingPlayerBullets = 50; // The player shoots fast, so pre-warm a good amount
    public int activePlayerBulletCount = 0; 
    private Queue<GameObject> playerBulletPool = new Queue<GameObject>();

    private void Awake()
    {
        for (int i = 0; i < startingBullets; i++)
        {
            GameObject bullet = Instantiate(enemyBulletPrefab, transform);
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
        // Pre-warm Player Bullets
        for (int i = 0; i < startingPlayerBullets; i++)
        {
            GameObject pBullet = Instantiate(playerBulletPrefab, transform);
            pBullet.SetActive(false);
            playerBulletPool.Enqueue(pBullet);
        }
    }

    public GameObject SpawnBullet(Vector3 position, Quaternion rotation)
    {
        // ✅ NEW: Increment the counter every time a bullet is fired
        activeBulletCount++; 

        if (bulletPool.Count > 0)
        {
            GameObject bullet = bulletPool.Dequeue();
            bullet.transform.position = position;
            bullet.transform.rotation = rotation;
            bullet.SetActive(true);
            return bullet;
        }
        else
        {
            GameObject newBullet = Instantiate(enemyBulletPrefab, position, rotation, transform);
            return newBullet;
        }
    }

    public void ReturnBullet(GameObject bullet)
    {
        // ✅ NEW: Prevent double-counting if a bullet tries to return itself twice
        if (bullet.activeSelf) 
        {
            activeBulletCount--; 
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }

    // Similar methods can be created for player bullets if needed
    public GameObject SpawnPlayerBullet(Vector3 position, Quaternion rotation)
    {
        activePlayerBulletCount++; 

        if (playerBulletPool.Count > 0)
        {
            GameObject pBullet = playerBulletPool.Dequeue();
            pBullet.transform.position = position;
            pBullet.transform.rotation = rotation;
            pBullet.SetActive(true);
            return pBullet;
        }
        else
        {
            return Instantiate(playerBulletPrefab, position, rotation, transform);
        }
    }

    public void ReturnPlayerBullet(GameObject pBullet)
    {
        if (pBullet.activeSelf) 
        {
            activePlayerBulletCount--; 
            pBullet.SetActive(false);
            playerBulletPool.Enqueue(pBullet);
        }
    }
}