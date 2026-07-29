using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpecial : MonoBehaviour
{
    [SerializeField] private int maxSpecial = 1000;
    [SerializeField] private SpecialBar specialBar;
    [SerializeField] private InputActionReference specialAction;
    private RapidFire rapidFire;
    private HealSpecial healSpecial;
    private int _currentSpecial;
    private int _currentSpecialStreak;
    private int _currentSpecialMultiplier = 1;

    public bool streakEnded;
    public bool specialBarFilled => _currentSpecial >= maxSpecial;

    // Read-only accessors for UI/status
    public int CurrentSpecial => _currentSpecial;
    public int MaxSpecial => maxSpecial;

    // Add this field to track if SFX has been played for the current streak
    private bool _specialBarFilledSfxPlayed = false;

    // Hit-based special gain is frozen while the player's own special is running.
    // Rapid Fire lands so many arrows in its 6s that the bar would refill before
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

        rapidFire = GetComponent<RapidFire>();
        healSpecial = GetComponent<HealSpecial>();
        _specialBarFilledSfxPlayed = false;
    }

    void Update()
    {
        SpecialTriggerCheck();
    }

    private void SpecialTriggerCheck()
    {
        if (!specialBarFilled || specialAction == null)
            return;

        if (specialAction.action.triggered)
        {
            if (gameObject.CompareTag("PlayerLeft"))
            {
                _currentSpecial = 0;
                updateSpecial(0);
                if (rapidFire != null)
                    rapidFire.ActivateRapidFire("PlayerLeft");
            }
            else if (gameObject.CompareTag("PlayerRight"))
            {
                _currentSpecial = 0;
                updateSpecial(0);
                if (healSpecial != null)
                    healSpecial.ActivateHeal("PlayerRight");
            }
        }
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
            if (previousSpecial < maxSpecial)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.multiFull);
            }
            
            _currentSpecial = maxSpecial;

            if (!_specialBarFilledSfxPlayed && !streakEnded)
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