using UnityEngine;
using System.Collections;

public class RapidFire : MonoBehaviour
{
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

        StartCoroutine(RapidFireRoutine(shooter));
    }

    private IEnumerator RapidFireRoutine(PlayerShooter shooter)
    {
        shooter.rapidFireEnabled = true;
        shooter.cooldownTime = .1f;
        yield return new WaitForSeconds(6f);
        shooter.rapidFireEnabled = false;
        shooter.cooldownTime = 1.5f;
    }
}
