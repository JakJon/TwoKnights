using System.Collections.Generic;
using UnityEngine;

// Bowsight: a laser sight bolted to the shield. The knight can't move, so every shot is a
// question of angle — the beam turns that guess into a read, and anything that sits in it
// starts to cook.
//
// Lives on the shield GameObject and parents its own renderers, so it inherits the
// shield's rotation and orbit for free. Deliberately unlit: a sight that vanishes in a
// dark map is no sight at all.
//
// THE BEAM IS DRAWN AS TWO SEGMENTS with a gap running up it, rather than as one solid
// line. That is not decoration. A static line has no direction — it reads as a bar the
// shield happens to be holding, and at a glance you cannot tell which end is the shield
// and which is downrange. A gap travelling outward and restarting at the base gives the
// beam a flow, and the flow is the read: it always runs AWAY from the knight, so the
// beam answers "which way am I pointing" without the player having to trace it.
//
// Drawn by scaling two renderers rather than by punching a hole in the texture every
// frame — the sprites are a few hundred pixels wide and re-uploading them at frame rate
// to move a twenty-pixel hole is a lot of bus traffic for an effect that is really just
// two transforms.
public class ShieldSight : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const int CoreThicknessPixels = 1;
    private const int GlowThicknessPixels = 11; // odd, so the core lands on its centre row
    private const int StartOffsetPixels = 8;    // clear of the shield's own face

    private static readonly Color32 CoreColor = new Color32(255, 48, 48, 255);
    private static readonly Color32 GlowColor = new Color32(255, 40, 40, 255);

    // Cross-section falloff of the glow, centre outwards. Baked into the texture; the
    // pulse then scales the whole thing via the renderer's alpha.
    private static readonly float[] GlowFalloff = { 0.85f, 0.62f, 0.42f, 0.27f, 0.15f, 0.07f };

    // The glow breathes; the core beam never does. A flickering core reads as a
    // rendering fault rather than a laser.
    private const float PulseSeconds = 1.15f;
    private const float PulseMinAlpha = 0.38f;
    private const float PulseMaxAlpha = 0.7f;

    // The travelling gap. Both figures are in WORLD units and converted to the beam's
    // pixel grid, so the gap is the same size and moves at the same speed whatever range
    // the tier bought — a longer beam takes proportionally longer to sweep, which is what
    // makes the sweep read as something moving along the beam rather than as the whole
    // beam flashing in place.
    private const float GapWorldWidth = 0.3f;
    private const float GapWorldSpeed = 7f;

    // Contact damage is quantised: one point per interval of unbroken contact, with the
    // interval derived from the authored dps. Breaking contact discards the partial tick,
    // so sweeping the beam across a crowd does nothing — you have to hold it on something.
    private const int DamagePerTick = 1;

    // The beam in two pieces, near end and far end, with the gap between them
    private SpriteRenderer _coreNear, _coreFar;
    private SpriteRenderer _glowNear, _glowFar;
    private ParticleSystem _motes;
    private Transform _beamRoot;
    private GameObject _damageSource;

    private Texture2D _coreTexture, _glowTexture, _moteTexture;
    private Sprite _coreSprite, _glowSprite;
    private Material _beamMaterial, _moteMaterial;

    private float _rangeUnits;
    private float _damagePerSecond;
    private bool _ignites;

    private int _beamPixels;
    private float _gapPixels;   // width of the gap on the beam's pixel grid
    private float _gapPosition; // near edge of the gap, in pixels from the beam root

    private readonly Dictionary<EnemyBase, float> _contact = new Dictionary<EnemyBase, float>();
    private readonly List<EnemyBase> _inBeam = new List<EnemyBase>();
    private readonly List<EnemyBase> _stale = new List<EnemyBase>();
    private readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>();
    private ContactFilter2D _filter;

    public float Range => _rangeUnits;
    public float DamagePerSecond => _damagePerSecond;
    public bool Ignites => _ignites;

    // Absolute, like the shield-shape setter: a tier states the beam it wants outright.
    public void Configure(float worldUnits, float damagePerSecond)
    {
        _rangeUnits = Mathf.Max(0f, worldUnits);
        _damagePerSecond = Mathf.Max(0f, damagePerSecond);
        Rebuild();
    }

    /// <summary>
    /// Fire Sight: the beam stops merely cooking what it touches and sets it alight.
    /// One-way — a capstone is never un-bought — so a later Bowsight tier re-running
    /// Configure cannot quietly turn it back off.
    /// </summary>
    public void SetIgnites(bool ignites)
    {
        _ignites = _ignites || ignites;
    }

    private void Awake()
    {
        // Enemy colliders are triggers, so a default filter would see straight through them
        _filter = new ContactFilter2D { useTriggers = true };
        _filter.useLayerMask = false;
        _filter.useDepth = false;
    }

    private void Update()
    {
        if (_rangeUnits <= 0f) return;

        PulseGlow();
        AdvanceGap();
        TickBeamDamage();
    }

    private void PulseGlow()
    {
        float t = Mathf.Sin(Time.time * (Mathf.PI * 2f / PulseSeconds)) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha, t);
        var colour = new Color(1f, 1f, 1f, alpha);

        if (_glowNear != null) _glowNear.color = colour;
        if (_glowFar != null) _glowFar.color = colour;
    }

    // ---- The travelling gap ----

    // Runs from just behind the shield to just past the tip and then restarts at the
    // base. It starts fully BEHIND the near end (a negative position) and finishes fully
    // past the far one, so the gap enters and leaves the beam rather than popping into
    // existence part-way up it.
    private void AdvanceGap()
    {
        if (_beamPixels <= 0 || _coreNear == null) return;

        float worldPerPixel = WorldPerPixel();
        float speed = GapWorldSpeed / Mathf.Max(worldPerPixel, 0.0001f);

        _gapPosition += speed * Time.deltaTime;
        if (_gapPosition >= _beamPixels) _gapPosition = -_gapPixels;

        float gapStart = _gapPosition;
        float gapEnd = _gapPosition + _gapPixels;

        // Near piece: the beam from the shield up to the gap. Far piece: from the far
        // side of the gap to the tip. Either can be empty while the gap straddles an end.
        float nearEnd = Mathf.Clamp(gapStart, 0f, _beamPixels);
        float farStart = Mathf.Clamp(gapEnd, 0f, _beamPixels);

        LaySegment(_coreNear, 0f, nearEnd);
        LaySegment(_glowNear, 0f, nearEnd);
        LaySegment(_coreFar, farStart, _beamPixels);
        LaySegment(_glowFar, farStart, _beamPixels);
    }

    // Position and scale one piece of the beam to cover pixels [from, to). The sprite is
    // pivoted on its near end at PixelsPerUnit, so a scale of 1 is the whole beam and the
    // offset is just pixels converted into the root's local units.
    private void LaySegment(SpriteRenderer piece, float from, float to)
    {
        if (piece == null) return;

        float span = to - from;
        if (span <= 0.5f) // sub-pixel slivers read as flicker, not as a beam
        {
            piece.enabled = false;
            return;
        }

        piece.enabled = true;
        piece.transform.localPosition = new Vector3(from / PixelsPerUnit, 0f, 0f);
        piece.transform.localScale = new Vector3(span / Mathf.Max(1, _beamPixels), 1f, 1f);
    }

    // ---- Damage ----

    private void TickBeamDamage()
    {
        if (_damagePerSecond <= 0f || _beamRoot == null) return;

        Vector2 origin = _beamRoot.position;
        Vector2 direction = transform.right;

        _inBeam.Clear();
        _hits.Clear();
        Physics2D.Raycast(origin, direction, _filter, _hits, _rangeUnits);

        for (int i = 0; i < _hits.Count; i++)
        {
            var collider = _hits[i].collider;
            if (collider == null) continue;

            // Colliders often hang off a child of the enemy root
            var enemy = collider.GetComponentInParent<EnemyBase>();
            if (enemy == null || _inBeam.Contains(enemy)) continue;

            _inBeam.Add(enemy);
        }

        float interval = DamagePerTick / _damagePerSecond;
        for (int i = 0; i < _inBeam.Count; i++)
        {
            EnemyBase enemy = _inBeam[i];

            // Fire Sight, before the tick. Lighting anything in the beam that is not
            // already alight — rather than on entering it — is what makes the capstone
            // read as "the beam burns": an enemy held in it stays lit as stacks expire,
            // and one that wanders in is lit the moment it crosses. IsIgnited is the
            // guard that keeps this from adding a burn stack every single frame.
            if (_ignites && !enemy.IsIgnited) enemy.Ignite(DamageSourceTag());

            _contact.TryGetValue(enemy, out float held);
            held += Time.deltaTime;

            // A loop, not an if: a long frame or a fast beam can owe more than one tick.
            // EnemyBase ignores damage once dead, so overshooting a kill is harmless.
            while (held >= interval)
            {
                enemy.TakeDamage(DamagePerTick, _damageSource);
                held -= interval;
            }

            _contact[enemy] = held;
        }

        // Drop anything that left the beam (or died), so partial ticks don't bank and
        // the dictionary can't grow across a wave
        _stale.Clear();
        foreach (var pair in _contact)
        {
            if (!_inBeam.Contains(pair.Key)) _stale.Add(pair.Key);
        }
        for (int i = 0; i < _stale.Count; i++) _contact.Remove(_stale[i]);
    }

    // The knight's own tag — Ignite credits the burn to a KNIGHT, not to a projectile
    // token, because it reads the igniting knight's Ember sheet for the stack's dps and
    // its trail. Cached: this walks the parent chain and the beam ticks every frame.
    private string _knightTag;

    private string DamageSourceTag()
    {
        if (_knightTag == null) _knightTag = FindKnightTag(transform) ?? "";
        return _knightTag;
    }

    // ---- Build ----

    private void Rebuild()
    {
        EnsureBeam();
        if (_coreNear == null) return;

        bool visible = _rangeUnits > 0f;
        if (!visible)
        {
            _coreNear.enabled = _coreFar.enabled = false;
            _glowNear.enabled = _glowFar.enabled = false;
            if (_motes != null) _motes.Stop();
            return;
        }

        // The beam root sits at the shield's scale, so a beam pixel is the same size on
        // screen as a shield pixel — convert the designed world reach into that grid.
        float worldPerPixel = WorldPerPixel();
        _beamPixels = Mathf.Max(1, Mathf.RoundToInt(_rangeUnits / Mathf.Max(worldPerPixel, 0.0001f)));
        _gapPixels = Mathf.Max(2f, GapWorldWidth / Mathf.Max(worldPerPixel, 0.0001f));
        _gapPosition = -_gapPixels;

        DisposeSprites();
        _coreTexture = BuildBeamTexture(_beamPixels, CoreThicknessPixels, core: true);
        _glowTexture = BuildBeamTexture(_beamPixels, GlowThicknessPixels, core: false);
        _coreSprite = MakeBeamSprite(_coreTexture);
        _glowSprite = MakeBeamSprite(_glowTexture);

        _coreNear.sprite = _coreFar.sprite = _coreSprite;
        _glowNear.sprite = _glowFar.sprite = _glowSprite;

        AdvanceGap();
        ConfigureMotes(_beamPixels * worldPerPixel);
    }

    private float WorldPerPixel()
    {
        return transform.lossyScale.x / PixelsPerUnit;
    }

    private static Sprite MakeBeamSprite(Texture2D texture)
    {
        // Pivot on the near end so the beam grows outward and its root stays put
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private static Texture2D BuildBeamTexture(int length, int thickness, bool core)
    {
        var pixels = new Color32[length * thickness];
        int centre = thickness / 2;

        for (int y = 0; y < thickness; y++)
        {
            int distance = Mathf.Abs(y - centre);
            Color32 colour;
            if (core)
            {
                colour = CoreColor;
            }
            else
            {
                float falloff = distance < GlowFalloff.Length ? GlowFalloff[distance] : 0f;
                colour = new Color32(GlowColor.r, GlowColor.g, GlowColor.b,
                    (byte)Mathf.RoundToInt(falloff * 255f));
            }

            for (int x = 0; x < length; x++) pixels[y * length + x] = colour;
        }

        var texture = new Texture2D(length, thickness, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private void EnsureBeam()
    {
        if (_beamRoot != null) return;

        var shieldRenderer = GetComponent<SpriteRenderer>();
        _beamMaterial = MakeUnlitMaterial(shieldRenderer);

        var root = new GameObject("Sight");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(StartOffsetPixels / PixelsPerUnit, 0f, 0f);
        _beamRoot = root.transform;

        int layer = shieldRenderer != null ? shieldRenderer.sortingLayerID : 0;
        int order = shieldRenderer != null ? shieldRenderer.sortingOrder : 0;

        // Glow behind the core, both behind the shield itself
        _glowNear = MakeBeamRenderer(root.transform, "GlowNear", layer, order - 2);
        _glowFar = MakeBeamRenderer(root.transform, "GlowFar", layer, order - 2);
        _coreNear = MakeBeamRenderer(root.transform, "CoreNear", layer, order - 1);
        _coreFar = MakeBeamRenderer(root.transform, "CoreFar", layer, order - 1);

        var start = new Color(1f, 1f, 1f, PulseMinAlpha);
        _glowNear.color = start;
        _glowFar.color = start;

        BuildMotes(root.transform, layer, order - 2);
        BuildDamageSource(root.transform);
    }

    private SpriteRenderer MakeBeamRenderer(Transform parent, string name, int sortingLayer, int sortingOrder)
    {
        var host = new GameObject(name);
        host.transform.SetParent(parent, false);
        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        if (_beamMaterial != null) renderer.sharedMaterial = _beamMaterial;
        return renderer;
    }

    // Sprites/Default is in the project's always-included shaders, so this survives a
    // build; fall back to the shield's own (lit) material if it somehow isn't.
    private static Material MakeUnlitMaterial(SpriteRenderer fallback)
    {
        var unlit = Shader.Find("Sprites/Default");
        if (unlit != null) return new Material(unlit);
        return fallback != null ? fallback.sharedMaterial : null;
    }

    // A collider-less token whose tag credits the beam's kills to the right knight —
    // EnemyBase reads the damage source's tag to award death specials.
    private void BuildDamageSource(Transform parent)
    {
        _damageSource = new GameObject("SightDamageSource");
        _damageSource.transform.SetParent(parent, false);

        string knightTag = FindKnightTag(transform);
        if (knightTag != null) _damageSource.tag = knightTag + "Projectile";
    }

    private static string FindKnightTag(Transform from)
    {
        for (Transform t = from; t != null; t = t.parent)
        {
            if (t.CompareTag("PlayerLeft") || t.CompareTag("PlayerRight")) return t.tag;
        }
        return null;
    }

    // ---- Motes ----

    // Sparse embers drifting off the beam. Kept deliberately thin: the glow carries the
    // effect, and a busy particle stream would fight the pixel art.
    private void BuildMotes(Transform parent, int sortingLayer, int sortingOrder)
    {
        var host = new GameObject("Motes");
        host.transform.SetParent(parent, false);
        // Cancel the shield's scale so every particle figure below is in world units
        float shieldScale = Mathf.Max(transform.lossyScale.x, 0.0001f);
        host.transform.localScale = Vector3.one / shieldScale;

        _motes = host.AddComponent<ParticleSystem>();
        _motes.Stop();

        var main = _motes.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startColor = (Color)new Color32(CoreColor.r, CoreColor.g, CoreColor.b, 190);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;

        var emission = _motes.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        var shape = _motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        var fade = _motes.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        _moteTexture = BuildMoteTexture();
        _moteMaterial = MakeUnlitMaterial(GetComponent<SpriteRenderer>());
        if (_moteMaterial != null) _moteMaterial.mainTexture = _moteTexture;

        var renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        if (_moteMaterial != null) renderer.material = _moteMaterial;
    }

    private void ConfigureMotes(float beamWorldLength)
    {
        if (_motes == null) return;

        var shape = _motes.shape;
        // Emit along the beam, in a band just wider than the glow
        float band = GlowThicknessPixels / PixelsPerUnit * transform.lossyScale.x;
        shape.scale = new Vector3(beamWorldLength, band, 0f);
        shape.position = new Vector3(beamWorldLength * 0.5f, 0f, 0f);

        if (!_motes.isPlaying) _motes.Play();
    }

    // A 4x4 soft dot — round enough to read as an ember, small enough to stay subtle
    private static Texture2D BuildMoteTexture()
    {
        const int size = 4;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - (size - 1) * 0.5f;
                float dy = y - (size - 1) * 0.5f;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(falloff * 255f));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ---- Teardown ----

    private void DisposeSprites()
    {
        if (_coreSprite != null) Destroy(_coreSprite);
        if (_glowSprite != null) Destroy(_glowSprite);
        if (_coreTexture != null) Destroy(_coreTexture);
        if (_glowTexture != null) Destroy(_glowTexture);
        _coreSprite = _glowSprite = null;
        _coreTexture = _glowTexture = null;
    }

    private void OnDestroy()
    {
        DisposeSprites();
        if (_moteTexture != null) Destroy(_moteTexture);
        if (_beamMaterial != null) Destroy(_beamMaterial);
        if (_moteMaterial != null) Destroy(_moteMaterial);
    }
}
