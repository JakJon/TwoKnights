/// <summary>
/// Stat keys for the Mine's own business, kept next to the detectors that write
/// them so a quest and its counter cannot drift apart.
///
/// Carts are counted three ways because the quests ask three different questions.
/// "Break a hundred carts" does not care what was in them; "break ten empty ones"
/// is about clearing the track; "set off twenty powder carts" is about using them
/// as a weapon. One tally could not have answered all three.
/// </summary>
public static class MineStats
{
    /// <summary>Every cart destroyed, whatever it was carrying. Every cart variant
    /// reports EnemyFamily.Cart, so the family counter already answers this exactly
    /// and a second tally would only be a way for the two to disagree.</summary>
    public const string CartsBroken = "kills.family.cart";

    /// <summary>Carts carrying nothing — the ones that are purely in the way.</summary>
    public const string EmptyCartsBroken = "carts.broken.empty";

    /// <summary>Powder carts set off. Counted on the detonation, not on the kills it scored.</summary>
    public const string PowderCartsDetonated = "carts.detonated.powder";

    /// <summary>The Mine's gate boss, per kill. Read by Outdone Overseer's gate.</summary>
    public const string OverseerKills = "kills.overseer";
}
