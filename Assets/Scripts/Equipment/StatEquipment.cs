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
    [Tooltip("Ignite chance added on top of any Ignited Tips.")]
    [SerializeField] private float startingIgniteChance = 0f;
    [Tooltip("Units added to the radius of every fire zone the knight lays down - trails and fireball craters alike.")]
    [SerializeField] private float fireZoneRadiusBonus = 0f;

    [Header("Dawn")]
    [Tooltip("Fraction of every heal echoed to the other knight from wave one, added on top of any Shared Light. 0.25 = a quarter.")]
    [SerializeField] private float startingEchoFraction = 0f;
    [Tooltip("Multiplies what health orbs heal, from wave one, on top of any Sunwell. 1.5 = half again.")]
    [SerializeField] private float startingOrbHealMultiplier = 0f;

    [Header("Frigid")]
    [Tooltip("Multiplies how long this knight's chill lingers. 2 = twice as long, at every rank.")]
    [SerializeField] private float chillDurationMultiplier = 1f;
    [Tooltip("Seconds added to how long a freeze holds. Does nothing without Frost Tip - the knight still has to be able to freeze at all.")]
    [SerializeField] private float freezeDurationBonus = 0f;
    [Tooltip("Grants this many Frost Tip ranks from wave one, stacked on top of any drafted Frost Tip. Cold depth still tops out at rank III.")]
    [SerializeField] private int startingFrostTip = 0;

    [Header("Guardian")]
    [Tooltip("Multiplies what a rock sent back off the guard hits for. 2 = twice as hard. Does nothing without Reflector - the guard still has to be able to send one back at all.")]
    [SerializeField] private float reflectDamageMultiplier = 1f;
    [Tooltip("Grants the Long Sword at this rank from wave one, before any draft. Ranks COMPOUND, so an item granting rank one plus a drafted Long Sword I lands roughly where rank two would.")]
    [SerializeField] private int startingLongSword = 0;

    [Header("Shadow")]
    [Tooltip("Added to the shadow arrow AND shuriken damage multipliers. 0.15 = both land noticeably harder.")]
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
        if (igniteDpsBonus > 0f || startingIgniteChance > 0f || fireZoneRadiusBonus > 0f)
        {
            var ember = knight.GetComponent<EmberBoost>() ?? knight.AddComponent<EmberBoost>();
            if (igniteDpsBonus > 0f) ember.AddZoneDpsBonus(igniteDpsBonus);
            if (fireZoneRadiusBonus > 0f) ember.AddZoneRadiusBonus(fireZoneRadiusBonus);
            // Added on top of whatever Ignited Tips goes on to grant, not a floor
            // under it — equipment always stacks
            if (startingIgniteChance > 0f) ember.AddIgniteChanceFromEquipment(startingIgniteChance);
        }

        // --- Dawn: routed through the Order's own sheet, never a parallel path.
        //     Both land on top of what Shared Light and Sunwell go on to grant.
        if (startingEchoFraction > 0f || startingOrbHealMultiplier > 0f)
        {
            var dawn = knight.GetComponent<DawnBoost>() ?? knight.AddComponent<DawnBoost>();
            if (startingEchoFraction > 0f) dawn.AddEchoFractionFromEquipment(startingEchoFraction);
            if (startingOrbHealMultiplier > 0f) dawn.MultiplyOrbHealFromEquipment(startingOrbHealMultiplier);
        }

        // --- Frigid: routed through the Order's own sheet, never a parallel path ---
        if (chillDurationMultiplier > 1f || freezeDurationBonus > 0f || startingFrostTip > 0)
        {
            var frigid = knight.GetComponent<FrigidBoost>() ?? knight.AddComponent<FrigidBoost>();
            if (chillDurationMultiplier > 1f) frigid.MultiplyChillDuration(chillDurationMultiplier);
            if (freezeDurationBonus > 0f) frigid.AddFreezeDurationBonus(freezeDurationBonus);
            // Ranks on top of whatever the draft goes on to hand the knight
            if (startingFrostTip > 0) frigid.AddFrostTipFromEquipment(startingFrostTip);
        }

        // --- Guardian ---
        if (reflectDamageMultiplier > 1f)
        {
            var guardian = knight.GetComponent<GuardianBoost>() ?? knight.AddComponent<GuardianBoost>();
            guardian.MultiplyReflectDamage(reflectDamageMultiplier);
        }
        if (startingLongSword > 0)
        {
            // The same numbers Long Sword I is authored with. AddRank compounds by
            // design, so this is a floor the draft builds on rather than a rank the
            // draft has to beat - see LongSwordBoost.
            var blade = knight.GetComponent<LongSwordBoost>() ?? knight.AddComponent<LongSwordBoost>();
            for (int i = 0; i < startingLongSword; i++) blade.AddRank(1.33f, 1.25f, 15);
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
