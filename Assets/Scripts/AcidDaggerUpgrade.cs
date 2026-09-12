using UnityEngine;

// Serpent capstone: half a second after every sword swing, a small acid-coated
// dagger jabs straight out along the shield facing — a short, narrow stab that
// reaches two thirds as far as the sword — dealing a heavy flat blow and
// poisoning what it lands on. SwordSwing throws it; this only switches it on.
//
// Replaced Plaguebringer as the capstone on 2026-09-10. Plaguebringer (hunting
// clouds) moved to the Serpent + Guardian combo slot. Availability is gated by
// requiresOrderCount on the asset (owned Serpent upgrades).
//
// The dagger's sprite cannot be written into the .asset by hand — an .aseprite's
// sprite id is minted at import — so Assets/Editor/NewUpgradeRegistration wires it.
[CreateAssetMenu(fileName = "AcidDaggerUpgrade", menuName = "Upgrades/Acid Dagger")]
public class AcidDaggerUpgrade : BaseUpgrade
{
    [Tooltip("Flat damage of each jab, before equipment banes.")]
    [SerializeField] private int daggerDamage = 50;

    [Tooltip("The dagger drawn during the jab (acid_dagger.aseprite).")]
    [SerializeField] private Sprite daggerSprite;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Acid Dagger";
        if (weight == 0f)
            weight = 10f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.EnableAcidDagger(daggerDamage, daggerSprite);

        Debug.Log($"Applied Acid Dagger to {targetKnight.name}: a {boost.AcidDaggerDamage}-damage jab follows every swing.");
    }
}
