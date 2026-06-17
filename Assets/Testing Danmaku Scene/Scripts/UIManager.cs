using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Phase & Timer UI")]
    public TextMeshProUGUI phaseText; 
    public TextMeshProUGUI timerText; 
    
    [Header("Agent Stats UI")]
    public TextMeshProUGUI agentNameText;
    public TextMeshProUGUI episodeText;
    public TextMeshProUGUI winLossRatioText;
    public TextMeshProUGUI avgSurvivalText;
    public TextMeshProUGUI phaseCompletionsText; // NEW: Tracks Phase 1 & 2 clears

    [Header("Arcade System UI")]
    public TextMeshProUGUI livesText; 
    public TextMeshProUGUI bulletCountText; // NEW: From your hierarchy
    public GameObject ceasefireWarning; 

    [Header("Boss HP UI")]
    public GameObject bossHpContainer; 
    public Slider bossHpSlider;

    [Header("Reward UI")]
    public TextMeshProUGUI rewardText;


    // This runs the exact frame you press Play
    private void Awake()
    {
        if (agentNameText != null) agentNameText.text = "Agent Name: ---";
        if (episodeText != null) episodeText.text = "Episode: 0";
        if (winLossRatioText != null) winLossRatioText.text = "Win/Loss Ratio: 0.00";
        if (avgSurvivalText != null) avgSurvivalText.text = "Average Survival Time: 00:00";
        if (rewardText != null) rewardText.text = "Reward Earned: 0.00";
        if (phaseCompletionsText != null) phaseCompletionsText.text = "Phase 1 Clears: 0 | Boss Kills: 0";
        if (bulletCountText != null) bulletCountText.text = "Bullet Count: 0";
    }

    // ✅ NEW: Tell the player the boss is coming!
    public void StartIntermissionUI()
    {
        if (phaseText != null) phaseText.text = "Intermission: Get Ready!";
        bossHpContainer.SetActive(false); 
    }

    // The ArenaManager will call this to update the screen
    public void UpdateBulletCount(int count)
    {
        if (bulletCountText != null)
        {
            bulletCountText.text = $"Bullet Count: {count}";
        }
    }

    // ✅ NEW: Now it takes the time directly from the ArenaManager
    public void UpdateTimerDisplay(float currentTrueTime)
    {
        int minutes = Mathf.FloorToInt(currentTrueTime / 60F);
        int seconds = Mathf.FloorToInt(currentTrueTime - minutes * 60);
        
        if (timerText != null)
        {
            timerText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds); 
        }
    }

    public void ResetAndStartUI()
    {
        // No more internal UI timers!
        phaseText.text = "Phase 1: Survive";
        bossHpContainer.SetActive(false); 
        SetCeasefireWarning(false); 
        UpdateTimerDisplay(0f); // Reset visual to 00:00
    }

    public void StartPhaseTwo(float bossMaxHp)
    {
        phaseText.text = "Phase 2: Assassinate";
        bossHpContainer.SetActive(true); 
        bossHpSlider.maxValue = bossMaxHp;
        bossHpSlider.value = bossMaxHp;
    }

    public void UpdateBossHp(float currentHp)
    {
        bossHpSlider.value = currentHp;
    }

    public void UpdateLives(int currentLives)
    {
        if (livesText != null) livesText.text = $"Lives: {currentLives}";
    }

    public void SetCeasefireWarning(bool isActive)
    {
        if (ceasefireWarning != null) ceasefireWarning.SetActive(isActive);
    }

    // Notice the new 'int phase1Clears' added at the very end of the parentheses!
    // ✅ FIXED: Removed 'int bulletCount' from the end of the parentheses!
    public void UpdateAgentStats(string name, int episodes, int wins, int losses, float avgSurvival, int phase1Clears)
    {
        // ✅ FIXED: Brought back the null-check armor so it never crashes!
        if (agentNameText != null) agentNameText.text = $"Agent Name: {name}";
        if (episodeText != null) episodeText.text = $"Episode: {episodes}";
        
        float ratio = losses == 0 ? wins : (float)wins / losses;
        if (winLossRatioText != null) winLossRatioText.text = $"Win/Loss Ratio: {ratio:F2}";

        int avgMin = Mathf.FloorToInt(avgSurvival / 60F);
        int avgSec = Mathf.FloorToInt(avgSurvival - avgMin * 60);
        if (avgSurvivalText != null) avgSurvivalText.text = string.Format("Avg Survival Time: {0:00}:{1:00}", avgMin, avgSec);

        if (phaseCompletionsText != null)
        {
            phaseCompletionsText.text = $"Phase 1 Clears: {phase1Clears} | Boss Kills: {wins}";
        }
    }

    public void UpdateRewardDisplay(float currentReward)
    {
        if (rewardText != null) rewardText.text = $"Reward Earned: {currentReward:F2}";
    }
}