using UnityEngine;

/// <summary>
/// Runtime half of Blood Tithe: listens for kills credited to this knight and
/// heals while the window is open.
///
/// Subscribes in OnEnable/OnDisable and gates on a deadline rather than starting
/// and stopping with the effect — the same pattern NinjaBoost uses for Thousand
/// Cuts. A component that added and removed itself would risk unsubscribing
/// during the very event it is handling.
/// </summary>
public class BloodTitheWindow : MonoBehaviour
{
    private string _ownerTag;
    private float _endsAt = -1f;
    private int _healPerKill;

    public static void Open(GameObject knight, string playerTag, float duration, int healPerKill)
    {
        if (knight == null) return;
        var window = knight.GetComponent<BloodTitheWindow>();
        if (window == null) window = knight.AddComponent<BloodTitheWindow>();
        window._ownerTag = playerTag;
        window._healPerKill = healPerKill;
        // Extend rather than restart, so firing twice can't shorten the window
        window._endsAt = Mathf.Max(window._endsAt, Time.time + duration);
    }

    private void OnEnable()
    {
        EnemyBase.OnEnemyKilledBy += HandleKill;
    }

    private void OnDisable()
    {
        EnemyBase.OnEnemyKilledBy -= HandleKill;
    }

    private void HandleKill(string playerTag)
    {
        if (Time.time >= _endsAt) return;
        if (playerTag != _ownerTag) return;

        // Heal already clamps to max health and cures knight poison
        GetComponent<PlayerHealth>()?.Heal(_healPerKill);
    }
}
