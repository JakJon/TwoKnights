// Something that is not an enemy but still moves, and so can still be slowed by
// the Frigid Order's cold: a health orb drifting across the field, a rock or a
// pickaxe in the air, a boss's fireball.
//
// An interface rather than a pile of type checks at the call sites, for the same
// reason ISleepHold is one: the ward sweeps a circle and asks whatever it finds,
// so a new kind of moving thing states its own answer once, on itself, instead of
// being remembered in a switch somewhere else. That switch is exactly how the
// sleeping dart came to do nothing to a boss.
//
// Note what is NOT here: freezing. Nothing that implements this can be frozen -
// there is no behaviour to switch off and nothing to hold still, and pillar 2 of
// the Order says only a blow the knight lands can stop anything anyway.
public interface IChillable
{
    /// <summary>Move at <paramref name="speedMultiplier"/> of your own speed for
    /// this many seconds. Overlapping chills take the DEEPER multiplier and the
    /// LATER deadline, never the most recent of either.</summary>
    void ApplyChill(float speedMultiplier, float seconds);
}
