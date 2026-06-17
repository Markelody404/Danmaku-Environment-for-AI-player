    using UnityEngine;
    using Unity.MLAgents;
    using System.Collections;

    public class ArenaManager : MonoBehaviour
    {
        [Header("UI Integration")]
        public UIManager uiManager; 

        [Header("Ceasefire System")]
        public bool isCeasefire = false;

        [Header("Core References")]
        public PlayerAgent playerAgent;
        private EnemySpawner enemySpawner;
        public ArenaPool arenaPool; // ✅ NEW: Grab the pooler

        [Header("Phase Timers")]
        public float phase1Duration = 290f; 
        public float intermissionDuration = 10f; // ✅ NEW: 10-second break
        public float phase2Duration = 300f; 
        
        private float currentPhaseTime = 0f;
        public bool isIntermission = false; // ✅ NEW: Tracks the break
        public bool isPhaseTwo = false;

        [Header("Agent Stats Tracking")]
        private int totalEpisodes = 0;
        private int totalWins = 0;
        private int totalLosses = 0;
        private float totalSurvivalTime = 0f;
        private int phase1ClearCount = 0; 

        [Header("Debug & Testing")]
        public bool forceUIOn = true;

        // Add this near your other [Header] sections at the top of ArenaManager.cs:
        [Header("Training Curriculum")]
        [Tooltip("Check this box if this arena should ONLY play Phase 2 (Boss).")]
        public bool isBossPracticeArena = false;

        private void Awake()
        {
            playerAgent = GetComponentInChildren<PlayerAgent>();
            enemySpawner = GetComponentInChildren<EnemySpawner>();
            arenaPool = GetComponent<ArenaPool>(); // ✅ NEW

            if (playerAgent == null) Debug.LogError($"ArenaManager in {gameObject.name} is missing a PlayerAgent!");
        }

        private void Update()
        {
            if (!isCeasefire)
            {
                currentPhaseTime += Time.deltaTime;
            }

            // ✅ The 3-Step Phase Logic
            if (!isPhaseTwo && !isIntermission) 
            {
                // Phase 1: Mobs
                if (currentPhaseTime >= phase1Duration) CompletePhaseOne();
            }
            else if (isIntermission)
            {
                // Phase 1.5: Intermission (Wait 10 seconds)
                if (currentPhaseTime >= intermissionDuration) BeginPhaseTwo();
            }
            else 
            {
                // Phase 2: Boss
                if (currentPhaseTime >= phase2Duration) HandleBossTimeOut();
            }

            // Bullet Radar
            int currentBullets = 0;
            foreach (Transform child in transform)
            {
                if (child.CompareTag("EnemyBullet")) currentBullets++;
            }

            if (forceUIOn && uiManager != null)
            {
                // ✅ THE FIX: The Referee pushes the single source of truth to the UI!
                uiManager.UpdateTimerDisplay(GetTotalElapsedTime());
                uiManager.UpdateRewardDisplay(playerAgent.GetCumulativeReward());
                if (arenaPool != null) 
                {
                    uiManager.UpdateBulletCount(arenaPool.activeBulletCount);
                }
            }
        }

        public void TriggerCeasefire(float duration)
        {
            StartCoroutine(CeasefireRoutine(duration));
        }

        private IEnumerator CeasefireRoutine(float duration)
        {
            isCeasefire = true;
            if (forceUIOn && uiManager != null) uiManager.SetCeasefireWarning(true);
            
            yield return new WaitForSeconds(duration);
            
            isCeasefire = false;
            if (forceUIOn && uiManager != null) uiManager.SetCeasefireWarning(false);
        }

        // --- Phase Transitions ---

        private void CompletePhaseOne()
        {
            playerAgent.ReceiveExternalReward(5f); // #Rewards
            CleanupEntities(true); 
            
            phase1ClearCount++; 
            
            isIntermission = true; // Trigger intermission!
            currentPhaseTime = 0f; 
            
            if (forceUIOn && uiManager != null) uiManager.StartIntermissionUI();
        }

        private void BeginPhaseTwo() 
        {
            isIntermission = false; // Turn off break
            isPhaseTwo = true;      // Start Boss phase
            currentPhaseTime = 0f;

            if (forceUIOn && uiManager != null) uiManager.StartPhaseTwo(1200f);
        }

        // Replace your GetTotalElapsedTime helper:
        private float GetTotalElapsedTime()
        {
            // ✅ NEW: If it's a boss-only arena, just return its raw time.
            if (isBossPracticeArena) return currentPhaseTime; 

            if (!isPhaseTwo && !isIntermission) return currentPhaseTime; // Died in Phase 1
            if (isIntermission) return phase1Duration + currentPhaseTime; // Died in Break
            return phase1Duration + intermissionDuration + currentPhaseTime; // Died in Phase 2
        }

        public void HandleBossKill()
        {
            float timeRemaining = phase2Duration - currentPhaseTime;
            float speedBonus = Mathf.Max(0, timeRemaining * 0.005f); //#Rewards
            
            playerAgent.ReceiveExternalReward(5 + speedBonus);   //#Rewards
            totalWins++; 
            totalSurvivalTime += GetTotalElapsedTime(); // Clean math!
            playerAgent.EndEpisode(); 
        }

        public void HandlePlayerDeath()
        {
            totalLosses++;
            totalSurvivalTime += GetTotalElapsedTime(); // Clean math!
            playerAgent.EndEpisode(); 
        }

        private void HandleBossTimeOut()
        {
            playerAgent.ReceiveExternalReward(-7f); //#Rewards 
            totalSurvivalTime += GetTotalElapsedTime(); // Clean math!
            playerAgent.EndEpisode();
        }

        // Replace your ResetArena method:
        public void ResetArena()
        {
            totalEpisodes++; 
            currentPhaseTime = 0f;
            isCeasefire = false;

            // --- CHECKLIST LOGIC ---
            if (!isBossPracticeArena)
            {
                // Normal 5 Arenas: Start at Phase 1 -> Intermission -> Phase 2
                isPhaseTwo = false;
                isIntermission = false;
                
                if (forceUIOn && uiManager != null) 
                {
                    uiManager.ResetAndStartUI(); 
                }
            }
            else
            {
                // The 4 Practice Arenas: Lock directly to Phase 2
                // No CompletePhaseOne() is called, so NO 50f reward is given!
                isPhaseTwo = true;
                isIntermission = false;

                if (forceUIOn && uiManager != null) 
                {
                    uiManager.ResetAndStartUI(); 
                    uiManager.StartPhaseTwo(1200f); 
                }
            }

            // --- Standard Reset Procedures ---
            if (enemySpawner != null) enemySpawner.ResetSpawner();
            CleanupEntities(false);

            if (forceUIOn && uiManager != null)
            {
                uiManager.UpdateLives(3); 
                float avgSurvival = totalEpisodes == 0 ? 0 : totalSurvivalTime / totalEpisodes;
                uiManager.UpdateAgentStats("DanmakuPPO", totalEpisodes, totalWins, totalLosses, avgSurvival, phase1ClearCount);
            }
        }

        private void CleanupEntities(bool phaseOneOnly)
        {
            foreach (Transform child in transform)
            {
                if (child == playerAgent.transform || child.name.Contains("Wall") || child.name.Contains("Spawner")) 
                    continue;

                bool isMob = child.GetComponent<YellowZoneMovement>() != null; 
                bool isEnemyTag = child.CompareTag("Enemy");
                bool isEnemyBullet = child.CompareTag("EnemyBullet");
                bool isPlayerBullet = child.CompareTag("Playerbullet");
                bool isBoss = child.CompareTag("Boss");

                if (phaseOneOnly)
                {
                    if (isEnemyBullet) arenaPool.ReturnBullet(child.gameObject); // ✅ Recycled!
                    else if (isPlayerBullet) arenaPool.ReturnPlayerBullet(child.gameObject); // ✅ Recycled Player Bullets!
                    else if (isMob || isEnemyTag) Destroy(child.gameObject);
                }
                else
                {
                    if (isEnemyBullet) arenaPool.ReturnBullet(child.gameObject); // ✅ Recycled!
                    else if (isPlayerBullet) arenaPool.ReturnPlayerBullet(child.gameObject); // ✅ Recycled Player Bullets!
                    else if (isMob || isEnemyTag || isBoss) Destroy(child.gameObject);
                }
            }
        }
    }