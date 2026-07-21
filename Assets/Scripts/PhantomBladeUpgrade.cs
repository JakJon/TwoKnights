using UnityEngine;

// Shadow discipline: sword swings leave dark after-images that repeat the swing
// at half damage.
[CreateAssetMenu(fileName = "PhantomBladeUpgrade", menuName = "Upgrades/Phantom Blade")]
public class PhantomBladeUpgrade : BaseUpgrade
{
    [SerializeField] private int echoCount = 1;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Phantom Blade";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        NinjaBoost boost = targetKnight.GetComponent<NinjaBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<NinjaBoost>();
        }

        boost.SetPhantomLevel(echoCount);

        Debug.Log($"Applied Phantom Blade to {targetKnight.name}. Echoes: {boost.PhantomLevel}");
    }
}
