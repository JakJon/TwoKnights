using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The camp as a place: the floor of wherever the last run got to, the two
// knights standing at their fire, the fire itself and the light it throws.
//
// Built in code, like the camp's panels are, so the Camp scene needs no wiring.
// CampfireMenu owns one of these and lays the menu's ring out around it.
//
// Draw order, bottom to top:
//   Background / 0    the stage's backdrop
//   Background / 40   the knights, under the ground pass so the fire lights
//                     them as fully as the floor they stand on (owner,
//                     2026-10-20: at the actor pass's fraction they stood out
//                     bright on top of the glow)
//   Background / 50   firelight, ground pass (full strength)
//   Background / 51   glow round the gems in the stage's art (GemGlow) — over
//                     the night shade, as it sits over the arena's shade
//   Background / 60   the menu's ring
//   Default / 1-99    knights, fire and props, nearer the bottom of the frame = in front
//   Default / 100     the stage's foreground, if it has one (the deep mine's
//                     rim, the forest canopy)
//   Default / 200     firelight, actor pass (a fraction of the ground pass)
//   Default / 210     sparks, which glow and so are not shaded
//
// Every sprite here is drawn with the plain sprite shader, not the lit one. The
// Camp scene has no 2D lights in it, and the firelight is the overlay above.
[DisallowMultipleComponent]
public class CampfireScene : MonoBehaviour
{
    /// <summary>
    /// The fire. The menu's ring turns about this point. Three art pixels below
    /// the middle of the frame: the items sit 4.55 units out, and that is the
    /// height at which the top one's title and the bottom one's art are the same
    /// distance from the edge of the screen.
    /// </summary>
    public static readonly Vector2 Center = new Vector2(0f, -3f / 32f);

    /// <summary>
    /// The band the menu items sit on, in world units from <see cref="Center"/>.
    /// It has no edge on either side. It thins in over <see cref="InnerFade"/>
    /// from <see cref="InnerRadius"/>, and from <see cref="FadeRadius"/> it
    /// thins out to nothing at <see cref="OuterRadius"/>. No circle is drawn
    /// round the knights (owner, 2026-10-13), and the frame is only 11.25 units
    /// tall, so a hard outer edge this far out would be cut off by the top and
    /// bottom of the screen.
    /// </summary>
    public const float InnerRadius = 3.2f;
    public const float InnerFade = 0.6f;
    public const float FadeRadius = 5.3f;
    public const float OuterRadius = 6f;

    public const float PixelsPerUnit = 32f;

    private const string BackgroundLayer = "Background";
    private const string ActorLayer = "Default";
    private const int KnightOrder = 40;
    private const int GroundLightOrder = 50;
    private const int WheelOrder = 60;
    private const int ForegroundOrder = 100;
    private const int ActorLightOrder = 200;
    private const int SparkOrder = 210;

    // Same framing as the arena: 20 x 11.25 world units
    private const float ViewHalfHeight = 5.625f;
    private const float Overscan = 1.2f;

    // The knights' feet, measured down from the fire's centre, and how far each
    // stands from it
    private const float GroundDrop = 0.45f;
    private const float KnightSpacing = 1.05f;

    // ---- the firelight
    private static readonly Color NightColor = new Color(0.02f, 0.025f, 0.055f, 1f);
    private static readonly Color WarmColor = new Color(1f, 0.6f, 0.26f, 1f);
    private static readonly Vector2 LightReach = new Vector2(9.8f, 7.6f);
    private const float LightCore = 0.2f;
    private const float NightStrength = 0.6f;
    // The orange the fire lays on the ground around it. Was 0.075; the owner
    // found the fire's glow too strong (2026-10-13). NightStrength, the dark at
    // the edge of the frame, is a separate number and was not touched.
    private const float WarmStrength = 0.03f;
    private const float GroundLightMaster = 1f;
    private const float ActorLightMaster = 0.4f;
    private const float FireFrameSeconds = 0.11f;

    private static readonly int FireCenterId = Shader.PropertyToID("_FireCenter");
    private static readonly int ReachId = Shader.PropertyToID("_Reach");
    private static readonly int CoreId = Shader.PropertyToID("_Core");
    private static readonly int FlickerId = Shader.PropertyToID("_Flicker");
    private static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
    private static readonly int ShadeStrengthId = Shader.PropertyToID("_ShadeStrength");
    private static readonly int WarmColorId = Shader.PropertyToID("_WarmColor");
    private static readonly int WarmStrengthId = Shader.PropertyToID("_WarmStrength");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int MasterId = Shader.PropertyToID("_Master");

    private CampfireKit _kit;
    private Camera _camera;

    private Material _spriteMaterial;
    private Material _lightMaterial;
    private Material _wheelMaterial;
    private Material _sparkMaterial;
    private Texture2D _quadTexture;
    private Sprite _quad;
    private MaterialPropertyBlock _block;

    private SpriteRenderer _backdrop;
    private SpriteRenderer _foreground;
    private GemGlow _gemGlow;
    private SpriteRenderer _fire;
    private SpriteRenderer _wheel;
    private float _nextFireFrame;
    private int _fireFrame;

    private List<KeyValuePair<MapDefinition, MapDefinition.MapStage>> _sites;
    private int _siteIndex = -1;

    /// <summary>The ring's material. CampfireMenu points the glow and sets the dividers through it.</summary>
    public Material WheelMaterial => _wheelMaterial;

    public Camera Camera => _camera;

    /// <summary>
    /// Builds the camp under <paramref name="parent"/>. Null when there is no
    /// camera to frame it or no plain sprite shader to draw it with — the caller
    /// falls back to the list menu.
    /// </summary>
    public static CampfireScene Create(CampfireKit kit, Transform parent, Camera camera)
    {
        if (kit == null || camera == null) return null;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            Debug.LogWarning("[CampfireScene] Sprites/Default is not in this build, so the camp cannot be drawn.");
            return null;
        }

        var holder = new GameObject("Campfire Scene");
        holder.transform.SetParent(parent, false);

        var scene = holder.AddComponent<CampfireScene>();
        scene._kit = kit;
        scene._camera = camera;
        scene.Build(spriteShader);
        return scene;
    }

    private void Build(Shader spriteShader)
    {
        _block = new MaterialPropertyBlock();
        _spriteMaterial = new Material(spriteShader) { name = "Camp Sprites", hideFlags = HideFlags.HideAndDontSave };

        // A one-unit quad for the overlays. Their shaders never sample it — they
        // want a surface to run on and work from world position.
        _quadTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "CampQuad",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        _quadTexture.SetPixel(0, 0, Color.white);
        _quadTexture.Apply();
        _quad = Sprite.Create(_quadTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _quad.name = "CampQuad";
        _quad.hideFlags = HideFlags.HideAndDontSave;

        FrameCamera();
        HideOldSceneDressing();

        _backdrop = NewRenderer("Backdrop", BackgroundLayer, 0);
        _foreground = NewRenderer("Backdrop Foreground", ActorLayer, ForegroundOrder);
        _foreground.enabled = false;
        _gemGlow = GemGlow.Build(_backdrop.transform);
        ShowSavedSite();

        BuildFirelight();
        BuildWheel();

        float feet = Center.y - GroundDrop;
        PlaceInFirelight(Place("Left Knight", _kit.LeftKnight, new Vector2(Center.x - KnightSpacing, feet)));
        PlaceInFirelight(Place("Right Knight", _kit.RightKnight, new Vector2(Center.x + KnightSpacing, feet)));

        // A few pixels lower than the knights' feet, so it sorts in front of them
        _fire = Place("Fire", _kit.FireFrames[0], new Vector2(Center.x, feet - 3f / PixelsPerUnit));
        BuildSparks(_fire.transform.position + new Vector3(0f, 0.42f, 0f));
    }

    private void OnDestroy()
    {
        if (_spriteMaterial != null) Destroy(_spriteMaterial);
        if (_lightMaterial != null) Destroy(_lightMaterial);
        if (_wheelMaterial != null) Destroy(_wheelMaterial);
        if (_sparkMaterial != null) Destroy(_sparkMaterial);
        if (_quad != null) Destroy(_quad);
        if (_quadTexture != null) Destroy(_quadTexture);
    }

    private void Update()
    {
        // Unscaled throughout: a run can end with time stopped, and the camp is
        // what the player lands on next
        float now = Time.unscaledTime;

        var frames = _kit.FireFrames;
        if (_fire != null && frames != null && frames.Length > 1 && now >= _nextFireFrame)
        {
            _nextFireFrame = now + FireFrameSeconds;
            _fireFrame = (_fireFrame + 1) % frames.Length;
            if (frames[_fireFrame] != null) _fire.sprite = frames[_fireFrame];
        }

        if (_lightMaterial != null)
        {
            // Two speeds laid over each other: a slow swell and a quick shiver.
            // One on its own reads as a light on a dimmer.
            float slow = Mathf.PerlinNoise(now * 2.3f, 0.37f) - 0.5f;
            float quick = Mathf.PerlinNoise(now * 8.9f, 4.2f) - 0.5f;
            _lightMaterial.SetFloat(FlickerId, 1f + slow * 0.09f + quick * 0.05f);
        }
    }

    // ------------------------------------------------------------------
    // Where the camp is
    // ------------------------------------------------------------------

    private void ShowSavedSite()
    {
        MapDefinition map;
        MapDefinition.MapStage stage;
        if (CampSite.Resolve(out map, out stage)) ShowStage(stage);
        else Debug.LogWarning("[CampfireScene] No map stage with a backdrop to pitch the camp on.");
    }

    public void ShowStage(MapDefinition.MapStage stage)
    {
        if (stage == null || _backdrop == null) return;

        // Both sprites are the same 640 x 360 canvas with the pivot at its bottom
        // middle, so both stand on the bottom edge of the frame
        Vector3 at = new Vector3(0f, -ViewHalfHeight, 0f);

        _backdrop.sprite = stage.backdrop;
        _backdrop.transform.position = at;

        _foreground.sprite = stage.foreground;
        _foreground.transform.position = at;
        _foreground.enabled = stage.foreground != null;

        _gemGlow.Apply(stage.gemGlow, stage.backdrop, stage.foreground);
    }

    /// <summary>
    /// Moves the camp to the next place it could be, without touching the save.
    /// For looking at every backdrop from the editor — CampfireMenu puts it on F7.
    /// </summary>
    public string ShowNextSite()
    {
        if (_sites == null) _sites = CampSite.AllSites();
        if (_sites.Count == 0) return null;

        _siteIndex = (_siteIndex + 1) % _sites.Count;
        var site = _sites[_siteIndex];
        ShowStage(site.Value);
        return site.Key.DisplayName + " / " + site.Value.label;
    }

    // ------------------------------------------------------------------
    // Putting things on the ground
    // ------------------------------------------------------------------

    /// <summary>
    /// Stands a sprite on the ground. Every .aseprite in the game imports with
    /// its pivot at the bottom middle of its canvas, so <paramref name="ground"/>
    /// is where that point lands. Things nearer the bottom of the frame draw in
    /// front.
    /// </summary>
    public SpriteRenderer Place(string objectName, Sprite sprite, Vector2 ground)
    {
        ground = Snap(ground);
        var renderer = NewRenderer(objectName, ActorLayer, OrderFor(ground.y));
        renderer.sprite = sprite;
        renderer.transform.position = new Vector3(ground.x, ground.y, 0f);
        return renderer;
    }

    // Down among the floor, beneath the full-strength ground pass, so the fire's
    // warmth lands on the knights exactly as it lands on the ground round them.
    // They still draw under the fire, which is on the actor layer; the ring's
    // band starts 3.2 units out and never reaches them.
    private static void PlaceInFirelight(SpriteRenderer renderer)
    {
        renderer.sortingLayerName = BackgroundLayer;
        renderer.sortingOrder = KnightOrder;
    }

    /// <summary>Onto the art's pixel grid, so nothing sits half a pixel off its neighbours.</summary>
    public static Vector2 Snap(Vector2 world)
    {
        return new Vector2(Mathf.Round(world.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(world.y * PixelsPerUnit) / PixelsPerUnit);
    }

    private static int OrderFor(float groundY)
    {
        return Mathf.Clamp(50 - Mathf.RoundToInt(groundY * 6f), 1, ForegroundOrder - 1);
    }

    private SpriteRenderer NewRenderer(string objectName, string sortingLayer, int sortingOrder)
    {
        var holder = new GameObject(objectName);
        holder.transform.SetParent(transform, false);

        var renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = _spriteMaterial;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    // ------------------------------------------------------------------
    // Camera and the old canvas
    // ------------------------------------------------------------------

    // The Camp scene's camera was only ever asked to clear the screen behind a
    // full-screen menu, and was left on perspective. The camp is drawn in the
    // world now, so it is framed exactly as the arena is.
    private void FrameCamera()
    {
        _camera.orthographic = true;
        _camera.orthographicSize = ViewHalfHeight;
        _camera.backgroundColor = Color.black;
        _camera.transform.position = new Vector3(0f, 0f, -10f);
        _camera.transform.rotation = Quaternion.identity;
    }

    // The Camp scene still carries two things from before its menu was a UI
    // document: Background_Camp, the old camp picture with the pink sky, stretched
    // to fill the camera, and the TMP canvas the side status panels replaced.
    // Neither ever showed, because the list menu paints the whole screen over
    // them. This camp is see-through, so both are switched off — the pink sky
    // must never be seen (owner, 2026-10-13). Everything in the scene that draws
    // and is not part of this camp goes; the scene itself is left as it is, so
    // the list style still finds it exactly as before.
    private void HideOldSceneDressing()
    {
        GameObject mine = transform.root.gameObject;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root == mine) continue;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
    }

    // ------------------------------------------------------------------
    // Firelight
    // ------------------------------------------------------------------

    private void BuildFirelight()
    {
        _lightMaterial = new Material(_kit.FirelightShader) { name = "Camp Firelight", hideFlags = HideFlags.HideAndDontSave };
        _lightMaterial.SetVector(FireCenterId, Center);
        _lightMaterial.SetVector(ReachId, LightReach);
        _lightMaterial.SetFloat(CoreId, LightCore);
        _lightMaterial.SetFloat(FlickerId, 1f);
        _lightMaterial.SetColor(ShadeColorId, ToRenderSpace(NightColor));
        _lightMaterial.SetFloat(ShadeStrengthId, NightStrength);
        _lightMaterial.SetColor(WarmColorId, ToRenderSpace(WarmColor));
        _lightMaterial.SetFloat(WarmStrengthId, WarmStrength);
        _lightMaterial.SetFloat(PixelSizeId, 1f / PixelsPerUnit);

        BuildLightPass("Firelight Ground", BackgroundLayer, GroundLightOrder, GroundLightMaster);
        BuildLightPass("Firelight Actors", ActorLayer, ActorLightOrder, ActorLightMaster);
    }

    private void BuildLightPass(string objectName, string sortingLayer, int sortingOrder, float master)
    {
        var pass = NewRenderer(objectName, sortingLayer, sortingOrder);
        pass.sprite = _quad;
        pass.sharedMaterial = _lightMaterial;

        // Sized to the view with a margin, so a window that is not 16:9 can
        // never show an unlit strip
        float height = ViewHalfHeight * 2f * Overscan;
        float width = height * Mathf.Max(_camera.aspect, 16f / 9f);
        pass.transform.position = new Vector3(0f, 0f, 0f);
        pass.transform.localScale = new Vector3(width, height, 1f);

        pass.GetPropertyBlock(_block);
        _block.SetFloat(MasterId, master);
        pass.SetPropertyBlock(_block);
    }

    private void BuildWheel()
    {
        _wheelMaterial = new Material(_kit.WheelShader) { name = "Camp Wheel", hideFlags = HideFlags.HideAndDontSave };
        _wheelMaterial.SetFloat(PixelSizeId, 1f / PixelsPerUnit);

        _wheel = NewRenderer("Menu Ring", BackgroundLayer, WheelOrder);
        _wheel.sprite = _quad;
        _wheel.sharedMaterial = _wheelMaterial;

        float size = OuterRadius * 2f + 1f;
        _wheel.transform.position = new Vector3(Center.x, Center.y, 0f);
        _wheel.transform.localScale = new Vector3(size, size, 1f);
    }

    // ------------------------------------------------------------------
    // Sparks
    // ------------------------------------------------------------------

    // The fire's body is the drawn frames. This is what comes off it: a soft
    // lick of flame over the top and sparks carried up on the heat. Built the
    // way FireFx builds its embers, off the same white mote.
    private void BuildSparks(Vector3 at)
    {
        _sparkMaterial = new Material(_spriteMaterial) { name = "Camp Sparks", hideFlags = HideFlags.HideAndDontSave };
        Sprite mote = FireResourceManager.Instance != null ? FireResourceManager.Instance.GetEmberMoteSprite() : null;
        if (mote != null) _sparkMaterial.mainTexture = mote.texture;

        // ---- flame licks: big, soft, short-lived
        var licks = NewParticles("Fire Licks", at + new Vector3(0f, -0.12f, 0f));
        var licksMain = licks.main;
        licksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        licksMain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
        licksMain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        licksMain.startColor = new Color(1f, 0.55f, 0.18f, 0.22f);
        licksMain.maxParticles = 40;

        var licksEmission = licks.emission;
        licksEmission.rateOverTime = 16f;

        var licksShape = licks.shape;
        licksShape.shapeType = ParticleSystemShapeType.Box;
        licksShape.scale = new Vector3(0.34f, 0.08f, 0.01f);

        var licksVelocity = licks.velocityOverLifetime;
        licksVelocity.enabled = true;
        licksVelocity.space = ParticleSystemSimulationSpace.World;
        licksVelocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        licksVelocity.y = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        licksVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        licks.Play();

        // ---- sparks: small, few, and they travel
        var sparks = NewParticles("Fire Sparks", at);
        var sparksMain = sparks.main;
        sparksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
        sparksMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        sparksMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        sparksMain.startColor = new Color(1f, 0.72f, 0.3f, 1f);
        sparksMain.maxParticles = 40;

        var sparksEmission = sparks.emission;
        sparksEmission.rateOverTime = 7f;

        var sparksShape = sparks.shape;
        sparksShape.shapeType = ParticleSystemShapeType.Circle;
        sparksShape.radius = 0.16f;

        var sparksVelocity = sparks.velocityOverLifetime;
        sparksVelocity.enabled = true;
        sparksVelocity.space = ParticleSystemSimulationSpace.World;
        sparksVelocity.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
        sparksVelocity.y = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        sparksVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // A little wander, so they do not climb in straight lines
        var sparksNoise = sparks.noise;
        sparksNoise.enabled = true;
        sparksNoise.strength = 0.12f;
        sparksNoise.frequency = 0.9f;
        sparks.Play();
    }

    private ParticleSystem NewParticles(string objectName, Vector3 at)
    {
        var holder = new GameObject(objectName);
        holder.transform.SetParent(transform, false);
        holder.transform.position = at;

        var system = holder.AddComponent<ParticleSystem>();
        // AddComponent starts it playing with the defaults; stop it so the
        // settings below are what it starts with
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.playOnAwake = false;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Hot core cooling to ash, the same ramp FireFx uses
        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.93f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _sparkMaterial;
        renderer.sortingLayerName = ActorLayer;
        renderer.sortingOrder = SparkOrder;
        return system;
    }

    // Inspector-style colours are sRGB. The project renders Linear and both
    // shaders do arithmetic with these, so they are handed over in the space
    // that arithmetic happens in — the same step CanopyProfile takes.
    public static Color ToRenderSpace(Color c)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
