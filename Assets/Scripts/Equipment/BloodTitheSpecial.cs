using UnityEngine;

/// <summary>
/// For a window, everything this knight kills gives a little life back.
/// Bought with crystals.
/// </summary>
[CreateAssetMenu(fileName = "Blood Tithe", menuName = "Equipment/Special/Blood Tithe")]
public class BloodTitheSpecial : SpecialDefinition
{
    [SerializeField] private float duration = 8f;
    [SerializeField] private int healPerKill = 3;
    [SerializeField] private Color glow = new Color(0.72f, 0.15f, 0.22f);

    public override void Activate(GameObject knight, string playerTag)
    {
        BloodTitheWindow.Open(knight, playerTag, duration, healPerKill);
        knight.GetComponent<GlowManager>()?.StartGlow(glow, duration);
    }
}
