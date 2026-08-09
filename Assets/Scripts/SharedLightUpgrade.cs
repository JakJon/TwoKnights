using UnityEngine;

// Dawn discipline: the partnership door, and the Order's signature. A fraction
// of every heal this knight RECEIVES is echoed to the other one — from any
// source, because PlayerHealth.Heal is the single funnel all healing passes
// through. Rank II adds the half that makes Dawn never waste: healing taken at
// full health is handed to the partner instead of evaporating.
//
// The echo is deliberately not itself echoable (PlayerHealth passes
// allowEcho: false), so two Dawn knights lift each other rather than looping.
[CreateAssetMenu(fileName = "SharedLightUpgrade", menuName = "Upgrades/Shared Light")]
public class SharedLightUpgrade : BaseUpgrade
{
    [SerializeField] private float echoFraction = 0.4f;
    [SerializeField] private bool routeOverheal = false; // rank II

    public override string ChainName => "Shared Light";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Shared Light";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.SetEchoFraction(echoFraction);
        if (routeOverheal) boost.EnableOverhealRouting();

        Debug.Log($"Applied Shared Light to {targetKnight.name}: {echoFraction:P0} echo, overheal routing {routeOverheal}");
    }
}
