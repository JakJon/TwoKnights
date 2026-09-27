using UnityEngine;

/// <summary>
/// The particles the quest NPCs arrive and leave in.
///
/// Same shape as every other Fx file in the project: a static class, one shared
/// builder, and the tintable white mote that FireResourceManager holds. There is not
/// a single ParticleSystem component in any prefab or scene and this does not add
/// one.
///
/// Deliberately NOT a port of the upgrade menu's order auras. Those are UI Toolkit
/// VisualElements sized in pixels against a 104x84 portrait; in the arena they would
/// have to be reinvented anyway, and particles are what the rest of the world is
/// built from.
/// </summary>
public static class NpcFx
{
    // Sorting: NPCs draw above the arena, and their effects BRACKET them — the
    // smoke that hides the ninja in front, the paladin's light behind.
    //
    // These have to straddle the order NpcActor gives the figure itself, which is
    // what NpcOrder is. They did not: "behind" was 598 against a figure at 550, so
    // the light meant to be standing behind the paladin covered him instead. Four
    // of the five NPCs import as ONE merged renderer sitting exactly on NpcOrder;
    // only the King is a SortingGroup, whose children sort inside the group and
    // are unaffected by any of this.
    public const int NpcOrder = 550;
    public const int BehindOrder = NpcOrder - 10;
    // Clear of the King's layer stack, which counts up from NpcOrder.
    public const int FrontOrder = NpcOrder + 52;

    private static Sprite Mote
    {
        get
        {
            Sprite mote = FireResourceManager.Instance != null
                ? FireResourceManager.Instance.GetEmberMoteSprite() : null;
            if (mote == null && PoisonResourceManager.Instance != null)
                mote = PoisonResourceManager.Instance.GetPoisonPuffSprite();
            return mote;
        }
    }

    // The cloud, measured against the figure it has to hide rather than against a
    // radius. The ninja's origin is at his FEET and he stands 1.6 units (32x32 at
    // 32 PPU, worldScale 1.6), so a circle centred on the position he was handed
    // put half the puff underground and left his head in clear air — which is
    // exactly what "it goes too low and never reaches his head" looks like.
    //
    // A box rather than a circle because the thing being covered is a standing
    // figure, and the emitter is lifted to the middle of him: 1.5 tall centred
    // 0.65 up spans -0.1 to 1.4, and the puffs themselves (half a unit, growing)
    // carry the rest over his head.
    private const float CloudWidth = 1.3f;
    private const float CloudHeight = 1.5f;
    private const float CloudCentreY = 0.65f;
    // What the radius argument is measured against, so the one caller's default
    // gives exactly the figures above and anything else scales off them.
    private const float BaseRadius = 1.1f;

    /// <summary>
    /// The ninja's smoke: dark grey, growing as it lifts, dense enough to hide a
    /// 32x32 sprite for the beat he appears or vanishes behind it.
    ///
    /// Lifted from BombFx.SpawnSmoke, which had already solved "read as smoke" —
    /// the growth curve is most of what does it, since every other effect in the
    /// game shrinks. The speeds are NOT its: a bomb wants its smoke thrown outward
    /// and this wants a cloud that stays where it was put for a beat, and the
    /// blast's speeds pulled the puff apart before it had covered anyone.
    ///
    /// <paramref name="opacity"/> thins the whole cloud: the Ninja trial's shadow
    /// copy arrives in a see-through puff to match its see-through figure.
    ///
    /// <paramref name="white"/> is the target range's puff (owner, 2026-09-26: "the
    /// same as the ninjas but white"). Same cloud, same growth, only the colours
    /// change — a white mote over the dark field reads as smoke just as well.
    /// </summary>
    public static ParticleSystem Smoke(Vector2 position, float radius = 1.1f, float opacity = 1f,
                                       bool white = false)
    {
        float scale = radius / BaseRadius;

        var host = new GameObject(white ? "Target Smoke" : "NPC Smoke");
        host.transform.position = new Vector3(position.x, position.y + CloudCentreY * scale, 0f);

        var smoke = Build(host, FrontOrder);

        var main = smoke.main;
        // The lifetime gradient below multiplies into this, so scaling the alpha
        // here thins every puff for its whole life.
        Color start = white ? new Color(0.96f, 0.96f, 0.95f) : new Color(0.34f, 0.33f, 0.33f);
        main.startColor = new Color(start.r, start.g, start.b, 0.95f * Mathf.Clamp01(opacity));
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        // Slow: the emission volume is what covers him now, not the flight. Fast
        // puffs thin the middle of the cloud out on the way to the edges.
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.05f, radius * 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);

        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.7f);
        curve.AddKey(1f, 1.6f);
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var colour = smoke.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            // White smoke greys a little as it thins, the way the dark smoke
            // darkens — it is the fading that sells it, in either colour.
            white
                ? new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.78f, 0.78f, 0.80f), 1f)
                }
                : new[]
                {
                    new GradientColorKey(new Color(0.42f, 0.41f, 0.40f), 0f),
                    new GradientColorKey(new Color(0.22f, 0.22f, 0.23f), 1f)
                },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.85f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        colour.color = gradient;

        // All three axes must share a curve MODE or the system logs "Velocity
        // curves must all be in the same mode" on every puff.
        var velocity = smoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(CloudWidth * scale, CloudHeight * scale, 0f);

        smoke.Play();
        // More of them, because they are spread over a taller volume than before
        // and the same count would have read as a thinner cloud, not a bigger one.
        smoke.Emit(48);
        Object.Destroy(host, 2.5f);
        return smoke;
    }

    /// <summary>
    /// The same smoke, centred ON a point rather than stood on it. Smoke lifts its
    /// cloud off the position it is handed, because an NPC's position is his feet;
    /// a range target's position is the middle of the target, and the lift would
    /// leave its lower half in clear air.
    /// </summary>
    public static ParticleSystem SmokeCentredOn(Vector2 centre, float radius, bool white = false)
    {
        float lift = CloudCentreY * (radius / BaseRadius);
        return Smoke(centre - Vector2.up * lift, radius, 1f, white);
    }

    /// <summary>
    /// The wizard's trail, in his Order's element. Attached to him so it follows
    /// the walk; the existing arrow trails already do exactly this job for a moving
    /// transform, so they are reused rather than rebuilt — but they are then resized
    /// for a figure, see Widen.
    /// </summary>
    public static ParticleSystem Trail(GameObject host, UpgradeOrder accent)
    {
        if (host == null) return null;
        var trail = accent == UpgradeOrder.Frigid
            ? FrostFx.AttachArrowTrail(host)
            : FireFx.AttachArrowTrail(host);
        // BEHIND him, unlike the veil that hides him on the way out. Now that the
        // motes are figure-sized they are emitted across his chest, and in front
        // they would sit on his face for the first frame of their life before the
        // walk carried them clear. Behind, the trail comes out from under him.
        return Widen(Adopt(trail, BehindOrder), host);
    }

    /// <summary>
    /// Turns an ARROW's trail into a WIZARD's. Everything in it is sized for a
    /// 0.3-unit projectile: a 0.06 emitter of 0.1-unit motes, half a second long.
    /// On a figure nearly two units tall that came out as a pencil line down his
    /// middle rather than anything trailing behind him.
    ///
    /// Three separate things were wrong and all three are fixed here.
    /// </summary>
    private static ParticleSystem Widen(ParticleSystem trail, GameObject npc)
    {
        if (trail == null || npc == null) return trail;

        // 1. SIZE. The trail host is parented with the world transform kept, so its
        // localScale is the INVERSE of the NPC's worldScale — and the default Local
        // scaling mode multiplies particle size by exactly that, shrinking every
        // mote to about six tenths of the figure it was asked for. Hierarchy uses
        // the full lossy scale, which is one, so the numbers below are world units.
        // Same lesson FrostFx.AttachEnemyChill records; do not switch it back.
        var main = trail.main;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        // 2. LENGTH. He walks at 3.2 units a second, so this is what decides how
        // far back the trail reaches — half a second of it barely cleared his own
        // sprite. A second and a bit puts the tail of it well above his head.
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);

        // 3. WIDTH AND PLACE. Emitted from a band across the middle of him rather
        // than a point at his origin, which is at his FEET — a point emitter on a
        // walking figure draws a line, and that line was under him.
        var shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        trail.transform.position = Centre(npc);

        // Enough of them to read as a cloud being left behind over that distance,
        // and still inside the shared 120 cap at the longest lifetime.
        var emission = trail.emission;
        emission.rateOverTime = 60f;

        return trail;
    }

    /// <summary>
    /// The veil that covers and hides the wizard as he goes. Takes the FIGURE
    /// rather than a point, for the reason the ninja's smoke does: the only
    /// position an NPC hands out is his feet, and a burst centred there covers his
    /// boots while he fades out above it.
    /// </summary>
    public static void Veil(GameObject npc, UpgradeOrder accent, float radius = 1.1f)
    {
        if (npc == null) return;

        Vector3 centre = Centre(npc);
        // A tight, fast burst is what the combat version is FOR — the leading
        // sparks of an impact. Kept small so those sparks stay sparks, and the
        // cloud that actually hides him is emitted separately below.
        var burst = accent == UpgradeOrder.Frigid
            ? FrostFx.Burst(centre, radius * 0.7f)
            : FireFx.Burst(centre, radius * 0.7f);
        Swell(Adopt(burst, FrontOrder), radius);
    }

    /// <summary>
    /// Second helping for the veil: bigger, slower motes over a wider mouth, which
    /// is what covers a figure. The burst it is handed has ALREADY emitted its own
    /// sparks — a ParticleSystem keeps what is in the air with the settings it was
    /// born with — so this re-emits rather than only retuning.
    /// </summary>
    private static void Swell(ParticleSystem burst, float radius)
    {
        if (burst == null) return;

        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.4f, radius * 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);

        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.5f;

        burst.Emit(36);
    }

    /// <summary>
    /// What the wizard STANDS in once he has stopped walking — his Order's element
    /// on him for as long as he is on the board, the way the upgrade menu keeps an
    /// Order's aura on a knight's portrait rather than only playing it on arrival.
    /// Held: the caller stops it on the way out.
    ///
    /// Both are the tells the arena already owns — the flame that rides a burning
    /// enemy and the motes that drift off a chilled one. That is deliberate: fire
    /// LICKS UP off his base and cold SINKS past him, which is the one contrast
    /// that keeps the two from reading as the same effect in two colours, and it is
    /// already solved there. They arrive sized for a status tell glimpsed across a
    /// crowded board, though, so Stoke feeds them.
    /// </summary>
    public static ParticleSystem Element(GameObject npc, UpgradeOrder accent)
    {
        if (npc == null) return null;

        var system = accent == UpgradeOrder.Frigid
            ? FrostFx.AttachEnemyChill(npc)
            : FireFx.AttachEnemyFlame(npc);

        // In FRONT of him, which is the call the upgrade menu makes for both of
        // these Orders — on the back layer the figure hides most of it and what is
        // left reads as a few stray specks.
        return Stoke(Adopt(system, FrontOrder), accent);
    }

    /// <summary>
    /// Feeds an enemy status tell up to something a lone figure on a still board
    /// can carry. Rate and headroom only — the shape, the colours and the
    /// direction are what make it fire or frost, and none of them are touched.
    /// </summary>
    private static ParticleSystem Stoke(ParticleSystem system, UpgradeOrder accent)
    {
        if (system == null) return null;

        var main = system.main;
        // The stock cap is 120, which the fed rates below would hit and then
        // silently thin out again.
        main.maxParticles = 240;

        var emission = system.emission;
        // Frost is fed harder because it starts at less than half the fire's rate:
        // on an enemy that is the point (a chill is a hint, a blaze is a state),
        // and here they have to carry the same weight.
        emission.enabled = true;
        emission.rateOverTime = emission.rateOverTime.constant *
                                (accent == UpgradeOrder.Frigid ? 3f : 2f);

        system.Play();
        return system;
    }

    /// <summary>
    /// The middle of an NPC in world space. Everything that has to cover a figure
    /// needs this and nothing can get it from the transform: an NPC's origin is at
    /// his FEET, and his height depends on the catalog's worldScale rather than on
    /// anything this file knows.
    /// </summary>
    private static Vector3 Centre(GameObject npc)
    {
        var renderer = npc.GetComponentInChildren<SpriteRenderer>();
        return renderer != null
            ? renderer.bounds.center
            : npc.transform.position + Vector3.up * 0.8f;
    }

    /// <summary>
    /// Takes a system borrowed from the combat effects and makes it fit for a
    /// quest scene: unscaled time, and a sorting order that puts it where the
    /// scene wants it.
    ///
    /// Both matter. Those builders are written for a running arena, where time
    /// flows and nothing needs to out-sort an NPC — so the trail arrived on
    /// SCALED time (frozen solid at timeScale zero) at the default order of ZERO,
    /// which is behind a figure drawn at 550. That is why the wizard's trail was
    /// invisible: it was stopped dead AND behind him. Only the NPC's own copy is
    /// touched; the shared builders keep their gameplay behaviour.
    /// </summary>
    private static ParticleSystem Adopt(ParticleSystem system, int sortingOrder)
    {
        if (system == null) return null;

        var main = system.main;
        main.useUnscaledTime = true;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
        }
        return system;
    }

    /// <summary>
    /// The paladin's light: a warm, slow, breathing bloom he fades in inside of.
    /// Held rather than one-shot — the caller stops it on the way out — so it is
    /// returned instead of self-destructing.
    ///
    /// NOT particles. Eighteen half-unit motes a second read as a dust cloud, not
    /// as light; see NpcAura for the whole argument. This is one soft disc.
    /// </summary>
    public static NpcAura Glow(GameObject host, UpgradeOrder accent)
    {
        if (host == null) return null;

        // The Dawn palette rather than Ember's orange, and the Guardian's steel for
        // the other half of the paladin's business. The steel is UpgradeMenu's own
        // Guardian colour, so the arena and the upgrade screen light him the same.
        Color tint = accent == UpgradeOrder.Guardian
            ? new Color(0.62f, 0.66f, 0.70f)
            : new Color(1f, 0.88f, 0.72f);

        return NpcAura.Bloom(host, tint, GlowSize, BehindOrder);
    }

    /// <summary>
    /// The Guardian's orbs, riding above the paladin's head on a flattened orbit —
    /// round his head rather than spinning in a circle across his face. The shape
    /// and the near/far shading come from UpgradeMenu's Guardian aura; see NpcAura.
    /// </summary>
    public static NpcAura Orbs(GameObject host)
    {
        if (host == null) return null;

        // Above his head: the sprite's origin is at its feet and it stands one unit.
        return NpcAura.Ring(host, OrbSteel, OrbCount, OrbRadius, OrbSize,
                            OrbDegreesPerSecond, height: 1.25f, sortingOrder: FrontOrder);
    }

    /// <summary>Across the paladin, wide enough to hold him without swallowing the plate.</summary>
    private const float GlowSize = 2.6f;

    // The ring, in the menu's proportions: four orbs on a wide flat orbit, turning
    // at a pace you can follow a single one around.
    private const int OrbCount = 4;
    private const float OrbRadius = 0.62f;
    private const float OrbSize = 0.2f;
    private const float OrbDegreesPerSecond = 70f;
    private static readonly Color OrbSteel = new Color(0.78f, 0.84f, 0.90f);

    /// <summary>Fade a held aura out and clean it up.</summary>
    public static void Release(NpcAura aura)
    {
        if (aura != null) aura.Release();
    }

    /// <summary>Let a held system finish what is already in the air, then clean up.</summary>
    public static void Release(ParticleSystem system)
    {
        if (system == null) return;
        var emission = system.emission;
        emission.enabled = false;
        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(system.gameObject, system.main.startLifetime.constantMax + 0.2f);
    }

    // Shared shell, mirroring FireFx.Build: the tintable mote on the default sprite
    // shader, emission driven by the caller rather than by a rate it was born with.
    private static ParticleSystem Build(GameObject host, int sortingOrder)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.playOnAwake = false;
        // THE WHOLE SCENE RUNS AT timeScale ZERO. A particle system simulates on
        // scaled time unless told otherwise, so every effect in this file emitted
        // nothing at all and simply never appeared — the paladin's light and his
        // orbs most visibly, since without them he just fades in on a bare arena.
        main.useUnscaledTime = true;

        var emission = system.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        var renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        var mote = Mote;
        if (mote != null) renderer.material.mainTexture = mote.texture;

        return system;
    }
}
