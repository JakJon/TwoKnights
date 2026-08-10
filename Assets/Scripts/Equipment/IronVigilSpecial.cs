using UnityEngine;

/// <summary>
/// Untouchable for a few seconds. Bought with crystals.
///
/// The window is a deadline on PlayerHealth rather than a coroutine that toggles
/// a flag, so it always expires — a coroutine killed by a scene change or a
/// death would otherwise leave a knight permanently invulnerable.
/// </summary>
[CreateAssetMenu(fileName = "Iron Vigil", menuName = "Equipment/Special/Iron Vigil")]
public class IronVigilSpecial : SpecialDefinition
{
    [SerializeField] private float duration = 12f;
    [SerializeField] private Color glow = new Color(0.72f, 0.78f, 0.85f);

    public override void Activate(GameObject knight, string playerTag)
    {
        var health = knight.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning($"[IronVigilSpecial] {playerTag} has no PlayerHealth.");
            return;
        }

        health.SetInvulnerable(duration);

        // The glow is the whole tell — nothing else on screen says "that hit
        // did nothing", and a special the player can't see working reads as broken
        knight.GetComponent<GlowManager>()?.StartGlow(glow, duration);
    }
}
