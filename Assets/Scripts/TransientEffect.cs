using UnityEngine;

// Tag component to mark ephemeral visual/effect objects that should not persist when duplicating enemies
[DisallowMultipleComponent]
public class TransientEffect : MonoBehaviour
{
    // Intentionally empty — acts as a marker
}
