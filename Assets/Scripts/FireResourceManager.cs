using UnityEngine;

// Singleton resource holder for Ember Order visuals, mirroring
// PoisonResourceManager. Resources.Load is only sanctioned for this true
// singleton pattern — individual sprites/prefabs travel on serialized fields.
[CreateAssetMenu(fileName = "FireResourceManager", menuName = "Two Knights/Fire Resource Manager")]
public class FireResourceManager : ScriptableObject
{
    [Header("Ember Visual Resources")]
    [Tooltip("Soft ember mote used by fire zones, craters and ignited-arrow trails (drawn WHITE, tinted in code)")]
    public Sprite emberMoteSprite;

    private static FireResourceManager _instance;
    public static FireResourceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<FireResourceManager>("FireResourceManager");
                if (_instance == null)
                {
                    Debug.LogWarning("FireResourceManager not found in Resources folder! Create one using Create > Two Knights > Fire Resource Manager");
                }
            }
            return _instance;
        }
    }

    public Sprite GetEmberMoteSprite()
    {
        return emberMoteSprite;
    }
}
