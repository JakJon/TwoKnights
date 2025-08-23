using UnityEngine;

// Component to handle shadow arrow spawning
public class ShadowArrowBoost : MonoBehaviour
{
    private GameObject shadowArrowPrefab;
    private int shadowArrowAmount = 1;
    private float damageMultiplier = 0.2f;
    private float trailDistance = 0.35f;
    private float positionVariance = 0.1f;
    
    // Backward-compatible overload: defaults to one shadow arrow
    public void SetShadowArrowSettings(GameObject prefab, float damageMultiplier, float trailDistance, float positionVariance)
    {
        SetShadowArrowSettings(prefab, 1, damageMultiplier, trailDistance, positionVariance);
    }

    public void SetShadowArrowSettings(GameObject prefab, int amount, float damageMultiplier, float trailDistance, float positionVariance)
    {
        this.shadowArrowPrefab = prefab;
        this.shadowArrowAmount = Mathf.Max(0, amount);
        this.damageMultiplier = damageMultiplier;
        this.trailDistance = trailDistance;
        this.positionVariance = positionVariance;
    }
    
    public GameObject GetShadowArrowPrefab()
    {
        return shadowArrowPrefab;
    }
    
    public float GetDamageMultiplier()
    {
        return damageMultiplier;
    }
    
    public int GetShadowArrowAmount()
    {
        return shadowArrowAmount;
    }
    
    public Vector2 GetShadowSpawnPosition(Vector2 mainProjectilePosition, Vector2 direction)
    {
        // Calculate base position behind main projectile
        Vector2 basePosition = mainProjectilePosition - (direction * trailDistance);
        
        // Add random variance
        float varianceX = Random.Range(-positionVariance, positionVariance);
        float varianceY = Random.Range(-positionVariance, positionVariance);
        
        return basePosition + new Vector2(varianceX, varianceY);
    }
}
