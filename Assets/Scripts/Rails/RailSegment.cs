using UnityEngine;

// One piece of track. Slams into place over the fall animation, then kicks up
// a dust puff from its bottom edge — the rail sprite pivots at bottom-centre,
// so the puff spawns at the transform with only a small nudge.
[RequireComponent(typeof(SpriteFlipbook))]
public class RailSegment : MonoBehaviour
{
    [SerializeField] private SpriteFlipbook fall;
    [SerializeField] private GameObject dustPrefab;
    [Tooltip("Offset from the piece's pivot (bottom-centre) to where the dust kicks up")]
    [SerializeField] private Vector2 dustOffset = Vector2.zero;
    [Tooltip("Optional — no rail SFX authored yet, so this stays empty for now")]
    [SerializeField] private SoundEffect landSound;

    public bool IsSettled { get; private set; }

    /// <summary>Seconds this piece's fall animation runs once it starts.</summary>
    public float FallDuration => fall != null ? fall.Duration : 0f;

    private void Awake()
    {
        if (fall == null) fall = GetComponent<SpriteFlipbook>();
    }

    private void OnEnable()
    {
        if (fall != null) fall.Completed += HandleLanded;
    }

    private void OnDisable()
    {
        if (fall != null) fall.Completed -= HandleLanded;
    }

    /// <summary>Drop into place after <paramref name="delay"/> seconds.</summary>
    public void Drop(float delay = 0f)
    {
        IsSettled = false;
        if (fall != null) fall.Play(delay);
    }

    /// <summary>Already in place — no fall, no dust (rebuilt mid-wave, editor preview).</summary>
    public void SnapSettled()
    {
        if (fall != null) fall.JumpToLastFrame();
        IsSettled = true;
    }

    private void HandleLanded()
    {
        IsSettled = true;

        if (dustPrefab != null)
        {
            Vector3 at = transform.position + (Vector3)dustOffset;
            Instantiate(dustPrefab, at, Quaternion.identity, transform.parent);
        }

        if (landSound != null && landSound.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(landSound);
        }
    }
}
