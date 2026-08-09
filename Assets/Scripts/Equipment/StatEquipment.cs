using UnityEngine;

/// <summary>
/// The workhorse equipment class: one type covering every item whose effect is
/// "write these numbers into the knight's existing boosts".
///
/// Twelve near-identical one-line classes would be twelve files to keep in step
/// for no gain — what actually differs between these items lives in the .asset,
/// not the code. Items with genuinely distinct behaviour still get their own
/// class (see FamilyBaneEquipment, BlastWardEquipment).
///
/// Every field defaults to inert, so an asset only fills in the one or two lines
/// it cares about.
/// </summary>
[CreateAssetMenu(fileName = "Equipment", menuName = "Equipment/Stat Equipment")]
public class StatEquipment : EquipmentDefinition
{
    [Header("The knight")]
    [SerializeField] private int maxHealthBonus = 0;
    [Tooltip("Multiplies the shot cooldown. 0.9 = a touch faster. 1 = unchanged.")]
    [SerializeField] private float fireCooldownMultiplier = 1f;
    [Tooltip("An upgrade pick before the first wave.")]
    [SerializeField] private bool grantsHeadStart = false;

    [Header("Draft weighting")]
    [Tooltip("Which Order this item makes more likely to appear in the knight's drafts.")]
    [SerializeField] private UpgradeOrder favouredOrder = UpgradeOrder.Neutral;
    [Tooltip("How much likelier. 2 = twice as common. 1 = no change.")]
    [SerializeField] private float favouredOrderMultiplier = 1f;

    [Header("Serpent")]
    [SerializeField] private int poisonTickBonus = 0;
    [Tooltip("Poison chance the knight starts the run with, before any Venom Tip.")]
    [SerializeField] private float startingPoisonChance = 0f;
    [Tooltip("Special charge granted when something dies of your venom.")]
    [SerializeField] private int poisonDeathSpecial = 0;

    [Header("Ember")]
    [SerializeField] private float igniteDpsBonus = 0f;
    [Tooltip("Ignite chance the knight starts the run with, before any Ignited Tips.")]
    [SerializeField] private float startingIgniteChance = 0f;
    [SerializeField] private float fireTrailDurationBonus = 0f;

    [Header("Shadow")]
    [Tooltip("Added to the shadow arrow damage multiplier. 0.15 = arrows land noticeably harder.")]
    [SerializeField] private float shadowArrowDamageBonus = 0f;
    [Tooltip("Extra sword echoes, on top of whatever Phantom Blade granted.")]
    [SerializeField] private int phantomEchoBonus = 0;
    [Tooltip("Grants this many shadow arrows from wave one. Needs the shadow arrow prefab.")]
    [SerializeField] private int startingShadowArrows = 0;
    [SerializeField] private GameObject shadowArrowPrefab;

    [Header("Utility")]
    [Tooltip("Scales how long confusion lasts. 0.35 = it wears off fast. 1 = unchanged.")]
    [SerializeField] private float confusionDurationMultiplier = 1f;

    public override void Apply(GameObject knight)
    {
        var equipment = BoostOn(knight);

        // --- the knight itself ---
        if (maxHealthBonus > 0)
        {
            knight.GetComponent<PlayerHealth>()?.IncreaseMaxHealth(maxHealthBonus);
        }

        if (fireCooldownMultiplier < 1f)
        {
            var shooter = knight.GetComponent<PlayerShooter>();
            if (shooter != null) shooter.cooldownTime *= fireCooldownMultiplier;
        }

        if (grantsHeadStart) equipment.GrantHeadStart();

        // Bends what the knight is OFFERED rather than what they have. Neutral is
        // ignored deliberately — it is the fallback pool, not a build.
        if (favouredOrder != UpgradeOrder.Neutral && favouredOrderMultiplier > 1f)
        {
            equipment.MultiplyOrderDraft(favouredOrder, favouredOrderMultiplier);
        }

        // --- Serpent: routed through the Order's own boost, never a parallel path ---
        if (poisonTickBonus > 0 || startingPoisonChance > 0f)
        {
            var poison = knight.GetComponent<PoisonTipBoost>() ?? knight.AddComponent<PoisonTipBoost>();
            if (poisonTickBonus > 0) poison.AddTickDamage(poisonTickBonus);
            if (startingPoisonChance > 0f) poison.IncreasePoisonChance(startingPoisonChance);
        }
        if (poisonDeathSpecial > 0) equipment.AddPoisonDeathSpecial(poisonDeathSpecial);

        // --- Ember ---
        if (igniteDpsBonus > 0f || startingIgniteChance > 0f || fireTrailDurationBonus > 0f)
        {
            var ember = knight.GetComponent<EmberBoost>() ?? knight.AddComponent<EmberBoost>();
            if (igniteDpsBonus > 0f) ember.AddZoneDpsBonus(igniteDpsBonus);
            if (fireTrailDurationBonus > 0f) ember.AddTrailDurationBonus(fireTrailDurationBonus);
            // SetIgniteChance keeps the larger value, so this can only ever be a
            // floor under whatever Ignited Tips goes on to grant
            if (startingIgniteChance > 0f) ember.SetIgniteChance(startingIgniteChance);
        }

        // --- Shadow ---
        if (shadowArrowDamageBonus > 0f) equipment.AddShadowArrowDamage(shadowArrowDamageBonus);
        if (phantomEchoBonus > 0)
        {
            var ninja = knight.GetComponent<NinjaBoost>() ?? knight.AddComponent<NinjaBoost>();
            ninja.AddPhantomEcho(phantomEchoBonus);
        }
        if (startingShadowArrows > 0 && shadowArrowPrefab != null)
        {
            var shadow = knight.GetComponent<ShadowArrowBoost>() ?? knight.AddComponent<ShadowArrowBoost>();
            // Same numbers ShadowArrowUpgrade uses, so an arrow granted here is
            // indistinguishable from a drafted one
            shadow.SetShadowArrowSettings(shadowArrowPrefab, startingShadowArrows, 0.2f, 0.35f, 0.2f);
        }

        // --- utility ---
        if (confusionDurationMultiplier < 1f) equipment.ShortenConfusion(confusionDurationMultiplier);
    }
}
