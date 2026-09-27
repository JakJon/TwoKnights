using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An orb that belongs to an Order trial rather than to a wave. It looks like the
/// orb it was built from and is collected the same way — any of a knight's arrows,
/// or the blade — but it pays nothing: no health, no special (owner's call). What
/// it does instead is tell the trial it was hit, or that it got away.
///
/// Built by instantiating the ordinary orb prefab and swapping its behaviour out,
/// so the art, the animation and the collider are the real orb's and stay in step
/// with it.
///
/// Two ways to be spent:
///   - Consumed (fire, ice): the first hit takes it, like a normal orb.
///   - Bitten (venom): the first hit marks it and it carries on, darkened. Every
///     hit after that is <see cref="StruckAgain"/> — the Serpent's trial is about
///     not wasting a second dose — and the arrow that lands it is spent on it, so
///     one shot can never bite a fresh orb and then carry on into a bitten one.
///
/// An orb can make several passes across the field (the venom orbs make three, so
/// nobody gets only one look at one). Between passes it waits off-screen.
/// </summary>
public class TrialOrb : MonoBehaviour, IChillable
{
    public struct Pass
    {
        public Vector2 From;
        public Vector2 To;
        public Pass(Vector2 from, Vector2 to) { From = from; To = to; }
    }

    public enum Look { Fire, Ice, Venom }

    /// <summary>First hit on a fresh orb.</summary>
    public event Action<TrialOrb> Struck;
    /// <summary>A hit on an orb that was already bitten. Venom orbs only.</summary>
    public event Action<TrialOrb> StruckAgain;
    /// <summary>Finished its last pass without ever being hit.</summary>
    public event Action<TrialOrb> Escaped;

    /// <summary>Seconds an orb spends off-screen between two passes.</summary>
    private const float PassGapSeconds = 0.8f;

    /// <summary>How far a bitten orb darkens. Dark enough to tell apart at a glance, not so dark it vanishes.</summary>
    private static readonly Color BittenTint = new Color(0.34f, 0.34f, 0.34f, 1f);

    private readonly List<Pass> _passes = new List<Pass>();
    private int _pass;
    private float _speed;
    private float _restUntil = -1f;
    private bool _consumedOnHit;
    private Look _look;

    private Collider2D _collider;
    private Vector2 _centreOffset;
    private SpriteRenderer _renderer;
    private PoisonBubbleEffect _bubbles;

    // Venom orbs wear their own frames: the mana orb's art in Serpent green, swapped
    // in frame for frame over the mana orb's own animation. See LateUpdate.
    private static Sprite[] _venomFrames;

    public bool Bitten { get; private set; }

    // Set by the first hit on a consumed orb. Destroy lands at the end of the
    // frame, so a second shot touching it on the same frame would count it twice.
    private bool _spent;

    /// <summary>
    /// A contact this soon after the bite is the same hit arriving twice, not a
    /// second dose. Short enough that a deliberate second shot is never forgiven.
    /// </summary>
    private const float BiteGraceSeconds = 0.05f;
    private float _bittenAt = -1f;

    /// <summary>
    /// Puts an orb on its first pass. <paramref name="passes"/> are in the orb's
    /// VISUAL centre — the prefab's pivot sits at the bottom of the art, and a lane
    /// authored at the transform would ride a quarter of a unit higher than it says.
    /// </summary>
    public static TrialOrb Spawn(GameObject orbPrefab, Look look, float speed, IList<Pass> passes)
    {
        if (orbPrefab == null || passes == null || passes.Count == 0) return null;

        var go = Instantiate(orbPrefab);
        go.name = "Trial Orb (" + look + ")";

        // Gone before its first Update: CollectibleOrb with no Initialize has nowhere
        // to go and would destroy itself on the first frame — and would pay out if it
        // were ever touched.
        var ordinary = go.GetComponent<CollectibleOrb>();
        if (ordinary != null) DestroyImmediate(ordinary);

        var orb = go.AddComponent<TrialOrb>();
        orb.Setup(look, speed, passes);
        return orb;
    }

    private void Setup(Look look, float speed, IList<Pass> passes)
    {
        _look = look;
        _speed = speed;
        _consumedOnHit = look != Look.Venom;
        _passes.AddRange(passes);

        _collider = GetComponent<Collider2D>();
        _renderer = GetComponentInChildren<SpriteRenderer>();
        _centreOffset = _collider != null
            ? (Vector2)transform.TransformVector(_collider.offset)
            : Vector2.zero;

        PlaceAt(_passes[0].From);
        UpdateCollider();

        // Sunwell III reaches trial orbs too (owner's call: the knight's build still
        // counts in a trial). Asked once, as the ordinary orb does.
        if (DawnBoost.AnyKnightSlowsOrbs())
        {
            _speed *= DawnBoost.SlowedOrbSpeedMultiplier;
            DawnFx.AttachOrbGlow(gameObject);
        }

        Dress();
    }

    private void Dress()
    {
        switch (_look)
        {
            case Look.Fire:
                // Flames round it and a streak behind it: the streak is what reads as
                // "twice as fast" before the player has had time to measure it.
                FireFx.AttachEnemyFlame(gameObject);
                FireFx.AttachArrowTrail(gameObject);
                break;
            case Look.Ice:
                FrostFx.AttachEnemyChill(gameObject);
                FrostFx.ShowIce(gameObject);
                break;
            case Look.Venom:
                _bubbles = AttachBubbles(gameObject);
                break;
        }
    }

    // ---------- moving ----------

    private void Update()
    {
        if (_pass >= _passes.Count) return;

        if (_restUntil > 0f)
        {
            if (Time.time < _restUntil) return;
            _restUntil = -1f;
            PlaceAt(_passes[_pass].From);
        }

        var pass = _passes[_pass];
        Vector2 centre = Centre;
        Vector2 next = Vector2.MoveTowards(centre, pass.To, _speed * ChillScale * Time.deltaTime);
        PlaceAt(next);
        UpdateCollider();

        if ((next - pass.To).sqrMagnitude > 1e-6f) return;

        _pass++;
        if (_pass < _passes.Count)
        {
            _restUntil = Time.time + PassGapSeconds;
            return;
        }

        // Last pass over. A fresh orb got away; a bitten one has simply finished.
        if (!Bitten) Escaped?.Invoke(this);
        Retire();
    }

    private Vector2 Centre => (Vector2)transform.position + _centreOffset;

    private void PlaceAt(Vector2 centre)
    {
        transform.position = centre - _centreOffset;
    }

    /// <summary>
    /// Only hittable where the player can see it. An arrow keeps flying for four
    /// seconds after it leaves the frame, and a venom orb waiting out of sight
    /// between passes must not be "bitten again" by a shot nobody aimed at it.
    /// </summary>
    private void UpdateCollider()
    {
        if (_collider == null) return;
        _collider.enabled = Spawner.IsInView(Centre);
    }

    // ---- cold (Frigid) — slowed exactly as an ordinary orb is ----
    private float _chillMultiplier = 1f;
    private float _chillUntil = -1f;
    private bool _chillGlow;

    public void ApplyChill(float speedMultiplier, float seconds)
    {
        if (seconds <= 0f) return;
        _chillMultiplier = Mathf.Clamp(Mathf.Min(_chillMultiplier, speedMultiplier), 0.05f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + seconds);
        if (!_chillGlow && _look != Look.Ice)
        {
            _chillGlow = true;
            FrostFx.AttachEnemyChill(gameObject);
        }
    }

    private float ChillScale => Time.time < _chillUntil ? _chillMultiplier : 1f;

    // ---------- being hit ----------

    private void OnTriggerEnter2D(Collider2D other)
    {
        GameObject knight = CollectibleOrb.CollectorFor(other);
        if (knight == null) return;

        if (_consumedOnHit)
        {
            if (_spent) return;
            _spent = true;
            Struck?.Invoke(this);
            Burst();
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.orbCollect);
            Retire();
            return;
        }

        // Venom: the shot is spent on the orb either way.
        SpendShot(other);

        if (Bitten)
        {
            if (Time.time - _bittenAt > BiteGraceSeconds) StruckAgain?.Invoke(this);
            return;
        }

        Bitten = true;
        _bittenAt = Time.time;
        if (_renderer != null) _renderer.color = BittenTint;
        if (_bubbles != null) _bubbles.StopBubbles();
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.poisoned);
        Struck?.Invoke(this);
    }

    /// <summary>
    /// Takes a knight's projectile off the board once it has landed on a trial
    /// target, so one shot is one hit. The BODY goes, not the collider: a shuriken
    /// wears its collider on a child. Only ever a tagged projectile — a blade's
    /// detector can share the knight's own rigidbody, and that must never be
    /// "spent".
    /// </summary>
    public static void SpendShot(Collider2D other)
    {
        if (other == null) return;
        if (!other.CompareTag("PlayerLeftProjectile") && !other.CompareTag("PlayerRightProjectile")) return;
        GameObject shot = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        Destroy(shot);
    }

    /// <summary>
    /// Leaves the board. The ice shell is handed back first, the way EnemyBase hands
    /// it back on a thaw: FrostFx lets it fade rather than popping, and its table of
    /// shells stops holding this orb. An orb the trial sweeps off on a loss skips
    /// this, exactly as an enemy killed while frozen does.
    /// </summary>
    private void Retire()
    {
        if (_look == Look.Ice) FrostFx.HideIce(gameObject);
        Destroy(gameObject);
    }

    private void Burst()
    {
        Vector2 at = Centre;
        if (_look == Look.Fire) FireFx.Burst(at, 0.6f);
        else if (_look == Look.Ice) FrostFx.Burst(at, 0.6f);
    }

    private void LateUpdate()
    {
        if (_look != Look.Venom || _renderer == null || _renderer.sprite == null) return;
        var frames = VenomFrames;
        if (frames == null) return;
        int index = FrameIndex(_renderer.sprite.name);
        if (index >= 0 && index < frames.Length && frames[index] != null
            && _renderer.sprite != frames[index])
        {
            _renderer.sprite = frames[index];
        }
    }

    // ---------- the venom orb's art ----------

    /// <summary>
    /// Orb_Venom's twelve frames, in frame order. It is the mana orb recoloured into
    /// the Serpent's green (Assets/Resources/Trials/Orb_Venom.aseprite), loaded from
    /// Resources because an .aseprite sprite reference cannot be written into a
    /// prefab by hand.
    /// </summary>
    private static Sprite[] VenomFrames
    {
        get
        {
            if (_venomFrames != null) return _venomFrames;
            var loaded = Resources.LoadAll<Sprite>("Trials/Orb_Venom");
            int count = 0;
            for (int i = 0; i < loaded.Length; i++) count = Mathf.Max(count, FrameIndex(loaded[i].name) + 1);
            _venomFrames = new Sprite[count];
            for (int i = 0; i < loaded.Length; i++)
            {
                int index = FrameIndex(loaded[i].name);
                if (index >= 0) _venomFrames[index] = loaded[i];
            }
            if (count == 0) Debug.LogWarning("[Trials] Orb_Venom frames not found in Resources/Trials — venom orbs fall back to the mana orb's colours.");
            return _venomFrames;
        }
    }

    /// <summary>"Frame_7" → 7, whatever the importer puts in front of it.</summary>
    private static int FrameIndex(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return -1;
        int end = spriteName.Length;
        int start = end;
        while (start > 0 && char.IsDigit(spriteName[start - 1])) start--;
        if (start == end) return -1;
        return int.TryParse(spriteName.Substring(start, end - start), out int value) ? value : -1;
    }

    /// <summary>The same bubbles a poisoned rock trails, so the orb reads as venom on sight.</summary>
    private static PoisonBubbleEffect AttachBubbles(GameObject host)
    {
        var resources = PoisonResourceManager.Instance;
        GameObject prefab = resources != null ? resources.GetPoisonBubblePrefab() : null;

        GameObject child;
        PoisonBubbleEffect bubbles;
        if (prefab != null)
        {
            child = Instantiate(prefab, host.transform.position, Quaternion.identity);
            child.transform.SetParent(host.transform);
            child.transform.localPosition = Vector3.zero;
            bubbles = child.GetComponent<PoisonBubbleEffect>();
            if (bubbles == null) bubbles = child.AddComponent<PoisonBubbleEffect>();
        }
        else
        {
            child = new GameObject("VenomOrbBubbles");
            child.transform.SetParent(host.transform);
            child.transform.localPosition = Vector3.zero;
            bubbles = child.AddComponent<PoisonBubbleEffect>();
            Sprite bubble = resources != null ? resources.GetPoisonBubbleSprite() : null;
            if (bubble != null) bubbles.SetBubbleSprite(bubble);
        }

        float rate = resources != null ? resources.projectileBubbleRate : 5f;
        bubbles.SetBubbleRate(rate);
        bubbles.StartBubbles();
        return bubbles;
    }
}
