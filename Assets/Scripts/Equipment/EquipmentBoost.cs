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
/// </summary>
public class EquipmentBoost : MonoBehaviour
{
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
    /// (1.5 = half again as much). Two items granting the same bane settle on
    /// the better one rather than whichever applied last.
    /// </summary>
    public void AddFamilyDamage(EnemyFamily family, float multiplier)
    {
        if (family == EnemyFamily.None) return;
        int i = (int)family;
        if (i < 0 || i >= _familyDamageBonus.Length) return;
        _familyDamageBonus[i] = Mathf.Max(_familyDamageBonus[i], multiplier - 1f);
    }

    /// <summary>
    /// <paramref name="damageTaken"/> is the share that still lands (0.25 = you
    /// take a quarter). Keeps the strongest protection offered.
    /// </summary>
    public void ReduceBlastDamage(float damageTaken)
    {
        _blastReduction = Mathf.Max(_blastReduction, 1f - Mathf.Clamp01(damageTaken));
    }

    public void GrantHeadStart()
    {
        GrantsHeadStart = true;
    }

    // ---- effects with no home on an Order's own boost ----

    /// <summary>Added to the shadow arrow damage multiplier at spawn time.</summary>
    public float ShadowArrowDamageBonus { get; private set; }

    /// <summary>Special charge granted when something dies of this knight's venom.</summary>
    public int PoisonDeathSpecial { get; private set; }

    /// <summary>Scales incoming confusion duration. 1 = unchanged.</summary>
    public float ConfusionDurationMultiplier { get; private set; } = 1f;

    public void AddShadowArrowDamage(float bonus)
    {
        ShadowArrowDamageBonus = Mathf.Max(ShadowArrowDamageBonus, bonus);
    }

    public void AddPoisonDeathSpecial(int amount)
    {
        PoisonDeathSpecial = Mathf.Max(PoisonDeathSpecial, amount);
    }

    public void ShortenConfusion(float multiplier)
    {
        ConfusionDurationMultiplier = Mathf.Min(ConfusionDurationMultiplier, Mathf.Clamp01(multiplier));
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
    /// common). Keeps the strongest offered rather than stacking, so two items
    /// naming the same Order don't quietly multiply out.
    /// </summary>
    public void MultiplyOrderDraft(UpgradeOrder order, float multiplier)
    {
        int i = (int)order;
        if (i < 0 || i >= _orderDraftBonus.Length) return;
        _orderDraftBonus[i] = Mathf.Max(_orderDraftBonus[i], multiplier - 1f);
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
