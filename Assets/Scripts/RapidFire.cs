using UnityEngine;
using System.Collections;

public class RapidFire : MonoBehaviour
{
    // Short and fast rather than long and fast: the window is about one approach
    // of a pack, so the special is spent ON something instead of being dumped the
    // moment the bar fills. The cadence below is half what it was for the same
    // reason — the old 0.16s wall of arrows made the window trivial.
    private const float Duration = 5f;

    // Seconds between shots while the window is open.
    private const float FireInterval = 0.32f;

    /// <summary>
    /// Activates rapid fire for the specified player ("PlayerLeft" or "PlayerRight")
    /// for <see cref="Duration"/> seconds.
    /// </summary>
    /// <param name="playerTag">The tag of the player ("PlayerLeft" or "PlayerRight").</param>
    public void ActivateRapidFire(string playerTag)
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"No player found with tag {playerTag}");
            return;
        }

        PlayerShooter shooter = player.GetComponent<PlayerShooter>();
        if (shooter == null)
        {
            Debug.LogWarning($"No PlayerShooter component found on {playerTag}");
            return;
        }

        // Freeze this knight's special gain for the duration: even at this cadence
        // the hits alone would refill the bar before rapid fire ran out.
        player.GetComponent<PlayerSpecial>()?.FreezeSpecialGain(Duration);

        StartCoroutine(RapidFireRoutine(shooter));
    }

    private IEnumerator RapidFireRoutine(PlayerShooter shooter)
    {
        // Auto-fire + a no-cooldown window. Never write cooldownTime here: the
        // old hardcoded restore (1.5) silently erased Reload upgrades after
        // every special.
        shooter.rapidFireEnabled = true;
        shooter.OpenNoCooldownWindow(Duration, FireInterval);
        yield return new WaitForSeconds(Duration);
        shooter.rapidFireEnabled = false;
    }
}
