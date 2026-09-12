using UnityEngine;

// Guided Reflections (Guardian): the point where the Order's two halves are welded
// together. A rock the guard turned around now steers the way the knight's own arrows
// do, so a rebound stops being a hopeful line off the shield face and starts being
// something that finds a body.
//
// It is the one upgrade in the game that needs BOTH prerequisites rather than either
// — see BaseUpgrade.requiresAllUnlocks, which exists for this. A knight who can only
// reflect has nothing to steer, and one who can only steer has nothing to send.
//
// Bodies only. A rock cannot collect an orb (CollectibleOrb answers to arrows alone),
// so GuidedShot is configured to ignore them here — see ProjectileSettings.TryReflect.
[CreateAssetMenu(fileName = "GuidedReflectionsUpgrade", menuName = "Upgrades/Guided Reflections")]
public class GuidedReflectionsUpgrade : BaseUpgrade
{
    [Tooltip("Rank this tier grants: 1, 2 or 3. Radii match Guided Shot's, deliberately — it is the same steering, bought again for the rock.")]
    [SerializeField] private int rank = 1;

    public override string ChainName => "Guided Reflections";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Guided Reflections";
        if (weight == 0f)
            weight = 30f; // Epic
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        GuardianBoost boost = targetKnight.GetComponent<GuardianBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<GuardianBoost>();
        }

        boost.SetGuidedReflections(rank);

        Debug.Log($"Applied Guided Reflections to {targetKnight.name}: rank {boost.GuidedReflectionsLevel}, " +
                  $"{boost.GuidedReflectionRadius:F2}u radius");
    }
}
