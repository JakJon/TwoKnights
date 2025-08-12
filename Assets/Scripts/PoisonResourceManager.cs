using UnityEngine;

[CreateAssetMenu(fileName = "PoisonResourceManager", menuName = "Two Knights/Poison Resource Manager")]
public class PoisonResourceManager : ScriptableObject
{
    [Header("Poison Visual Resources")]
    [Tooltip("Prefab to use for poison bubble effects")]
    public GameObject poisonBubblePrefab;
    
    [Tooltip("Sprite to use for poison bubbles (fallback if no prefab)")]
    public Sprite poisonBubbleSprite;
    
    [Header("Settings")]
    [Tooltip("Default bubble rate for enemies")]
    public float enemyBubbleRate = 2f;
    
    [Tooltip("Default bubble rate for projectiles")]
    public float projectileBubbleRate = 15f;
    
    // Singleton instance
    private static PoisonResourceManager _instance;
    public static PoisonResourceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<PoisonResourceManager>("PoisonResourceManager");
                if (_instance == null)
                {
                    Debug.LogWarning("PoisonResourceManager not found in Resources folder! Create one using Create > Two Knights > Poison Resource Manager");
                }
            }
            return _instance;
        }
    }
    
    public GameObject GetPoisonBubblePrefab()
    {
        return poisonBubblePrefab;
    }
    
    public Sprite GetPoisonBubbleSprite()
    {
        return poisonBubbleSprite;
    }
}
