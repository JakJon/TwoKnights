using UnityEngine;

// Serpent discipline: the THROWING door.
//
// Every other way this Order puts venom on the board goes through a shot — an
// arrow that rolled poison, a trail shed by one, a cloud left where something
// poisoned died. All of it starts with the knight pointing at something. This
// does not. A vial is the knight's other hand, and it is deliberately the one
// Serpent upgrade that needs no prior poison rank to appear: it is a door INTO
// the Order rather than a reward for already being in it.
//
// What it is for, in one line: shield facing is also shooting direction, so the
// ground a knight cannot cover is wherever he is not looking — and the vials go
// there. Rank II throws one straight out the back, which is the exact square the
// bow can never answer.
//
// The ranks, and why they are shaped this way:
//   I   (common)    one vial, two units along the aim, every fifth shot
//   II  (common)    a second vial, two units straight behind
//   III (legendary) a third, four units along the aim, and every FOURTH shot
//
// Rank III is legendary and the other two are common on purpose. I and II are
// utility a lot of builds are happy to take; III turns the pair into a corridor
// of venom down the knight's own sightline AND shortens the cadence, which is the
// point at which the upgrade stops being a garnish on a build and becomes one.
[CreateAssetMenu(fileName = "VialThrowUpgrade", menuName = "Upgrades/Vial Throw")]
public class VialThrowUpgrade : BaseUpgrade
{
    [Tooltip("Which rank this asset is: 1, 2 or 3. The boost derives the whole throw from it (see PoisonVialBoost.SetTier), so a rank states one number rather than a list of vials that can drift out of step with its own description.")]
    [SerializeField] private int tier = 1;

    [Tooltip("Shots between throws. Five on ranks I and II, four on III — the cadence is part of what the last rank buys.")]
    [SerializeField] private int shotsPerVial = 5;

    [Tooltip("The vial's art. Only the sprite is read — a vial builds itself (see PoisonVial), because a prefab here would exist to carry a sprite and a collider it never uses.")]
    [SerializeField] private Sprite vialSprite;

    public override string ChainName => "Vial Throw";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Vial Throw";
        if (weight == 0f)
            weight = 100f; // Common
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonVialBoost boost = targetKnight.GetComponent<PoisonVialBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonVialBoost>();
        }

        boost.SetTier(tier, shotsPerVial, null);

        // The sprite rides on the boost's own resource lookup rather than being
        // pushed through it, so a knight who took rank I from one asset and rank
        // III from another cannot end up with two different vials
        PoisonVialSprite.Register(vialSprite);

        Debug.Log($"Applied Vial Throw {tier} to {targetKnight.name}: " +
                  $"{boost.Throws.Count} vial(s) every {boost.ShotsPerVial} shots.");
    }
}

/// <summary>
/// Where the vial's art lives at runtime. A static rather than a field on the
/// boost because both knights throw the same vial and the upgrade assets are the
/// only things that know what it looks like — this is the one place the two meet,
/// and it means PoisonVial never has to be handed a sprite it could be handed
/// two different versions of.
/// </summary>
public static class PoisonVialSprite
{
    private static Sprite _sprite;

    public static void Register(Sprite sprite)
    {
        if (sprite != null) _sprite = sprite;
    }

    /// <summary>
    /// The authored sprite, falling back to the poison bubble so a vial still
    /// appears if an asset was never given one. A silent invisible throw is far
    /// worse than the wrong picture: the cadence is a promise, and a promise the
    /// player cannot see kept is a bug report.
    /// </summary>
    public static Sprite Current
    {
        get
        {
            if (_sprite != null) return _sprite;
            return PoisonResourceManager.Instance != null
                ? PoisonResourceManager.Instance.GetPoisonBubbleSprite()
                : null;
        }
    }
}
