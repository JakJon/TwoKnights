// How a knight got hurt.
//
// PlayerHealth's funnel carried only a display name, which is all the death
// screen needs but not enough for equipment that answers one specific kind of
// harm ("blasts hurt you less"). The alternative — intercepting at each damage
// source — puts the same rule in several places and silently misses the next
// source someone adds.
//
// Deliberately coarse: these are categories a player could name out loud, not a
// taxonomy of every source. Add a case only when something needs to tell it
// apart from the others.
public enum DamageKind
{
    Generic = 0,
    Contact = 1,     // something walked into you
    Projectile = 2,  // arrows, pickaxes, enemy fireballs
    Blast = 3        // powder kegs, bombs
}
