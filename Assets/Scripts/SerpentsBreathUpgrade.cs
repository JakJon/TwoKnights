using UnityEngine;

// Serpent discipline: sword swings have a chance to exhale a venom cloud that
// drifts in the direction the shield is pointing, poisoning enemies it touches.
[CreateAssetMenu(fileName = "SerpentsBreathUpgrade", menuName = "Upgrades/Serpent's Breath")]
public class SerpentsBreathUpgrade : BaseUpgrade
{
    [SerializeField] private float cloudChance = 33f; // Percent chance per swing
    [SerializeField] private float cloudDuration = 3f; // Seconds the cloud travels
    [SerializeField] private bool largeCloud = false; // Tier III breathes big

    public override string ChainName => "Serpent's Breath";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Serpent's Breath";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.SetSwordCloud(cloudChance, cloudDuration, largeCloud);

        Debug.Log($"Applied Serpent's Breath to {targetKnight.name}: {cloudChance}% chance, {cloudDuration}s, large={largeCloud}");
    }
}
