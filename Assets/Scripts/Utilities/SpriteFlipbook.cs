using System;
using System.Collections;
using UnityEngine;

// Frame-steps a SpriteRenderer through a sprite array. Used instead of an
// Animator wherever the exact end-of-animation moment has to drive logic (a
// rail landing spawns its dust, a dust puff destroys itself) — an Animator
// state would need a behaviour script and a controller asset per clip to
// report the same thing.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [Tooltip("Seconds per frame. The rail fall is 6 frames at 0.1 = 600ms.")]
    [SerializeField] private float frameSeconds = 0.1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop;
    [SerializeField] private bool destroyOnComplete;
    [Tooltip("Hidden until the animation actually starts — lets a staggered cascade instantiate everything up front")]
    [SerializeField] private bool hideDuringStartDelay = true;

    private SpriteRenderer _renderer;
    private Coroutine _playing;

    // Fires on the frame the last frame's hold expires (never for loop = true)
    public event Action Completed;

    public int FrameCount => frames != null ? frames.Length : 0;
    public float Duration => FrameCount * frameSeconds;
    public bool IsPlaying => _playing != null;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        _playing = null;
    }

    public void Play(float startDelay = 0f)
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (FrameCount == 0) return;
        if (_playing != null) StopCoroutine(_playing);
        _playing = StartCoroutine(PlayRoutine(startDelay));
    }

    // Skip straight to the settled look — a layout can be built mid-wave
    // without replaying every fall
    public void JumpToLastFrame()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (FrameCount == 0) return;
        if (_playing != null) StopCoroutine(_playing);
        _playing = null;
        _renderer.sprite = frames[FrameCount - 1];
        _renderer.enabled = true;
    }

    private IEnumerator PlayRoutine(float startDelay)
    {
        if (startDelay > 0f)
        {
            if (hideDuringStartDelay) _renderer.enabled = false;
            yield return new WaitForSeconds(startDelay);
        }

        _renderer.enabled = true;

        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                _renderer.sprite = frames[i];
                yield return new WaitForSeconds(frameSeconds);
            }
        } while (loop);

        _playing = null;
        Completed?.Invoke();

        if (destroyOnComplete) Destroy(gameObject);
    }

#if UNITY_EDITOR
    // Lets the prefab builder fill frames without a SerializedObject round-trip
    public void EditorSetFrames(Sprite[] value) { frames = value; }
#endif
}
