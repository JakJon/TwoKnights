using UnityEngine;

// Shadow and Serpent at once: the dart is the ninja's timing and the venom's
// patience in one shot. Gated behind two of EACH order (see BaseUpgrade's
// second-order requirement) so it only ever turns up in a build that has
// already committed to both.
[CreateAssetMenu(fileName = "SleepingDartUpgrade", menuName = "Upgrades/Sleeping Dart")]
public class SleepingDartUpgrade : BaseUpgrade
{
    [Tooltip("Every Nth arrow leaves as a dart instead.")]
    [SerializeField] private int shotsPerDart = 5;

    [Tooltip("Seconds the enemy it hits stands still.")]
    [SerializeField] private float sleepSeconds = 5f;

    [Tooltip("The dart the arrow wears on its way out.")]
    [SerializeField] private Sprite dartSprite;

    [Tooltip("The Z that drifts off a sleeping enemy.")]
    [SerializeField] private Sprite sleepZSprite;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Sleeping Dart";
        if (weight == 0f)
            weight = 55f; // Rare
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        SleepBoost boost = targetKnight.GetComponent<SleepBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<SleepBoost>();
        }

        boost.SetDart(shotsPerDart, sleepSeconds, dartSprite);
        SleepFx.SetZSprite(sleepZSprite);

        Debug.Log($"Applied Sleeping Dart to {targetKnight.name}. Every {boost.ShotsPerDart} shots, {boost.SleepSeconds}s sleep.");
    }
}
