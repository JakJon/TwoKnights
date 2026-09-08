using UnityEngine;

// Sleeping Dart (Shadow + Serpent): every Nth arrow leaves the shield as a dart
// that puts what it hits under. A COUNTER, not a roll - the player can count to
// ten and put the dart into the thing that most needs to stop, which is the only
// reason a five second hold is worth a shot at all.
//
// The counter is only advanced by shots that could actually BE a dart: on a
// frame where Ember has already claimed the arrow for a fireball, the tally
// holds where it is and the dart arrives on the next shot instead. A promise
// about "every tenth shot" that silently ate one to a fireball would be worse
// than no promise.
public class SleepBoost : MonoBehaviour
{
    private int _shotsPerDart = 10;
    private float _sleepSeconds = 5f;
    private int _shotCounter;
    private Sprite _dartSprite;

    public int ShotsPerDart => _shotsPerDart;
    public float SleepSeconds => _sleepSeconds;
    public Sprite DartSprite => _dartSprite;

    /// <summary>
    /// Later ranks fire oftener and hold longer. Both are set outright rather
    /// than multiplied so the upgrade asset states the real numbers.
    /// </summary>
    public void SetDart(int shotsPerDart, float sleepSeconds, Sprite dartSprite)
    {
        _shotsPerDart = Mathf.Max(1, shotsPerDart);
        _sleepSeconds = Mathf.Max(0.1f, sleepSeconds);
        if (dartSprite != null) _dartSprite = dartSprite;
        // Counting restarts on the upgrade so the first dart after taking rank
        // two is a full cycle away, not whatever the old tally happened to be
        _shotCounter = 0;
    }

    /// <summary>
    /// Call once per ordinary arrow. True on every Nth, and only then is the
    /// tally spent.
    /// </summary>
    public bool AdvanceShotAndCheckDart()
    {
        _shotCounter++;
        if (_shotCounter < _shotsPerDart) return false;
        _shotCounter = 0;
        return true;
    }

    /// <summary>Whether the NEXT arrow is the dart, for tells that want to say so.</summary>
    public bool NextShotIsDart => _shotCounter + 1 >= _shotsPerDart;
}
