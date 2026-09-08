using UnityEngine;
using UnityEngine.Rendering.Universal;

// Lays a map stage's CanopyProfile over the arena as light and shade.
//
// It draws the canopy TWICE, and that is the whole idea rather than an
// optimisation gone wrong:
//
//   * the GROUND pass sits on the Background sorting layer, just above the
//     backdrop. It shades only the forest floor, so it can run at full strength
//     — deep shade at the frame's edge, leaf shadows crawling across the glade.
//   * the ACTOR pass sits above everything on Default. It is the same pattern at
//     a fraction of the strength, so knights and enemies pick up the same
//     shadows the ground does and sit IN the scene instead of on top of it,
//     without the dark edge of the frame ever swallowing an enemy walking in.
//
// Both passes share one material and differ only by _Master on a property block.
// Because the shader works from world position, the pattern is pinned to the
// arena while the quads themselves just have to cover the viewport.
//
// Nothing in the scene needs wiring: BackgroundController calls Ensure() when it
// applies a stage, exactly as it builds its own foreground renderer. A stage
// with no profile switches both passes off.
[DisallowMultipleComponent]
public class ArenaLighting : MonoBehaviour
{
    // Above the backdrop (Background/0) and any scenery on that layer, still
    // under everything on Default
    private const string GroundSortingLayer = "Background";
    private const int GroundSortingOrder = 50;

    // Over the whole arena. Clear of the highest order the game hands out — the
    // bomb blast at 25, prefabs at 12, BackgroundController's foreground at 100 —
    // because the canopy is the roof of the world and everything below is under it.
    private const string ActorSortingLayer = "Default";
    private const int ActorSortingOrder = 200;

    // The quads only have to cover the viewport; a margin keeps them covering it
    // through an aspect change without a resize landing a frame late
    private const float Overscan = 1.2f;
    private const float PlaneZ = -1f;

    // Fallback for a scene without a PixelPerfectCamera. Matches the project's
    // import default (spritePixelsToUnits: 32).
    private const float FallbackPixelsPerUnit = 32f;

    private static readonly int MasterId = Shader.PropertyToID("_Master");

    public static ArenaLighting Instance { get; private set; }

    private Camera _camera;
    private SpriteRenderer _ground;
    private SpriteRenderer _actors;
    private MaterialPropertyBlock _block;
    private Material _material;
    private Sprite _quad;
    private Texture2D _quadTexture;

    private CanopyProfile _profile;
    private Light2D _globalLight;
    private bool _lightSearched;
    private float _baseIntensity = 1f;
    private float _pixelSize = 1f / FallbackPixelsPerUnit;
    private float _fittedHeight = -1f;
    private float _fittedAspect = -1f;

    /// <summary>
    /// The arena's lighting rig, built on first use. Safe to call every frame.
    /// </summary>
    public static ArenaLighting Ensure()
    {
        if (Instance != null) return Instance;

        var holder = new GameObject("ArenaLighting");
        Instance = holder.AddComponent<ArenaLighting>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _block = new MaterialPropertyBlock();
        _quadTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "CanopyQuad",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        _quadTexture.SetPixel(0, 0, Color.white);
        _quadTexture.Apply();

        // A one-unit quad the shader never actually samples — it wants a surface
        // to run on, and the pattern comes entirely from world position
        _quad = Sprite.Create(_quadTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _quad.name = "CanopyQuad";
        _quad.hideFlags = HideFlags.HideAndDontSave;

        _ground = BuildPass("CanopyGround", GroundSortingLayer, GroundSortingOrder);
        _actors = BuildPass("CanopyActors", ActorSortingLayer, ActorSortingOrder);

        ResolveCamera();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_material != null) Destroy(_material);
        if (_quad != null) Destroy(_quad);
        if (_quadTexture != null) Destroy(_quadTexture);
    }

    /// <summary>
    /// Switches the arena over to a stage's canopy. Null — a stage that declares
    /// no lighting, or a map that has none at all — switches the overlay off.
    /// Cheap to call every frame; only a real change does any work.
    /// </summary>
    public void Apply(CanopyProfile profile)
    {
        if (profile == _profile) return;
        _profile = profile;

        bool on = profile != null && profile.Shader != null;
        if (profile != null && profile.Shader == null)
            Debug.LogWarning($"[ArenaLighting] {profile.name} has no shader assigned, so it cannot draw. " +
                             "Assign Custom/CanopyLight to its Shader field.");

        _ground.enabled = on;
        _actors.enabled = on;
        if (!on) return;

        if (_material == null || _material.shader != profile.Shader)
        {
            if (_material != null) Destroy(_material);
            _material = new Material(profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
            _ground.sharedMaterial = _material;
            _actors.sharedMaterial = _material;
        }

        profile.PushTo(_material, _pixelSize);
    }

    void LateUpdate()
    {
        if (_profile == null || _material == null) return;

        FitToCamera();

        // Ride the global light down with the venture curtain. The overlay is
        // unlit — without this its highlights would hang in the frame, glowing,
        // while the arena underneath them goes to black between waves.
        float dim = GlobalLightScale();
        SetMaster(_ground, _profile.GroundStrength * dim);
        SetMaster(_actors, _profile.ActorStrength * dim);
    }

    private SpriteRenderer BuildPass(string name, string sortingLayer, int sortingOrder)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(transform, false);

        var renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sprite = _quad;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    private void SetMaster(SpriteRenderer renderer, float master)
    {
        renderer.GetPropertyBlock(_block);
        _block.SetFloat(MasterId, master);
        renderer.SetPropertyBlock(_block);
    }

    // The quads are sized to the viewport rather than to the backdrop, so a
    // letterboxed or resized window can never show an unlit strip at the edge
    private void FitToCamera()
    {
        if (_camera == null && !ResolveCamera()) return;

        float height = _camera.orthographicSize * 2f;
        float aspect = _camera.aspect;

        Vector3 camPos = _camera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, PlaneZ);

        if (Mathf.Approximately(height, _fittedHeight) && Mathf.Approximately(aspect, _fittedAspect)) return;
        _fittedHeight = height;
        _fittedAspect = aspect;

        var scale = new Vector3(height * aspect * Overscan, height * Overscan, 1f);
        _ground.transform.localScale = scale;
        _actors.transform.localScale = scale;
    }

    private bool ResolveCamera()
    {
        _camera = Camera.main;
        if (_camera == null) return false;

        // The art's pixel size is what the shader snaps every gradient to, so take
        // it from the camera that defines it rather than from a number a profile
        // could get wrong
        var pixelPerfect = _camera.GetComponent<PixelPerfectCamera>();
        float ppu = pixelPerfect != null && pixelPerfect.assetsPPU > 0
            ? pixelPerfect.assetsPPU
            : FallbackPixelsPerUnit;
        _pixelSize = 1f / ppu;
        return true;
    }

    // 1 while the arena is lit, 0 once the curtain has taken the light out. The
    // base is re-learned as a running maximum: nothing ever drives the light
    // ABOVE its authored intensity, so if this rig happens to be built while a
    // transition is already under way, the first lit frame corrects it.
    private float GlobalLightScale()
    {
        if (_globalLight == null)
        {
            // Searched once and only once. A scene with no global light must not
            // pay for a FindObjectsByType sweep on every frame it renders.
            if (_lightSearched) return 1f;
            _lightSearched = true;

            var lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                _globalLight = light;
                _baseIntensity = light.intensity;
                break;
            }
            // No global light in this scene: nothing is dimming the arena, so
            // the canopy plays at full strength
            if (_globalLight == null) return 1f;
        }

        _baseIntensity = Mathf.Max(_baseIntensity, _globalLight.intensity);
        if (_baseIntensity <= 0f) return 0f;
        return Mathf.Clamp01(_globalLight.intensity / _baseIntensity);
    }
}
