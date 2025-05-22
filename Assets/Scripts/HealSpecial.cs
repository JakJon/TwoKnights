using UnityEngine;

public class HealSpecial : MonoBehaviour
{
    /// <summary>
    /// Heals the invoking player for 40 and the other player for 20.
    /// </summary>
    /// <param name="playerTag">"PlayerLeft" or "PlayerRight"</param>
    public void ActivateHeal(string playerTag)
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.healSpecial);

        // Heal invoking player
        PlayerHealth selfHealth = GetComponent<PlayerHealth>();
        if (selfHealth != null)
        {
            selfHealth.Heal(40);
            GetComponent<GlowManager>()?.StartGlow(Color.magenta, 2);
        }

        // Heal other player
        string otherTag = playerTag == "PlayerLeft" ? "PlayerRight" : "PlayerLeft";
        GameObject otherPlayer = GameObject.FindGameObjectWithTag(otherTag);
        if (otherPlayer != null)
        {
            PlayerHealth otherHealth = otherPlayer.GetComponent<PlayerHealth>();
            if (otherHealth != null)
            {
                otherHealth.Heal(20);
                otherPlayer.GetComponent<GlowManager>()?.StartGlow(Color.magenta, 2);
            }
        }
    }
}
