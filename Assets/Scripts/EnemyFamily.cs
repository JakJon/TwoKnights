// Broad kinship groups for enemies, for effects that care about "vermin" or
// "beasts" rather than one exact class.
//
// This is a code virtual rather than a serialized field on EnemyBase on purpose.
// The serialized route (EnemyAttributes.EnemyType) would mean editing every
// enemy prefab to tag it, and a prefab that got missed would silently drop out
// of the group with nothing to catch it. An override on the class cannot drift
// from the class it describes.
//
// It also fixes a trap in the type hierarchy: EnemyDarkBat is a SIBLING of
// EnemyBat, not a subclass, and EnemyRatKing is unrelated to EnemyRat — so
// `enemy is EnemyBat` silently misses half of what a player would call a bat.
public enum EnemyFamily
{
    None = 0,
    Vermin = 1,   // rats, bats, dark bats, the Rat King
    Beast = 2,    // wolves
    Ooze = 3,     // slimes, giant slimes
    Cart = 4      // mine carts and everything riding one
}
