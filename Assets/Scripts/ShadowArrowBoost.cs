using UnityEngine;

// Component to handle shadow arrow spawning
public class ShadowArrowBoost : MonoBehaviour
{
    private GameObject shadowArrowPrefab;
    private float damageMultiplier = 0.33f;
    private float trailDistance = 0.5f;
    private float positionVariance = 0.3f;
    
    public void SetShadowArrowSettings(GameObject prefab, float damageMultiplier, float trailDistance, float positionVariance)
    {
        this.shadowArrowPrefab = prefab;
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
