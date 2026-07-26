using UnityEngine;

// Ember discipline: every Nth arrow leaves the shield as a fireball that explodes,
// ignites everything caught, and leaves a burning crater. A deterministic counter
// rather than a roll, so the player can count to five and time the big one into a
// cluster — the only upgrade in the game that gives the shoot button a rhythm.
[CreateAssetMenu(fileName = "FireballUpgrade", menuName = "Upgrades/Fireball")]
public class FireballUpgrade : BaseUpgrade
{
    [SerializeField] private int everyNShots = 5;
    [SerializeField] private float blastRadius = 1.5f;
    [Tooltip("Fireball prefab — travels with the upgrade (house convention); PlayerShooter falls back to the arrow prefab when null")]
    [SerializeField] private GameObject fireballPrefab;

    public override string ChainName => "Fireball";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Fireball";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        boost.SetFireball(everyNShots, blastRadius, fireballPrefab);

        Debug.Log($"Applied Fireball to {targetKnight.name}: every {everyNShots} shots, blast {blastRadius}");
    }
}
