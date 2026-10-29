using System.Collections.Generic;
using UnityEngine;

// The end of a knight's shot that ran out of flight (owner, 2026-10-27: arrows
// "just instantly disappear when despawning"). It keeps flying and fades out over
// Seconds, then goes. Only the timed end: a shot that hits something — a body, a
// pillar, a guard — still stops dead on the spot, because that is the hit.
//
// While it fades it is already spent: its colliders are switched off, so it
// cannot land a hit, be carried by a mirror or be caught by anything else, and
// its trails stop putting out new sparks (the ones in the air finish on their own).
public class ShotFade : MonoBehaviour
{
    public const float Seconds = 0.25f;

    private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    private readonly List<float> _alphas = new List<float>();
    private float _startedAt;

    /// <summary>Fades <paramref name="shot"/> out and then destroys it. Safe to
    /// call twice; the second call does nothing.</summary>
    public static void Begin(GameObject shot)
    {
        if (shot == null || shot.GetComponent<ShotFade>() != null) return;
        shot.AddComponent<ShotFade>();
    }

    private void Awake()
    {
        _startedAt = Time.time;

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;

        foreach (ParticleSystem p in GetComponentsInChildren<ParticleSystem>())
        {
            p.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (SpriteRenderer s in GetComponentsInChildren<SpriteRenderer>())
        {
            _sprites.Add(s);
            _alphas.Add(s.color.a);
        }
    }

    // LateUpdate, so the fade has the last word over anything that tints the
    // shot during the frame
    private void LateUpdate()
    {
        float t = Mathf.Clamp01((Time.time - _startedAt) / Seconds);

        for (int i = 0; i < _sprites.Count; i++)
        {
            SpriteRenderer s = _sprites[i];
            if (s == null) continue;
            Color c = s.color;
            c.a = _alphas[i] * (1f - t);
            s.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
