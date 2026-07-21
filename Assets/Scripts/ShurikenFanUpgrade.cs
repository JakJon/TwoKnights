using UnityEngine;

// Shadow discipline: the main shot splits into angled shurikens at 35% damage.
[CreateAssetMenu(fileName = "ShurikenFanUpgrade", menuName = "Upgrades/Shuriken Fan")]
public class ShurikenFanUpgrade : BaseUpgrade
{
    [SerializeField] private int fanLevel = 1; // 1 = pair at ±12°, 2 = + pair at ±24°
    [SerializeField] private GameObject shurikenPrefab; // spun steel star; fan falls back to the arrow prefab if unset

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Shuriken Fan";
        if (weight == 0f)
            weight = 60f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        NinjaBoost boost = targetKnight.GetComponent<NinjaBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<NinjaBoost>();
        }

        boost.SetShurikenFan(fanLevel, shurikenPrefab);

        Debug.Log($"Applied Shuriken Fan to {targetKnight.name}. Level: {boost.ShurikenLevel}");
    }
}
