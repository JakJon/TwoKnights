using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-knight stat sheet written by equipment at run start and read by the
/// combat systems — the same shape as NinjaBoost, EmberBoost and PoisonTipBoost.
///
/// Equipment never holds gameplay logic. An item's Apply writes numbers in here;
/// the systems that already own the behaviour (PlayerHealth, arrows, the sword)
/// read them. That keeps a new item from reaching into combat code, and keeps
/// combat code from having to know what an item is.
///
/// Lives on the knight GameObject, so it dies with the scene. There is no
/// surviving run state to clear the way UpgradeManager.ResetRunState has to,
/// because nothing here outlives the run.
///
/// EVERYTHING STACKS. Two items touching the same number both count: a
/// multiplier an item advertises (1.5x damage, 2x as often) multiplies with the
/// others, and a flat amount (+15%, +8 charge) adds to them. Nothing here keeps
/// only the best one on offer.
/// </summary>
public class EquipmentBoost : MonoBehaviour
{
    // Each knight's sheet, by the knight's tag, so a hit can find its owner's
    // equipment from the tag it carries without a scene search per hit. A knight
    // carrying nothing never gets a sheet, and finds nothing here.
    private static readonly Dictionary<string, EquipmentBoost> _byKnightTag =
        new Dictionary<string, EquipmentBoost>();

    private void OnEnable()
    {
        _byKnightTag[gameObject.tag] = this;
    }

    private void OnDisable()
    {
        if (_byKnightTag.TryGetValue(gameObject.tag, out EquipmentBoost current) && current == this)
        {
            _byKnightTag.Remove(gameObject.tag);
        }
    }

    /// <summary>"PlayerLeft" / "PlayerRight" -> that knight's sheet, or null.</summary>
    public static EquipmentBoost ForKnight(string knightTag)
    {
        if (string.IsNullOrEmpty(knightTag)) return null;
        return _byKnightTag.TryGetValue(knightTag, out EquipmentBoost boost) ? boost : null;
    }

    /// <summary>
    /// One hit a knight's weapon lands on <paramref name="target"/>, with that
    /// knight's family banes applied. Every knight-owned projectile and blade
    /// routes its damage through here, so an item that says "wolves take more"
    /// holds for arrows, fireballs and their blasts, rebounds and the sword alike.
    ///
    /// Callers apply this BEFORE Killing Blow and Shatter, so an execute still
    /// lands as exactly lethal rather than an inflated number.
    /// </summary>
    public static int ScaleHit(int damage, EnemyBase target, string knightTag)
    {
        if (target == null || damage <= 0) return damage;
        EquipmentBoost boost = ForKnight(knightTag);
        if (boost == null) return damage;
        float multiplier = boost.DamageMultiplierFor(target.Family);
        return multiplier > 1f ? Mathf.CeilToInt(damage * multiplier) : damage;
    }

    // Held as BONUSES and REDUCTIONS, where zero means "no change", rather than
    // as multipliers. A multiplier array would have to be filled with 1s in
    // Awake, and any reader that ran before that — or any path where Awake
    // doesn't fire, as when a component is added outside play mode — would see
    // a multiplier of 0 and silently erase the knight's damage entirely.
    // Zero-initialized fields are already correct this way.
    private readonly float[] _familyDamageBonus =
        new float[System.Enum.GetValues(typeof(EnemyFamily)).Length];

    private float _blastReduction; // 0 = unprotected, 0.75 = three quarters absorbed

    /// <summary>Incoming blast damage is scaled by this. 1 = unprotected.</summary>
    public float BlastDamageMultiplier => 1f - _blastReduction;

    /// <summary>This knight gets an upgrade pick before the first wave.</summary>
    public bool GrantsHeadStart { get; private set; }

    /// <summary>
    /// Outgoing damage multiplier against a kinship group. EnemyFamily.None
    /// always returns 1 — an item that hits "everything" is a flat damage
    /// upgrade, which is the Orders' job, not equipment's.
    /// </summary>
    public float DamageMultiplierFor(EnemyFamily family)
    {
        if (family == EnemyFamily.None) return 1f;
        int i = (int)family;
        if (i < 0 || i >= _familyDamageBonus.Length) return 1f;
        return 1f + _familyDamageBonus[i];
    }

    /// <summary>
    /// <paramref name="multiplier"/> is the full multiplier the item advertises
    /// (1.5 = half again as much). Two items granting the same bane multiply:
    /// 1.5x and 1.5x is 2.25x.
    /// </summary>
    public void AddFamilyDamage(EnemyFamily family, float multiplier)
    {
        if (family == EnemyFamily.None) return;
        int i = (int)family;
        if (i < 0 || i >= _familyDamageBonus.Length) return;
        _familyDamageBonus[i] = (1f + _familyDamageBonus[i]) * Mathf.Max(0f, multiplier) - 1f;
    }

    /// <summary>
    /// <paramref name="damageTaken"/> is the share that still lands (0.25 = you
    /// take a quarter). Two wards multiply the share, so a quarter of a quarter
    /// lands — stacking can approach full protection but never pass it.
    /// </summary>
    public void ReduceBlastDamage(float damageTaken)
    {
        _blastReduction = 1f - (1f - _blastReduction) * Mathf.Clamp01(damageTaken);
    }

    public void GrantHeadStart()
    {
        GrantsHeadStart = true;
    }

    // ---- effects with no home on an Order's own boost ----

    /// <summary>Added to the shadow arrow and shuriken damage multipliers at spawn time.</summary>
    public float ShadowArrowDamageBonus { get; private set; }

    /// <summary>Special charge granted when something dies of this knight's venom.</summary>
    public int PoisonDeathSpecial { get; private set; }

    /// <summary>Scales incoming confusion duration. 1 = unchanged.</summary>
    public float ConfusionDurationMultiplier { get; private set; } = 1f;

    public void AddShadowArrowDamage(float bonus)
    {
        ShadowArrowDamageBonus += Mathf.Max(0f, bonus);
    }

    public void AddPoisonDeathSpecial(int amount)
    {
        PoisonDeathSpecial += Mathf.Max(0, amount);
    }

    public void ShortenConfusion(float multiplier)
    {
        ConfusionDurationMultiplier *= Mathf.Clamp01(multiplier);
    }

    // ---- draft weighting ----

    // How much likelier each Order is to appear in this knight's draft. Bonuses
    // again, so zero is inert.
    private readonly float[] _orderDraftBonus =
        new float[System.Enum.GetValues(typeof(UpgradeOrder)).Length];

    /// <summary>
    /// Multiplier applied to an Order's draft weight for this knight. 1 = normal.
    /// Read by UpgradeManager when it builds a pool.
    /// </summary>
    public float OrderDraftMultiplier(UpgradeOrder order)
    {
        int i = (int)order;
        if (i < 0 || i >= _orderDraftBonus.Length) return 1f;
        return 1f + _orderDraftBonus[i];
    }

    /// <summary>
    /// <paramref name="multiplier"/> is what the item advertises (2 = twice as
    /// common). Two items naming the same Order multiply: 2x and 2x is 4x.
    /// </summary>
    public void MultiplyOrderDraft(UpgradeOrder order, float multiplier)
    {
        int i = (int)order;
        if (i < 0 || i >= _orderDraftBonus.Length) return;
        _orderDraftBonus[i] = (1f + _orderDraftBonus[i]) * Mathf.Max(0f, multiplier) - 1f;
    }

    /// <summary>
    /// Whether this knight is carrying anything at all. Drives the "bare run"
    /// feats — see RunPurity.
    /// </summary>
    public bool CarriesAnything { get; private set; }

    /// <summary>Called by Loadout for each item it applies.</summary>
    public void NoteCarrying()
    {
        CarriesAnything = true;
    }
}
