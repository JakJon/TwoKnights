using System.Collections.Generic;
using UnityEngine;

// Vial Throw (Serpent): the knight's stat sheet for the vials, and the counter
// that decides which shot they leave on.
//
// A COUNTER, not a roll, for the same reason Ember's Fireball and the Sleeping
// Dart are counters: the whole value of a vial is choosing where the cloud lands,
// and you cannot choose anything about a coin flip. Five shots is long enough for
// the choice to be worth planning and short enough to plan it — and the shield
// steams on the shot before (see PoisonFx), so the player reads the promise
// rather than counting arrows.
//
// The counter is only advanced by shots that COULD be a vial, and the vials go
// out beside the arrow rather than instead of it. A vial is thrown, not fired: it
// is the knight's other hand, so it never costs the shot it rides on.
public class PoisonVialBoost : MonoBehaviour
{
    /// <summary>Where one vial of a throw goes, relative to the shield's facing.</summary>
    public struct Throw
    {
        public bool alongAim;  // false = straight behind, the way the knight is NOT looking
        public float distance; // world units from the knight before it breaks
    }

    private int _shotsPerVial;   // 0 = the knight has no Vial Throw yet
    private int _tier;
    private GameObject _vialPrefab;
    private int _shotCounter;

    private readonly List<Throw> _throws = new List<Throw>();

    public bool HasVials => _shotsPerVial > 0 && _throws.Count > 0;
    public int ShotsPerVial => _shotsPerVial;
    public GameObject VialPrefab => _vialPrefab;
    public IReadOnlyList<Throw> Throws => _throws;

    /// <summary>
    /// Each rank states the whole throw outright rather than adding to it, so an
    /// asset says what the upgrade DOES and a rank re-offered by the draft cannot
    /// stack a fourth vial nobody authored. Ranks only ever move forward: a lower
    /// tier arriving after a higher one is ignored.
    /// </summary>
    public void SetTier(int tier, int shotsPerVial, GameObject vialPrefab)
    {
        if (tier <= _tier) return;

        _tier = tier;
        _shotsPerVial = Mathf.Max(1, shotsPerVial);
        if (vialPrefab != null) _vialPrefab = vialPrefab;

        _throws.Clear();

        // I — one over the shield, two units out. Short on purpose: two units is
        // inside the shield's own orbit-and-a-bit, so the cloud lands in the lane
        // the knight is already defending rather than somewhere across the board.
        _throws.Add(new Throw { alongAim = true, distance = 2f });

        // II — a second one straight out the back. This is the rank that changes
        // how the upgrade is played: the knight's facing is his shooting direction,
        // so the ONE piece of ground he can never cover is directly behind the
        // shield, and rank II is what puts venom there. It arrives without being
        // aimed at, which is the point.
        if (_tier >= 2) _throws.Add(new Throw { alongAim = false, distance = 2f });

        // III — a long one down the aim, and the cadence tightens to four. Two
        // clouds down the same lane at two and four units is a corridor rather
        // than a puddle, and it is laid along the exact line the knight is looking.
        if (_tier >= 3) _throws.Add(new Throw { alongAim = true, distance = 4f });
    }

    /// <summary>
    /// Call once per shot. True on every Nth, and only then is the tally spent.
    /// </summary>
    public bool AdvanceShotAndCheckVial()
    {
        if (!HasVials) return false;

        _shotCounter++;
        if (_shotCounter < _shotsPerVial) return false;
        _shotCounter = 0;
        return true;
    }

    /// <summary>Whether the NEXT shot throws, for the tell that says so.</summary>
    public bool NextShotIsVial => HasVials && _shotCounter + 1 >= _shotsPerVial;
}
