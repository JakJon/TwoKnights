using UnityEngine;

/// <summary>
/// The paladin's light and his ring of shields, drawn the way the upgrade menu
/// draws them over a Guardian knight rather than the way the arena draws smoke.
///
/// The particle versions read wrong for two reasons, and both are baked into what
/// a ParticleSystem is:
///
///   THE LIGHT was a cloud of separate motes. Eighteen sprites a second, each half
///   a unit across and each fading on its own clock, is dust — a shape made of
///   grains you can count. The menu's version is one soft disc that breathes, so
///   that is what this is: a single sprite with a cubed falloff, swelling and
///   dimming on one slow sine. No grain, no seams.
///
///   THE RING spun flat. velocityOverLifetime's orbitalZ turns particles about the
///   Z axis, which is a circle drawn on the screen — the orbs went round and round
///   in front of the paladin's face. The menu squashes its ring to a third of its
///   height and draws the far half smaller and dimmer, so it reads as an orbit
///   passing behind his head and back around. That needs per-orb placement, which
///   is what this component does every frame.
///
/// The numbers mirror UpgradeMenu.OrbitEffect: the same 0.34 flattening, the same
/// near/far size and alpha split, and a gleam behind each orb that swells as it
/// swings past the front.
/// </summary>
public class NpcAura : MonoBehaviour
{
    /// <summary>How flat the orbit is. 1 would be a circle on the screen; this is the menu's value.</summary>
    private const float Flatten = 0.34f;

    /// <summary>Seconds a released aura takes to go out.</summary>
    private const float FadeSeconds = 0.5f;

    private SpriteRenderer _bloom;
    private SpriteRenderer[] _orbs;
    private SpriteRenderer[] _gleams;

    private Color _tint;
    private float _bloomSize;
    private float _radius;
    private float _orbSize;
    private float _degreesPerSecond;
    private float _angle;

    private float _strength = 1f;
    private bool _fading;
    private float _breath;

    // ---------- building ----------

    /// <summary>
    /// The breathing light he fades in inside of. One disc, not a cloud.
    /// </summary>
    public static NpcAura Bloom(GameObject host, Color tint, float size, int sortingOrder)
    {
        if (host == null) return null;

        var child = new GameObject("NPC Bloom");
        child.transform.SetParent(host.transform, false);
        child.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        var aura = child.AddComponent<NpcAura>();
        aura._tint = tint;
        aura._bloomSize = size;
        aura._bloom = MakeDisc(child.transform, sortingOrder, tint, size);
        return aura;
    }

    /// <summary>
    /// The ring of orbs over his head: <paramref name="count"/> of them on a
    /// flattened orbit, each carrying its own gleam.
    /// </summary>
    public static NpcAura Ring(GameObject host, Color tint, int count, float radius,
                               float orbSize, float degreesPerSecond,
                               float height, int sortingOrder)
    {
        if (host == null || count <= 0) return null;

        var child = new GameObject("NPC Orb Ring");
        child.transform.SetParent(host.transform, false);
        child.transform.localPosition = new Vector3(0f, height, 0f);

        var aura = child.AddComponent<NpcAura>();
        aura._tint = tint;
        aura._radius = radius;
        aura._orbSize = orbSize;
        aura._degreesPerSecond = degreesPerSecond;
        aura._orbs = new SpriteRenderer[count];
        aura._gleams = new SpriteRenderer[count];

        for (int i = 0; i < count; i++)
        {
            // Every gleam goes down before any orb does, so an orb is never drawn
            // under the halo of the one behind it. Same sorting order throughout —
            // child index is what settles it.
            aura._gleams[i] = MakeDisc(child.transform, sortingOrder, tint, orbSize * 4.5f);
        }
        for (int i = 0; i < count; i++)
        {
            aura._orbs[i] = MakeDisc(child.transform, sortingOrder, tint, orbSize);
        }

        aura.PlaceRing();
        return aura;
    }

    // ---------- running ----------

    private void LateUpdate()
    {
        // The whole quest scene runs at timeScale zero, so nothing here may read
        // scaled time — the same trap the particle version had to document.
        float dt = Time.unscaledDeltaTime;

        if (_fading)
        {
            _strength -= dt / FadeSeconds;
            if (_strength <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (_bloom != null)
        {
            // Breathing, not flickering: one slow sine driving scale and alpha
            // together, which is the menu's core disc swelling on its transition.
            _breath += dt;
            float wave = Mathf.Sin(_breath * 1.7f);
            SetSize(_bloom, _bloomSize * (1f + 0.06f * wave));
            _bloom.color = Tinted((0.42f + 0.10f * wave) * _strength);
        }

        if (_orbs != null)
        {
            _angle += _degreesPerSecond * dt;
            PlaceRing();
        }
    }

    private void PlaceRing()
    {
        if (_orbs == null) return;

        for (int i = 0; i < _orbs.Length; i++)
        {
            float theta = Mathf.Deg2Rad * _angle + (Mathf.PI * 2f * i) / _orbs.Length;
            float x = Mathf.Cos(theta) * _radius;
            float y = Mathf.Sin(theta) * _radius * Flatten;

            // sin(theta) > 0 is the far half of the orbit: smaller, dimmer, behind.
            // This split is the whole difference between an orbit and a circle
            // spinning on the screen.
            float near = (1f - Mathf.Sin(theta)) * 0.5f;
            float size = _orbSize * (0.68f + 0.5f * near);
            float alpha = 0.75f * (0.45f + 0.75f * near) * _strength;

            var orb = _orbs[i];
            orb.transform.localPosition = new Vector3(x, y, 0f);
            SetSize(orb, size);
            orb.color = Tinted(alpha);

            // The light an orb carries is brightest as it passes in front.
            var gleam = _gleams[i];
            gleam.transform.localPosition = new Vector3(x, y, 0f);
            SetSize(gleam, size * 4.5f);
            gleam.color = Tinted(alpha * 0.42f * near);
        }
    }

    /// <summary>Fades the aura out, then takes itself with it.</summary>
    public void Release()
    {
        _fading = true;
    }

    private Color Tinted(float alpha) =>
        new Color(_tint.r, _tint.g, _tint.b, Mathf.Clamp01(alpha));

    // ---------- the disc ----------

    private static void SetSize(SpriteRenderer renderer, float worldSize)
    {
        if (renderer == null || renderer.sprite == null) return;
        float native = renderer.sprite.bounds.size.x;
        float scale = native > 0f ? worldSize / native : 1f;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static SpriteRenderer MakeDisc(Transform parent, int sortingOrder, Color tint, float size)
    {
        var go = new GameObject("Disc");
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Disc;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
        renderer.color = tint;
        SetSize(renderer, size);
        return renderer;
    }

    private static Sprite _disc;

    /// <summary>
    /// A white disc with a cubed falloff — a bright, tight core and a long faint
    /// tail, which is what reads as light rather than as a circle. Generated rather
    /// than imported so it stays in step with UpgradeMenu.SoftDisc, whose curve
    /// this is line for line.
    /// </summary>
    private static Sprite Disc
    {
        get
        {
            if (_disc != null) return _disc;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "NpcAuraDisc",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                // Generated, not an asset — without this it leaks into the scene on
                // every domain reload in the editor.
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _disc = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                                  new Vector2(0.5f, 0.5f), 128f);
            _disc.name = "NpcAuraDisc";
            _disc.hideFlags = HideFlags.HideAndDontSave;
            return _disc;
        }
    }
}
