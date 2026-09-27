using UnityEngine;

// Code-built Dawn visuals: the holy light. Mirrors FireFx / ShadowFx — no
// prefab and no new art, just the shared white ember mote tinted to the Order's
// gold, so Dawn reads as light rather than as fire.
//
// Three surfaces, one look:
//   Blessing  — a one-shot ring of motes ASCENDING around a knight, fired
//               whenever a Dawn knight receives healing.
//   Aura      — a sustained halo for as long as a knight is untouchable.
//   OrbGlow   — a slowed orb emanating light, so Sunwell III is visible on the
//               object it changed rather than only in the numbers.
//   HolyOrb   — a single steady orb riding behind a shot carrying holy damage.
//               The one surface here that is a SPRITE rather than motes; see
//               AttachArrowTrail for why.
//
// Everything rises. Fire licks up and dies dark (FireFx cools to ash); Dawn
// rises and dies BRIGHT, fading to white rather than to soot. That contrast is
// the whole reason the two Orders don't look alike despite sharing a sprite.
public static class DawnFx
{
    // The Order's accent, matching order--dawn in UpgradeMenu.uss
    private static readonly Color Gold = new Color(240f / 255f, 200f / 255f, 170f / 255f);
    private static readonly Color HolyWhite = new Color(1f, 0.98f, 0.90f);

    private const string AuraChildName = "DawnAura";
    private const string OrbGlowChildName = "DawnOrbGlow";
    private const string HolyOrbChildName = "HolyOrb";

    // Finished diameter in WORLD units, so the orb is the same size behind a
    // full-scale shadow arrow and a half-scale arrow.
    //
    // The orb used to ride 0.31 world units BEHIND the shot as well, which is what
    // made it read as a trail dragging along after the arrow instead of as light
    // coming off it. It sits on the sprite's own centre now (see AttachArrowTrail),
    // and at twice the old diameter, because a glow the size of the arrowhead reads
    // as a second object next to the arrow rather than as the arrow glowing.
    //
    // This pair is the ONE-PICK glow — the arrow of a knight who has dipped a
    // single toe into the Order. It is the baseline the growth below builds on.
    private const float HolyOrbWorldDiameter = 0.8f;

    // Peak opacity at the very centre. Everything outside that is thinner again by
    // the falloff below, so the orb never presents an edge.
    //
    // A THIRD of the 0.38 this used to be (owner's call). That number was set when
    // the glow only ever meant "this arrow carries holy" and had one setting; as
    // the floor of a ramp it was far too loud, because a single Dawn pick is +2 on
    // the arrow and was lighting the shot up as if the knight had bought the Order.
    // The first pick should be a hint that something is on the arrow, and the last
    // one should be the arrow burning.
    private const float HolyOrbAlpha = 0.127f;

    // ---- how the light grows ----
    //
    // Holy damage is +2 an arrow for EVERY Dawn pick (see DawnBoost.HolyDamage), so
    // by the end of the Order the arrow is carrying twenty-odd points of light it
    // was carrying two of at the start — and it looked identical the whole way. The
    // glow says how far down the Order the knight has gone, which is the one Dawn
    // number the player has no other way to read off the screen.
    //
    // Both ends grow, because either alone reads wrong: alpha alone turns a soft
    // glow into a hard white pill, and size alone into a bigger patch of the same
    // faint haze. Together they read as the light getting STRONGER.
    //
    // The alpha spans nearly six to one now that the floor has come down to a
    // third, which is the point: the difference between one Dawn card and thirteen
    // should be obvious across a crowded board without anyone counting anything.
    private const int HolyOrbFullPicks = 13;        // the whole Order, per DawnBoost
    private const float HolyOrbFullAlpha = 0.72f;
    private const float HolyOrbFullDiameter = 1.35f;

    // How fast the glow thins from centre to rim. Above 1 the light collapses
    // toward the middle, which is what makes it read as a glow rather than a disc.
    private const float HolyOrbFalloff = 2.4f;

    private const int HolyOrbTextureSize = 64;

    // The orb's own sprite, generated once and kept.
    //
    // It deliberately does NOT use the shared ember mote the rest of DawnFx draws
    // with. That sprite is a 16x16 blob with a near-solid five-pixel core, which is
    // right for a particle seen for a third of a second and wrong for something
    // that sits on screen for a whole flight — blown up to this size it reads as a
    // hard disc with a fuzzy rim. A radial falloff computed here has no edge at any
    // size, which is the whole requirement.
    private static Sprite _holyOrbSprite;

    private static Sprite HolyOrbSprite
    {
        get
        {
            if (_holyOrbSprite != null) return _holyOrbSprite;

            const int size = HolyOrbTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float centre = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), HolyOrbFalloff);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Pixels-per-unit equal to the texture size, so the sprite is exactly
            // one world unit across at scale 1 and the sizing in AttachArrowTrail
            // stays arithmetic anyone can follow.
            _holyOrbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                                           new Vector2(0.5f, 0.5f), size);
            _holyOrbSprite.hideFlags = HideFlags.HideAndDontSave;
            return _holyOrbSprite;
        }
    }

    private static Sprite MoteSprite
    {
        get
        {
            // The ember mote is a white alpha mask authored for exactly this —
            // code-tinting, not a fire-specific sprite. Poison puff is the
            // fallback, same as FireFx uses.
            Sprite mote = FireResourceManager.Instance != null
                ? FireResourceManager.Instance.GetEmberMoteSprite()
                : null;
            if (mote == null && PoisonResourceManager.Instance != null)
            {
                mote = PoisonResourceManager.Instance.GetPoisonPuffSprite();
            }
            return mote;
        }
    }

    /// <summary>
    /// One-shot ring of rising motes around a knight — the Dawn "something good
    /// just happened to you" beat. Fired from PlayerHealth whenever a knight
    /// carrying any Dawn upgrade is healed, so no individual Dawn effect has to
    /// remember to ask for it.
    /// </summary>
    public static void Blessing(GameObject knight)
    {
        if (knight == null) return;

        var host = new GameObject("DawnBlessing");
        host.transform.position = CenterOf(knight);

        var system = Build(host, 0.95f);

        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        // Barely any outward push — the rise below is what carries them
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);

        // Emitted from the RIM of a circle, so the light reads as a halo forming
        // around the knight rather than a puff at their middle
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = RadiusOf(knight);
        shape.radiusThickness = 0f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
        system.Emit(22);
        Object.Destroy(host, 1.5f);
    }

    /// <summary>
    /// A halo held for as long as the knight is untouchable. Hooked into
    /// PlayerHealth.SetInvulnerable, which is the game's single untouchable
    /// funnel — so Second Wind, Benediction II, Last Light and Iron Vigil all
    /// look the same, because to the player they ARE the same thing.
    ///
    /// Replaces any aura already running rather than stacking: overlapping
    /// windows EXTEND the deadline, so the caller passes the new remaining time
    /// and gets one halo of the right length.
    /// </summary>
    public static void ShowInvulnerableAura(GameObject knight, float seconds)
    {
        if (knight == null || seconds <= 0f) return;

        ClearChild(knight, AuraChildName);

        var host = new GameObject(AuraChildName);
        host.transform.SetParent(knight.transform);
        host.transform.position = CenterOf(knight);

        var system = Build(host, 0.7f);

        var main = system.main;
        // Local space so the halo travels with the knight; Hierarchy scaling so
        // it stays world-sized whatever the knight's transform scale is (the
        // lesson FireFx learned on scaled slimes)
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        // Emit for exactly the window, then let the last motes live out and stop
        main.duration = seconds;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);

        var emission = system.emission;
        emission.rateOverTime = 30f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = RadiusOf(knight);
        shape.radiusThickness = 0f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
        Object.Destroy(host, seconds + 1.2f);
    }

    /// <summary>
    /// Sunwell III: a slowed orb emanates light. Parented to the orb, so it dies
    /// with it on collection or exit — the orb is the thing that changed, so the
    /// orb is what has to look different.
    /// </summary>
    public static void AttachOrbGlow(GameObject orb)
    {
        if (orb == null) return;

        ClearChild(orb, OrbGlowChildName);

        var host = new GameObject(OrbGlowChildName);
        host.transform.SetParent(orb.transform);
        host.transform.localPosition = Vector3.zero;

        var system = Build(host, 0.8f);

        var main = system.main;
        // World space: the motes should hang in the air as the orb drifts past,
        // leaving a wake of light rather than a rigid halo travelling with it
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);

        var emission = system.emission;
        emission.rateOverTime = 26f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f; // fill, not rim — the orb IS the light source

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
    }

    /// <summary>
    /// The tell for holy damage: a single glowing orb riding directly behind the
    /// shot. Deliberately NOT a particle trail — Dawn's other surfaces all throw
    /// motes, and a bow firing three times a second turned the screen into drifting
    /// bubbles. One steady orb reads as a shot that is carrying something.
    ///
    /// A sprite rather than a ParticleSystem, because the whole point is that it
    /// does not move relative to the arrow: parented, offset along local -X (the
    /// arrow sprite is drawn pointing along +X), and it turns with the shot for
    /// free when Guided Shot steers it.
    /// </summary>
    /// <param name="dawnPicks">How many Dawn upgrades the firing knight owns. One
    /// (or anything less) draws the baseline glow; every pick past that brightens
    /// and widens it toward the full-Order light. Callers pass DawnBoost.DawnPicks.</param>
    public static GameObject AttachArrowTrail(GameObject arrow, int dawnPicks = 1)
    {
        if (arrow == null) return null;

        Sprite orb = HolyOrbSprite;
        if (orb == null) return null;

        // Linear between the one-pick baseline and the full-Order light. Linear
        // rather than curved because the thing being reported is linear: each pick
        // is worth the same +2, so each pick should look like the same step.
        float growth = Mathf.Clamp01((dawnPicks - 1f) / (HolyOrbFullPicks - 1f));
        float alpha = Mathf.Lerp(HolyOrbAlpha, HolyOrbFullAlpha, growth);
        float diameter = Mathf.Lerp(HolyOrbWorldDiameter, HolyOrbFullDiameter, growth);

        ClearChild(arrow, HolyOrbChildName);

        // Read BEFORE the orb exists. GetComponentInChildren searches the object
        // itself first, but on a prefab whose renderer sits on a CHILD it walks the
        // children in order — and the orb, once parented, is one of them.
        var arrowRenderer = arrow.GetComponentInChildren<SpriteRenderer>();

        var host = new GameObject(HolyOrbChildName);
        host.transform.SetParent(arrow.transform, false);

        // Dead centre of the SPRITE, which is not the same point as the transform
        // origin: the arrow prefabs are pivoted at the tail so they rotate about
        // the nock, so parking the orb at localPosition zero would still hang it
        // off one end. Set in world space off the renderer's own bounds and left
        // to the parenting to hold, which lands it on the middle of the art for
        // an arrow, a shuriken and a fireball alike whatever each is pivoted on.
        float parentScale = Mathf.Max(0.01f, Mathf.Abs(arrow.transform.lossyScale.x));
        if (arrowRenderer != null) host.transform.position = arrowRenderer.bounds.center;
        else host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.identity;

        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = orb;
        renderer.color = new Color(Gold.r, Gold.g, Gold.b, alpha);

        // Sit just under the arrow so the shot itself stays the readable thing,
        // and share its layer so the orb can never end up on the wrong side of
        // the scenery the arrow is flying over
        if (arrowRenderer != null)
        {
            renderer.sortingLayerID = arrowRenderer.sortingLayerID;
            renderer.sortingOrder = arrowRenderer.sortingOrder - 1;
        }

        // Sized in WORLD units against the parent's scale, because the arrow
        // prefab is authored at 0.5 and a child would otherwise inherit it
        float spriteWorld = Mathf.Max(0.01f, orb.bounds.size.x);
        float scale = diameter / (spriteWorld * parentScale);
        host.transform.localScale = new Vector3(scale, scale, 1f);

        return host;
    }

    // ---- shared ----

    private static Vector3 CenterOf(GameObject target)
    {
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds.center : target.transform.position;
    }

    // Halo radius from the sprite, so a knight and anything larger both get a
    // ring that sits just off the body instead of buried inside it
    private static float RadiusOf(GameObject target)
    {
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return 0.5f;
        Bounds b = sr.bounds;
        return Mathf.Clamp(Mathf.Max(b.size.x, b.size.y) * 0.55f, 0.35f, 2f);
    }

    private static void ClearChild(GameObject parent, string childName)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null) Object.Destroy(existing.gameObject);
    }

    // Shared builder: white mote on the default sprite shader, holy-white core
    // warming to the Order's gold and fading out BRIGHT — the deliberate inverse
    // of FireFx, which cools to ash.
    private static ParticleSystem Build(GameObject host, float startAlpha)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(Gold.r, Gold.g, Gold.b, startAlpha);
        main.maxParticles = 120;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        // Motes swell slightly then thin away, so the light feels like it is
        // being given off rather than thrown
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(0.35f, 1f);
        sizeCurve.AddKey(1f, 0.15f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(HolyWhite, 0f),
                new GradientColorKey(Gold, 0.55f),
                new GradientColorKey(HolyWhite, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.75f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            Sprite mote = MoteSprite;
            if (mote != null)
            {
                material.mainTexture = mote.texture;
            }
            renderer.material = material;
            renderer.sortingOrder = 20; // over the knight / orb sprite
        }

        return system;
    }
}
