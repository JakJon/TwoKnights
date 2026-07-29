using UnityEngine;
using System.Collections;

public class RapidFire : MonoBehaviour
{
    private const float Duration = 6f;

    /// <summary>
    /// Activates rapid fire for the specified player ("PlayerLeft" or "PlayerRight") for 6 seconds.
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

        // Freeze this knight's special gain for the duration: at ~0.16s per shot
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
        shooter.OpenNoCooldownWindow(Duration, 0.16f);
        yield return new WaitForSeconds(Duration);
        shooter.rapidFireEnabled = false;
    }
}
