using UnityEngine;

// Component to track poison tip upgrade bonuses
public class PoisonTipBoost : MonoBehaviour
{
    private float poisonChance = 0f; // Percentage chance (0-100)
    
    public void IncreasePoisonChance(float amount)
    {
        poisonChance = Mathf.Clamp(poisonChance + amount, 0f, 100f);
    }
    
    public float GetPoisonChance()
    {
        return poisonChance;
    }
    
    // Check if this shot should be poisoned based on chance
    public bool ShouldApplyPoison()
    {
        return Random.Range(0f, 100f) < poisonChance;
    }
}
