using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpecial : MonoBehaviour
{
    [SerializeField] private int maxSpecial = 1000;
    [SerializeField] private SpecialBar specialBar;
    [SerializeField] private InputActionReference specialAction;
    [Tooltip("This knight's stock special. Left with none, it is inferred from whichever special component the prefab carries.")]
    [SerializeField] private SpecialDefinition defaultSpecial;

    // What this knight will actually fire, resolved once at Start: the specials
    // chosen in the camp if they own them, otherwise the prefab's stock. A
    // knight with two slots fires BOTH on one full bar — the bar is spent once
    // either way, which is what makes the second slot worth having.
    private List<SpecialDefinition> _specials = new List<SpecialDefinition>();

    private int _currentSpecial;
    private int _currentSpecialStreak;
    private int _currentSpecialMultiplier = 1;

    public bool streakEnded;
    public bool specialBarFilled => _currentSpecial >= maxSpecial;

    /// <summary>
    /// Whether a full bar can actually be spent. The bar still fills while this is
    /// down — the tutorial wants the player watching it fill before it tells them
    /// what the button is for. See PlayerShooter.InputEnabled.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    // Read-only accessors for UI/status
    public int CurrentSpecial => _currentSpecial;
    public int MaxSpecial => maxSpecial;

    // Add this field to track if SFX has been played for the current streak
    private bool _specialBarFilledSfxPlayed = false;

    // Hit-based special gain is frozen while the player's own special is running.
    // Rapid Fire lands so many arrows in its window that the bar would refill before
    // the special even ended. Deadline-based (not a flag) so it always expires,
    // even if the effect's coroutine is killed early.
    private float _gainFrozenUntil = -1f;
    public bool SpecialGainFrozen => Time.time < _gainFrozenUntil;

    /// <summary>
    /// Blocks special gain from hits for <paramref name="duration"/> seconds.
    /// Overlapping calls extend the freeze rather than shortening it.
    /// </summary>
    public void FreezeSpecialGain(float duration)
    {
        _gainFrozenUntil = Mathf.Max(_gainFrozenUntil, Time.time + duration);
    }

    void Start()
    {
        streakEnded = false;
        _currentSpecial = 000;
        specialBar.Initialize(maxSpecial);
        specialBar.SetValue(_currentSpecial);

        _specials = Loadout.ResolveSpecials(Loadout.KnightIdFromTag(tag), ResolveStockSpecial());
        // Nothing to fire means nothing to show: the bar keeps filling, it just
        // isn't drawn, and the "special ready" stings stay quiet with it
        specialBar.SetVisible(_specials.Count > 0);
        _specialBarFilledSfxPlayed = false;
    }

    /// <summary>
    /// The prefab's own special. Falls back to sniffing which component the
    /// knight carries so the two original knights keep working with no prefab
    /// wiring at all — the serialized field is the tidy path, not a requirement.
    /// </summary>
    private SpecialDefinition ResolveStockSpecial()
    {
        if (defaultSpecial != null) return defaultSpecial;

        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) return null;
        if (GetComponent<RapidFire>() != null) return catalog.FindSpecial("rapid_fire");
        if (GetComponent<HealSpecial>() != null) return catalog.FindSpecial("field_healing");
        return null;
    }

    // Whether time was stopped on the previous frame. See SpecialTriggerCheck.
    private bool _pausedLastFrame;

    void Update()
    {
        SpecialTriggerCheck();
    }

    private void SpecialTriggerCheck()
    {
        // The specials sit on the d-pad (left knight) and the face buttons (right
        // knight), which are also what every menu and dialogue box is driven with.
        // A press made while time is stopped belongs to that menu, never to the
        // knight. The frame AFTER a pause is refused too: input callbacks run
        // before Update, so the press that closes a menu has already put time back
        // to normal by the time this reads it, and would otherwise spend the bar.
        bool paused = Time.timeScale <= 0f;
        bool menuPress = paused || _pausedLastFrame;
        _pausedLastFrame = paused;
        if (menuPress) return;

        if (!InputEnabled || !specialBarFilled || specialAction == null)
            return;

        if (!specialAction.action.triggered) return;
        // Carrying no special is a legitimate loadout — the bar fills and there
        // is simply nothing to spend it on. Silent, not a warning.
        if (_specials.Count == 0) return;

        // Spend and redraw the bar BEFORE anything freezes gain — updateSpecial
        // returns early while frozen, so freezing first would leave the bar
        // drawn full over an empty meter
        _currentSpecial = 0;
        updateSpecial(0);

        // Every freeze is applied before the FIRST activation, so a long special
        // firing alongside a short one can't have its window cut by the short
        // one's effect refilling the bar. FreezeSpecialGain already takes the
        // later deadline, so the longest wins on its own.
        for (int i = 0; i < _specials.Count; i++)
        {
            if (_specials[i].FreezeGainSeconds > 0f) FreezeSpecialGain(_specials[i].FreezeGainSeconds);
        }
        for (int i = 0; i < _specials.Count; i++)
        {
            _specials[i].Activate(gameObject, tag);
        }

        // Benediction (Dawn): spending a special lifts the OTHER knight too,
        // whatever the special was. Hooked here rather than inside each special
        // so nothing added later can quietly escape the blessing.
        GetComponent<DawnBoost>()?.PayBenediction(tag);
    }

    public void ResetSpecialStreak()
    {
        _currentSpecialStreak = 0;
        streakEnded = false;
        _currentSpecialMultiplier = 1;
        specialBar.SetStreak(_currentSpecialMultiplier, _currentSpecialStreak);
        _specialBarFilledSfxPlayed = false;
    }

    public void updateSpecial(int amountToGain)
    {
        // Special is active: the bar and the streak both hold where they are.
        // Nothing is lost, it just doesn't build until the special is over.
        if (SpecialGainFrozen)
            return;

        if (!streakEnded)
        {
            _currentSpecialStreak += amountToGain * _currentSpecialMultiplier;

            if (_currentSpecialStreak >= 55 && _currentSpecialStreak < 233)
            {
                if (_currentSpecialMultiplier != 2)
                {
                    if (gameObject.CompareTag("PlayerLeft"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.leftMulti2);
                    else if (gameObject.CompareTag("PlayerRight"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.rightMulti2);
                }
                _currentSpecialMultiplier = 2;
            }
            else if (_currentSpecialStreak >= 233 && _currentSpecialStreak < 610)
            {
                if (_currentSpecialMultiplier != 3)
                {
                    if (gameObject.CompareTag("PlayerLeft"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.leftMulti3);
                    else if (gameObject.CompareTag("PlayerRight"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.rightMulti3);
                }
                _currentSpecialMultiplier = 3;
            }
            else if (_currentSpecialStreak >= 610)
            {
                if (_currentSpecialMultiplier != 4)
                {
                    if (gameObject.CompareTag("PlayerLeft"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.leftMulti4);
                    else if (gameObject.CompareTag("PlayerRight"))
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.rightMulti4);
                }
                _currentSpecialMultiplier = 4;
            }
            else
            {
                _currentSpecialMultiplier = 1;
            }

            specialBar.SetStreak(_currentSpecialMultiplier, _currentSpecialStreak);
        }

        int previousSpecial = _currentSpecial;
        _currentSpecial += amountToGain * _currentSpecialMultiplier;

        if (_currentSpecial >= maxSpecial)
        {
            // Only play multiFull sound if bar wasn't already filled
            if (previousSpecial < maxSpecial && _specials.Count > 0)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.multiFull);
            }
            
            _currentSpecial = maxSpecial;

            if (!_specialBarFilledSfxPlayed && !streakEnded && _specials.Count > 0)
            {
                if (gameObject.CompareTag("PlayerLeft"))
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.leftSpecial);
                else if (gameObject.CompareTag("PlayerRight"))
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.rightSpecial);

                _specialBarFilledSfxPlayed = true;
            }
        }

        specialBar.SetValue(_currentSpecial);
    }

    public void AddSpecialFromOrb(int amount)
    {
        _currentSpecial += amount;
        if (_currentSpecial > maxSpecial)
            _currentSpecial = maxSpecial;
        specialBar.SetValue(_currentSpecial);
    }
}