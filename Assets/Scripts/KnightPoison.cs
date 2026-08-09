using System.Collections;
using UnityEngine;

// Poison on a KNIGHT. The mine's answer to a player who has learned to shoot
// every bomb out of the air: this one does not need to land a big hit, it needs
// to land at all.
//
// The shape of it is a debt rather than a blow. Twenty-five points is more than
// any single thing in the mine deals — a bomb is fifteen, a keg fifteen — but it
// arrives one point at a time over twenty-five seconds, which is most of a wave.
// So it is never what kills you on its own and always what makes the next thing
// lethal, and the knight carrying it is playing the rest of the wave on a clock.
//
// THE WAY OUT IS HEALING, and that is the whole design. A health orb is normally
// a small, dull decision — twenty points you take when there is a spare arrow.
// While poison is running, the same orb is worth whatever is left on the timer as
// well, and it has to be taken NOW. So the poison does not add a new mechanic to
// learn; it re-prices one that was already on the board, which is the cheapest
// kind of pressure a wave can buy.
//
// Any heal cures it outright rather than reducing it — see PlayerHealth.Heal.
// Part-curing would make the player do arithmetic mid-fight to work out whether
// an orb was worth an arrow, and the answer needs to be readable at a glance.
[RequireComponent(typeof(PlayerHealth))]
public class KnightPoison : MonoBehaviour
{
    // Every figure here is a property of the POISON rather than of whatever
    // applied it, so two sources cannot quietly disagree about what "poisoned"
    // means. A gnome's bomb and anything that inherits this later both leave a
    // knight in exactly the same state.
    private const int DamagePerTick = 1;
    private const float TickSeconds = 1f;
    private const float DefaultDuration = 25f;

    // Matches the red pulse every other damage event uses, in the one colour that
    // reads as poison rather than as a hit. Same duration, so a poison tick and an
    // arrow hit are the same length of flash and only the hue tells them apart.
    private static readonly Color PoisonFlash = new Color(0.45f, 1f, 0.35f);
    private const float FlashSeconds = 0.3f;

    private PlayerHealth _health;
    private Coroutine _running;
    private string _sourceName;

    /// <summary>Is this knight currently rotting?</summary>
    public bool IsPoisoned => _running != null;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
    }

    /// <summary>
    /// Poison <paramref name="knight"/>, adding the component if this is the first
    /// time. The entry point every source should use — nothing else needs to know
    /// whether the knight has been poisoned before.
    /// </summary>
    public static void Apply(GameObject knight, string sourceName)
    {
        if (knight == null) return;

        KnightPoison poison = knight.GetComponent<KnightPoison>();
        if (poison == null) poison = knight.AddComponent<KnightPoison>();
        poison.Begin(sourceName, DefaultDuration);
    }

    /// <summary>
    /// Start (or restart) the timer. A second dose REFRESHES rather than stacks:
    /// two bombs landing on the same knight should be a longer problem, not a
    /// faster one, or a pair of unlucky frames turns a slow bleed into a spike
    /// nothing on the board explains.
    /// </summary>
    public void Begin(string sourceName, float seconds)
    {
        _sourceName = sourceName;
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Rot(Mathf.Max(0f, seconds)));
    }

    /// <summary>
    /// Cut it short. Called by every heal path — see the class note: curing
    /// outright is what makes a health orb worth an arrow while this is running.
    /// Safe to call on a knight who was never poisoned.
    /// </summary>
    public void Cure()
    {
        if (_running == null) return;
        StopCoroutine(_running);
        _running = null;
    }

    private IEnumerator Rot(float seconds)
    {
        // The first tick is a second in, not immediate. The bomb that applied this
        // has already dealt its own damage this frame, and a poison tick landing
        // on the same frame reads as the blast having hit twice.
        float remaining = seconds;

        while (remaining > 0f)
        {
            yield return new WaitForSeconds(TickSeconds);
            remaining -= TickSeconds;

            // Dead knights stop rotting: the run is over and the death screen is
            // already up, so another tick would only fire OnPlayerDeath twice.
            if (_health == null || _health.CurrentHealth <= 0) break;

            // Its own voice rather than the standard hurt cry — see AudioManager
            // .knightPoisonTick. Still routed through TakeDamage so the streak
            // reset and the death path cannot drift away from every other hit.
            SoundEffect bubble = AudioManager.Instance != null
                ? AudioManager.Instance.knightPoisonTick
                : null;
            _health.TakeDamage(DamagePerTick, _sourceName, PoisonFlash, FlashSeconds, bubble);
        }

        _running = null;
    }
}
