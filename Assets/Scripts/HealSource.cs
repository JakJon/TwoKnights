// Where a heal came from.
//
// The mirror of DamageKind, and it exists for one reason: an orb already has a
// sound of its own (orbCollect), so the Dawn blessing chime has to be held back
// a beat rather than fired underneath it. Everything else that heals a knight —
// Lifebloom, Shared Light, Benediction, Second Wind, Last Light — chimes
// immediately, because nothing else is competing for that instant.
//
// Deliberately coarse, same rule as DamageKind: add a case only when something
// actually needs to tell itself apart.
public enum HealSource
{
    Generic = 0,
    Orb = 1  // already announced by orbCollect; the blessing follows it
}
