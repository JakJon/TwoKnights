// Something on an enemy that owns its own POSITION, and therefore has to be told
// to stop when that enemy is put to sleep.
//
// EnemyBase can pin a body it moves itself (see the freeze in LateUpdate), but it
// cannot pin one that is being carried: a mine cart keeps rolling however still
// its rider is sitting, and its distance along the rail goes on being counted, so
// a cart pinned by force would simply teleport forward the moment it woke up. The
// vehicle knows how to stop properly; this is how it gets asked.
//
// An interface rather than a subclass override so a new kind of vehicle states it
// once, on itself, and every enemy that ever rides one sleeps correctly without
// anybody remembering to wire it up.
public interface ISleepHold
{
    /// <summary>Stop where you are for this many seconds. Overlapping holds take
    /// the longest, never the latest.</summary>
    void HoldFor(float seconds);
}
