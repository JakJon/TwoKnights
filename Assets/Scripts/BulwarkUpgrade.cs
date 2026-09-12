using UnityEngine;

// Bulwark — the Guardian capstone.
//
// Every knight in the game answers a body that reaches the guard the same way: the
// enemy is deleted and the knight is charged health for it (EnemyBase.OnTriggerEnter2D,
// the Shield branch). It is a trade, and it is the trade that kills runs.
//
// Bulwark refuses it. Nothing is paid and nothing is deleted — the body is thrown two
// units back off the shield and has to make the walk again. That is a real cost as
// well as a real gift: the enemy is still alive, still coming, and still has to be
// killed properly. What the knight has bought is that reaching the guard is no longer
// worth anything to it.
//
// This is the Order's second pillar taken to its end — being hit is never the thing
// that pays out. Stalwart (blocks charge the special) was designed alongside this and
// cut for contradicting it; don't put it back.
[CreateAssetMenu(fileName = "BulwarkUpgrade", menuName = "Upgrades/Bulwark")]
public class BulwarkUpgrade : BaseUpgrade
{
    public override string ChainName => "Bulwark";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Bulwark";
        if (weight == 0f)
            weight = 12f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        GuardianBoost boost = targetKnight.GetComponent<GuardianBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<GuardianBoost>();
        }

        boost.SetBulwark();

        Debug.Log($"Applied Bulwark to {targetKnight.name}: bodies are thrown " +
                  $"{GuardianBoost.BulwarkShoveDistance}u off the guard instead of trading health.");
    }
}
