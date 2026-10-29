using System.Collections.Generic;
using UnityEngine;

// The soft light the gems and ore painted into the arena backdrop give off now
// and then.
//
// BackgroundController builds this on a child of the backdrop, the same way it
// builds its foreground renderer, and hands it each stage's GemGlowProfile
// along with the sprites the stage is showing. CampfireScene does the same for
// the camp, which is pitched on the same stages.
//
// From those it finds every gem — a clump of pixels in the profile's colours —
// and paints that gem's halo: each of its pixels spreads a little of its own
// colour onto the floor round it. Every gem gets its own small renderer, so it
// keeps its own time: dark most of the while, then a short glow every few
// seconds, out of step with its neighbours (owner, 2026-10-24: one steady glow
// over everything was too noticeable). Separate renderers rather than one
// picture of every halo because two gems side by side must be able to glow at
// different moments without either halo being cut off where they overlap.
//
// The halos draw additively just above ArenaLighting's ground shade. A gem in
// the darkest corner is a light in the dark, so the shade at the frame's edge
// must not swallow it. They are still under every knight, enemy and
// projectile: those are all on Default.
//
// The art is read back through the GPU rather than Texture2D.GetPixels, so the
// .aseprite files can keep Read/Write off and a newly painted backdrop glows
// without touching its import settings.
[DisallowMultipleComponent]
public class GemGlow : MonoBehaviour
{
    // Just over ArenaLighting's ground pass (Background/50), under everything on Default
    private const string GlowSortingLayer = "Background";
    private const int GlowSortingOrder = 51;

    // Pixels of the art this transparent are not looked at — the empty parts
    // of a foreground layer
    private const byte OpaqueEnough = 128;

    // Gem pixels this many art pixels apart (either way) are the same gem, so
    // a stone crossed by a dark crack or outline still glows as one
    private const int SameGemReach = 2;

    // The halos are packed into rows of an atlas this wide. A halo is a few
    // dozen texels across, so a whole stage's worth fits in a handful of rows.
    private const int AtlasWidth = 512;

    private static readonly int MasterId = Shader.PropertyToID("_Master");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int EveryId = Shader.PropertyToID("_Every");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");

    /// <summary>
    /// One gem's light: where its halo sits (art pixels from the backdrop's
    /// bottom-left corner), the halo itself, and the gem's own timing.
    /// </summary>
    private struct Halo
    {
        public int x;
        public int y;
        public int width;
        public int height;

        /// <summary>Bottom row first: rgb the light's colour with its brightest channel at full, alpha how much.</summary>
        public Color32[] pixels;

        /// <summary>How far into its cycle the gem starts, 0-1.</summary>
        public float phase;

        /// <summary>This gem's gap between glows as a multiple of the profile's.</summary>
        public float everyScale;
    }

    private readonly List<SpriteRenderer> _halos = new List<SpriteRenderer>();
    private readonly List<Sprite> _sprites = new List<Sprite>();
    private Material _material;
    private Texture2D _atlas;
    private MaterialPropertyBlock _block;

    private GemGlowProfile _profile;
    private Sprite _backdrop;
    private Sprite _foreground;
    private int _revision;

    /// <summary>
    /// Builds the glow on a child of the backdrop, sharing its transform so each
    /// halo lies exactly over the art it was painted from.
    /// </summary>
    public static GemGlow Build(Transform backdrop)
    {
        var holder = new GameObject("BackdropGemGlow");
        holder.transform.SetParent(backdrop, false);
        return holder.AddComponent<GemGlow>();
    }

    /// <summary>
    /// Switches the glow to a stage. Null profile — a stage with no gems, or a
    /// map with none at all — switches it off. Cheap to call every frame; only
    /// a real change repaints.
    /// </summary>
    public void Apply(GemGlowProfile profile, Sprite backdrop, Sprite foreground)
    {
        if (profile == _profile && backdrop == _backdrop && foreground == _foreground) return;

        _profile = profile;
        _backdrop = backdrop;
        _foreground = foreground;
        Repaint();
    }

    void LateUpdate()
    {
        if (_profile == null) return;

        // Tuning in the inspector while the game runs
        if (_profile.Revision != _revision) Repaint();
        if (_material == null || _halos.Count == 0) return;

        // Ride the global light down with the venture curtain, as the canopy
        // does — this is unlit, and would otherwise hang in the frame glowing
        // while the arena goes to black between waves. Read off the arena's
        // lighting rig rather than building one: the camp has no rig and no 2D
        // light to follow, and every arena stage with gems has a canopy, which
        // is what builds the rig.
        float light = ArenaLighting.Instance != null ? ArenaLighting.Instance.GlobalLightScale() : 1f;
        _material.SetFloat(MasterId, light);
    }

    void OnDestroy()
    {
        ClearHalos();
        Discard(_material);
    }

    private void Repaint()
    {
        _revision = _profile != null ? _profile.Revision : 0;
        ClearHalos();
        if (_profile == null || _backdrop == null) return;

        if (_profile.Shader == null)
        {
            Debug.LogWarning($"[GemGlow] {_profile.name} has no shader assigned, so it cannot draw. " +
                             "Assign Custom/GemGlow to its Shader field.");
            return;
        }

        List<Halo> halos = PaintHalos(_profile, _backdrop, _foreground);
        if (halos.Count == 0) return;

        if (_material == null || _material.shader != _profile.Shader)
        {
            Discard(_material);
            _material = new Material(_profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        _profile.PushTo(_material);
        _material.SetFloat(PixelSizeId, 1f / _backdrop.pixelsPerUnit);

        Vector2Int[] places = PackAtlas(halos);
        if (_block == null) _block = new MaterialPropertyBlock();

        // Each halo is positioned from the backdrop's own pivot and
        // pixels-per-unit, so at the backdrop's transform every halo texel sits
        // on the art pixel it was painted for
        Vector2 pivot = _backdrop.pivot;
        float pixelsPerUnit = _backdrop.pixelsPerUnit;
        for (int i = 0; i < halos.Count; i++)
        {
            Halo halo = halos[i];

            var sprite = Sprite.Create(_atlas, new Rect(places[i].x, places[i].y, halo.width, halo.height),
                Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "GemHalo";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _sprites.Add(sprite);

            var holder = new GameObject("GemHalo");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3((halo.x - pivot.x) / pixelsPerUnit,
                                                         (halo.y - pivot.y) / pixelsPerUnit, 0f);

            var renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = _material;
            renderer.sortingLayerName = GlowSortingLayer;
            renderer.sortingOrder = GlowSortingOrder;

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(PhaseId, halo.phase);
            _block.SetFloat(EveryId, _profile.GlowEvery * halo.everyScale);
            renderer.SetPropertyBlock(_block);
            _halos.Add(renderer);
        }
    }

    // Rows, tallest halos first, a clear texel between neighbours. Builds the
    // atlas and returns where each halo landed in it.
    private Vector2Int[] PackAtlas(List<Halo> halos)
    {
        int width = AtlasWidth;
        foreach (Halo halo in halos) width = Mathf.Max(width, halo.width + 2);

        var order = new List<int>(halos.Count);
        for (int i = 0; i < halos.Count; i++) order.Add(i);
        order.Sort((a, b) => halos[b].height.CompareTo(halos[a].height));

        var places = new Vector2Int[halos.Count];
        int x = 1, y = 1, rowHeight = 0;
        foreach (int i in order)
        {
            if (x + halos[i].width + 1 > width)
            {
                x = 1;
                y += rowHeight + 1;
                rowHeight = 0;
            }
            places[i] = new Vector2Int(x, y);
            x += halos[i].width + 1;
            rowHeight = Mathf.Max(rowHeight, halos[i].height);
        }
        int height = y + rowHeight + 1;

        // Linear: the halos hold light to be added in the render's own space,
        // so nothing may decode it on the way in
        _atlas = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "GemHaloAtlas",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _atlas.SetPixels32(new Color32[width * height]);
        for (int i = 0; i < halos.Count; i++)
            _atlas.SetPixels32(places[i].x, places[i].y, halos[i].width, halos[i].height, halos[i].pixels);

        // Rebuilt from scratch on every repaint, so the CPU copy can go
        _atlas.Apply(false, true);
        return places;
    }

    private void ClearHalos()
    {
        foreach (SpriteRenderer renderer in _halos)
            if (renderer != null) Discard(renderer.gameObject);
        _halos.Clear();

        foreach (Sprite sprite in _sprites) Discard(sprite);
        _sprites.Clear();

        Discard(_atlas);
        _atlas = null;
    }

    /// <summary>
    /// Every gem in a stage's art and the halo round it. Pure — no scene state —
    /// so it can be checked against the art from the editor.
    /// </summary>
    private static List<Halo> PaintHalos(GemGlowProfile profile, Sprite backdrop, Sprite foreground)
    {
        int width = Mathf.RoundToInt(backdrop.rect.width);
        int height = Mathf.RoundToInt(backdrop.rect.height);
        Color32[] art = ReadArt(backdrop);

        if (foreground != null)
        {
            if (Mathf.RoundToInt(foreground.rect.width) == width && Mathf.RoundToInt(foreground.rect.height) == height)
                LayerOver(art, ReadArt(foreground));
            else
                Debug.LogWarning($"[GemGlow] {foreground.name} is not the size of {backdrop.name}, so its gems " +
                                 "are left out of the glow.");
        }

        // The listed colours as the art stores them (sRGB bytes) for matching,
        // and as light in the space the render adds it up in
        var gems = profile.GemColours;
        var match = new Color32[gems.Count];
        var light = new Vector3[gems.Count];
        bool linearRender = QualitySettings.activeColorSpace == ColorSpace.Linear;
        for (int g = 0; g < gems.Count; g++)
        {
            match[g] = gems[g];
            Color c = linearRender ? gems[g].linear : gems[g];
            light[g] = new Vector3(c.r, c.g, c.b);
        }
        int toleranceSq = profile.ColourTolerance * profile.ColourTolerance;

        // Which listed colour every pixel is, or -1. Cave floors are long runs
        // of one colour, so most pixels are the one just matched.
        var colourAt = new int[width * height];
        Color32 lastPixel = default;
        int lastGem = -1;
        bool hasLast = false;
        for (int i = 0; i < art.Length; i++)
        {
            Color32 pixel = art[i];
            if (pixel.a < OpaqueEnough)
            {
                colourAt[i] = -1;
                continue;
            }
            if (!hasLast || pixel.r != lastPixel.r || pixel.g != lastPixel.g || pixel.b != lastPixel.b)
            {
                lastGem = NearestGem(pixel, match, toleranceSq);
                lastPixel = pixel;
                hasLast = true;
            }
            colourAt[i] = lastGem;
        }

        // Falls to nothing at the radius, squared so most of the light stays
        // close to the gem and the halo has no visible rim
        float radius = profile.Radius;
        int reach = Mathf.CeilToInt(radius);
        int side = reach * 2 + 1;
        var falloff = new float[side * side];
        for (int ky = -reach; ky <= reach; ky++)
        for (int kx = -reach; kx <= reach; kx++)
        {
            float f = Mathf.Clamp01(1f - Mathf.Sqrt(kx * kx + ky * ky) / radius);
            falloff[(ky + reach) * side + kx + reach] = f * f;
        }

        var halos = new List<Halo>();
        var claimed = new bool[width * height];
        var gemPixels = new List<int>();
        var frontier = new Stack<int>();

        for (int start = 0; start < colourAt.Length; start++)
        {
            if (colourAt[start] < 0 || claimed[start]) continue;

            // Gather one gem: every listed-colour pixel reachable from here in
            // hops of up to SameGemReach
            gemPixels.Clear();
            claimed[start] = true;
            frontier.Push(start);
            while (frontier.Count > 0)
            {
                int at = frontier.Pop();
                gemPixels.Add(at);
                int ax = at % width, ay = at / width;
                for (int ny = Mathf.Max(0, ay - SameGemReach); ny <= Mathf.Min(height - 1, ay + SameGemReach); ny++)
                for (int nx = Mathf.Max(0, ax - SameGemReach); nx <= Mathf.Min(width - 1, ax + SameGemReach); nx++)
                {
                    int n = ny * width + nx;
                    if (claimed[n] || colourAt[n] < 0) continue;
                    claimed[n] = true;
                    frontier.Push(n);
                }
            }

            halos.Add(PaintHalo(gemPixels, colourAt, light, width, reach, side, falloff, profile));
        }
        return halos;
    }

    private static Halo PaintHalo(List<int> gemPixels, int[] colourAt, Vector3[] light, int artWidth,
                                  int reach, int side, float[] falloff, GemGlowProfile profile)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (int p in gemPixels)
        {
            int px = p % artWidth, py = p / artWidth;
            minX = Mathf.Min(minX, px);
            maxX = Mathf.Max(maxX, px);
            minY = Mathf.Min(minY, py);
            maxY = Mathf.Max(maxY, py);
        }

        // The gem plus the halo's reach on every side. It may run past the edge
        // of the frame; that part is simply never on screen.
        int x0 = minX - reach, y0 = minY - reach;
        int width = maxX - minX + 1 + reach * 2;
        int height = maxY - minY + 1 + reach * 2;

        var sum = new Vector3[width * height];
        foreach (int p in gemPixels)
        {
            Vector3 colour = light[colourAt[p]];
            int cx = p % artWidth - x0, cy = p / artWidth - y0;
            for (int ky = 0; ky < side; ky++)
            {
                int row = (cy - reach + ky) * width + cx - reach;
                int krow = ky * side;
                for (int kx = 0; kx < side; kx++)
                    sum[row + kx] += colour * falloff[krow + kx];
            }
        }

        // Light keeps adding up, the glow does not: it eases toward full, so a
        // big stone reads brighter than a fleck without its brightest spot
        // blowing out
        float buildUp = profile.BuildUp;
        var pixels = new Color32[sum.Length];
        for (int i = 0; i < sum.Length; i++)
        {
            Vector3 s = sum[i];
            float peak = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (peak <= 0f) continue;

            float amount = 1f - Mathf.Exp(-peak * buildUp);
            pixels[i] = new Color32(
                (byte)Mathf.RoundToInt(s.x / peak * 255f),
                (byte)Mathf.RoundToInt(s.y / peak * 255f),
                (byte)Mathf.RoundToInt(s.z / peak * 255f),
                (byte)Mathf.RoundToInt(amount * 255f));
        }

        // Each gem's timing comes from where it is, not from a dice roll, so it
        // is the same on every run and moves only if the gem is repainted
        float variation = profile.EveryVariation;
        return new Halo
        {
            x = x0,
            y = y0,
            width = width,
            height = height,
            pixels = pixels,
            phase = Hash01(minX, minY, 0),
            everyScale = 1f + variation * (Hash01(minX, minY, 1) * 2f - 1f)
        };
    }

    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }

    private static int NearestGem(Color32 pixel, Color32[] gems, int toleranceSq)
    {
        int best = -1;
        int bestSq = toleranceSq;
        for (int g = 0; g < gems.Length; g++)
        {
            int dr = pixel.r - gems[g].r;
            int dg = pixel.g - gems[g].g;
            int db = pixel.b - gems[g].b;
            int sq = dr * dr + dg * dg + db * db;
            if (sq > bestSq) continue;
            best = g;
            bestSq = sq;
        }
        return best;
    }

    // Straight alpha-over, in the bytes the art is stored as. Close enough to
    // what the screen shows to tell a gem from the rock around it.
    private static void LayerOver(Color32[] below, Color32[] above)
    {
        for (int i = 0; i < below.Length; i++)
        {
            Color32 a = above[i];
            if (a.a == 0) continue;
            if (a.a == 255)
            {
                below[i] = a;
                continue;
            }
            float t = a.a / 255f;
            Color32 b = below[i];
            below[i] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(b.r, a.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(b.g, a.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(b.b, a.b, t)),
                (byte)Mathf.Max(a.a, b.a));
        }
    }

    // The sprite's pixels, bottom row first, as the sRGB bytes the art was
    // painted in. Blitting into an sRGB target re-encodes exactly what sampling
    // the texture decoded, so the bytes come back as they are in the .aseprite.
    private static Color32[] ReadArt(Sprite sprite)
    {
        Texture source = sprite.texture;
        Rect rect = sprite.textureRect;

        RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, target);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var copy = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height),
            TextureFormat.RGBA32, false, false);
        copy.ReadPixels(rect, 0, 0, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color32[] pixels = copy.GetPixels32();
        Discard(copy);
        return pixels;
    }

    private static void Discard(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
