using UnityEngine;

/// <summary>
/// A knight's special: the thing that fires when the bar is full.
///
/// These used to be hardcoded by tag inside PlayerSpecial — left knight got
/// Rapid Fire, right knight got the heal, permanently. Making them data lets a
/// knight carry a bought one instead, and lets the equipment screen show what
/// each knight is holding without knowing anything about the effects.
/// </summary>
public abstract class SpecialDefinition : ScriptableObject
{
    [Tooltip("Stable save key. Never change it once a build has shipped.")]
    [SerializeField] protected string id;
    [SerializeField] protected string displayName;
    [Tooltip("Flavor. One sentence, second person, NO numbers — house style.")]
    [TextArea(2, 4)]
    [SerializeField] protected string description;
    [Tooltip("Exactly what it does, with the numbers. This is the line a player buys on.")]
    [SerializeField] protected string effect;
    [SerializeField] protected Sprite icon;
    [Tooltip("Crystals to buy in the camp shop. 0 with ownedFromTheStart means a knight's stock special.")]
    [SerializeField] protected int crystalCost = 0;
    [Tooltip("The two originals are owned from a new game; bought specials are not.")]
    [SerializeField] protected bool ownedFromTheStart = false;
    [Tooltip("Seconds to block special GAIN after firing, so the effect's own hits don't refill the bar mid-effect. Leave 0 when the effect freezes the bar itself.")]
    [SerializeField] protected float freezeGainSeconds = 0f;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    /// <summary>The mechanical line, with numbers. Never leave this empty on a shop item.</summary>
    public string Effect => effect;
    public Sprite Icon => icon;
    public int CrystalCost => crystalCost;
    public bool SoldInShop => crystalCost > 0;
    public bool OwnedFromTheStart => ownedFromTheStart;
    public float FreezeGainSeconds => freezeGainSeconds;

    /// <summary>
    /// Fired with the bar already spent. <paramref name="playerTag"/> is
    /// "PlayerLeft" or "PlayerRight".
    /// </summary>
    public abstract void Activate(GameObject knight, string playerTag);
}
