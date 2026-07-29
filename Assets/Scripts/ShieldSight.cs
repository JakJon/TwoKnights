using UnityEngine;

// Bowsight: a dashed line thrown downrange from the shield along the firing axis, so a
// knight can read where the next arrow goes before committing to the shot. Purely an
// aiming aid — no collider, no damage, nothing to block.
//
// Lives on the shield GameObject and parents its own child renderer, so it inherits the
// shield's rotation and orbit for free. Deliberately unlit: a sight that vanishes in a
// dark map is no sight at all.
public class ShieldSight : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const int DashPixels = 4;
    private const int GapPixels = 4;
    private const int ThicknessPixels = 1;
    // Clear of the shield's own face (1 px outboard offset + 3 px half-thickness)
    private const int StartOffsetPixels = 8;

    private static readonly Color32 SightColor = new Color32(212, 162, 74, 190);
    private static readonly Color32 TransparentColor = new Color32(0, 0, 0, 0);

    private SpriteRenderer _renderer;
    private Texture2D _texture;
    private Sprite _sprite;
    private float _rangeUnits;

    public float Range => _rangeUnits;

    // Absolute, like the shield-shape setters: a tier states the reach it wants rather
    // than adding to whatever came before.
    public void SetRange(float worldUnits)
    {
        _rangeUnits = Mathf.Max(0f, worldUnits);
        Rebuild();
    }

    private void Rebuild()
    {
        EnsureRenderer();
        if (_renderer == null) return;

        if (_rangeUnits <= 0f)
        {
            _renderer.enabled = false;
            return;
        }

        // The child inherits the shield's scale, so a sight pixel is the same size on
        // screen as a shield pixel — convert the designed world reach into that grid.
        float worldPerPixel = transform.lossyScale.x / PixelsPerUnit;
        int length = Mathf.Max(1, Mathf.RoundToInt(_rangeUnits / Mathf.Max(worldPerPixel, 0.0001f)));

        var pixels = new Color32[length * ThicknessPixels];
        for (int x = 0; x < length; x++)
        {
            bool lit = x % (DashPixels + GapPixels) < DashPixels;
            for (int y = 0; y < ThicknessPixels; y++)
                pixels[y * length + x] = lit ? SightColor : TransparentColor;
        }

        DisposeGenerated();

        _texture = new Texture2D(length, ThicknessPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _texture.SetPixels32(pixels);
        _texture.Apply();

        // Pivot on the near end so the line grows outward and its root stays put
        _sprite = Sprite.Create(_texture, new Rect(0f, 0f, length, ThicknessPixels),
            new Vector2(0f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        _renderer.sprite = _sprite;
        _renderer.enabled = true;
    }

    private void EnsureRenderer()
    {
        if (_renderer != null) return;

        var shieldRenderer = GetComponent<SpriteRenderer>();

        var host = new GameObject("Sight");
        host.transform.SetParent(transform, false);
        host.transform.localPosition = new Vector3(StartOffsetPixels / PixelsPerUnit, 0f, 0f);

        _renderer = host.AddComponent<SpriteRenderer>();
        if (shieldRenderer != null)
        {
            _renderer.sortingLayerID = shieldRenderer.sortingLayerID;
            _renderer.sortingOrder = shieldRenderer.sortingOrder - 1; // behind the shield
        }

        // Sprites/Default is in the project's always-included shaders, so this survives
        // a build; fall back to the shield's own material if it ever isn't.
        var unlit = Shader.Find("Sprites/Default");
        if (unlit != null)
            _renderer.material = new Material(unlit);
        else if (shieldRenderer != null)
            _renderer.sharedMaterial = shieldRenderer.sharedMaterial;
    }

    private void DisposeGenerated()
    {
        if (_sprite != null) Destroy(_sprite);
        if (_texture != null) Destroy(_texture);
        _sprite = null;
        _texture = null;
    }

    private void OnDestroy()
    {
        DisposeGenerated();
    }
}
