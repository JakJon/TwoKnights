using UnityEngine;

// The gnome bomb's detonation, and the keg cart's. Code-built, no prefab and no
// art of its own — the same arrangement as FireFx and ShadowFx, reusing the
// shared white mote.
//
// It is deliberately not a fireball pop. Ember's burst is a warm orange bloom
// that means "this is on fire and will keep burning"; a bomb is a hard white
// flash that is over instantly, followed by grey smoke that drifts and dies. The
// player has to be able to tell at a glance which one just went off, because one
// of them leaves a burning crater and the other leaves nothing.
//
// THE EFFECT HAS TO BE HONEST ABOUT THE RADIUS. A keg cart is the one thing on
// the rails whose timing the player controls, and the only way to learn that
// timing is to learn how far the powder reaches — so a blast that draws bigger
// or smaller than it hits is teaching the wrong number. Two rules follow, and
// everything below exists to keep them:
//
//   1. NOTHING IS DRAWN OUTSIDE THE DAMAGE CIRCLE. Every layer's travel is
//      solved against its own lifetime so the furthest a mote can possibly get
//      is the rim. The old version rolled speed and lifetime independently —
//      speed up to radius*8 against a lifetime up to 0.26s — so the fastest
//      motes flew to twice the real radius while the slowest covered half of it.
//      The cloud read as roughly double the blast, with no edge anywhere in it.
//   2. THE RIM IS DRAWN EXPLICITLY. An expanding cloud tells you something went
//      off; it does not tell you where it stopped. The ring below opens to
//      exactly the damage radius and then HOLDS there while it fades, and that
//      hold is what the player actually reads the distance off.
public static class BombFx
{
    // Constant, not a range: the whole point is that travel = speed * lifetime is
    // a number we control, and a random lifetime puts that back out of reach.
    private const float FlashLifetime = 0.2f;

    // Where the flash is born, as a fraction of the radius. Subtracted back out
    // of the speed so the head start does not become overshoot.
    private const float FlashOriginFraction = 0.06f;

    private static Sprite MoteSprite
    {
        get
        {
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
    /// One-shot blast at <paramref name="position"/>. <paramref name="radius"/> is
    /// the bomb's real damage radius, and every layer is bounded by it, so what
    /// the player sees is exactly what hurt — the flash reaches the edge of the
    /// kill zone and no further, and the ring marks where that edge was.
    /// </summary>
    public static void Explode(Vector2 position, float radius)
    {
        radius = Mathf.Max(0.05f, radius);

        var host = new GameObject("BombBlast");
        host.transform.position = position;

        SpawnFlash(host.transform, radius);
        SpawnRim(host.transform, radius);
        SpawnSmoke(host.transform, radius);
        BlastRing.Build(host.transform, radius, BlastRing.ExpandSeconds, BlastRing.HoldSeconds);

        Object.Destroy(host, 2.5f);
    }

    // The hit itself: a dense sphere of fast white-hot shrapnel that fills the
    // circle and expires on the rim.
    //
    // Lifetime is fixed and speed is the only roll, so every mote's travel is
    // speed * FlashLifetime and the fastest of them lands on the boundary at the
    // instant it dies. That is what makes the cloud's outer edge the damage edge
    // instead of a guess.
    private static void SpawnFlash(Transform parent, float radius)
    {
        var host = new GameObject("Flash");
        host.transform.SetParent(parent, false);

        var flash = Build(host);

        float originRadius = radius * FlashOriginFraction;
        float reach = (radius - originRadius) / FlashLifetime;

        var main = flash.main;
        main.startColor = new Color(1f, 0.95f, 0.75f, 0.95f);
        main.startLifetime = FlashLifetime;
        // Solved against the fixed lifetime, and measured from the emission ring
        // rather than from the centre: the fastest mote travels exactly the
        // remaining distance to the rim. The floor keeps the middle filled.
        main.startSpeed = new ParticleSystem.MinMaxCurve(reach * 0.25f, reach);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);

        var colorOverLifetime = flash.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.92f), 0f),
                new GradientColorKey(new Color(1f, 0.78f, 0.35f), 0.35f),
                new GradientColorKey(new Color(0.55f, 0.35f, 0.22f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // radiusThickness 0 puts every mote on the little starting ring and fires
        // it straight outward, which is what lets the arithmetic above be exact —
        // a mote born off-centre inside a disc would carry that offset to the rim.
        var shape = flash.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = originRadius;
        shape.radiusThickness = 0f;

        flash.Play();
        // Scaled off circumference: a bigger blast has a bigger rim to fill and
        // would otherwise thin out at the edge, which is where it has to be read
        flash.Emit(Mathf.RoundToInt(30f + radius * 14f));
    }

    // The boundary itself, in debris. Fired when the ring gets there, ON the
    // damage circle and going nowhere, so it settles into a dotted rim that says
    // "this far" for a beat after the front has passed.
    //
    // The ring is the precise read and this is the one that survives a busy
    // frame — a thin line can be lost against the track, a scatter of embers
    // sitting on the same circle cannot.
    private static void SpawnRim(Transform parent, float radius)
    {
        var host = new GameObject("Rim");
        host.transform.SetParent(parent, false);

        var rim = Build(host);

        var main = rim.main;
        main.startColor = new Color(1f, 0.86f, 0.5f, 1f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);

        var colorOverLifetime = rim.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.62f), 0f),
                new GradientColorKey(new Color(0.95f, 0.5f, 0.2f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Exactly the damage radius, edge only. This is the honest circle.
        var shape = rim.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;

        // A timed burst rather than an Emit, because it has to land when the ring
        // does — Emit fires the instant it is called, which would paint the rim
        // before the front ever reached it
        var emission = rim.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(BlastRing.ExpandSeconds, (short)Mathf.RoundToInt(14f + radius * 9f))
        });

        rim.Play();
    }

    // The aftermath: fewer, fatter, slower motes that swell as they grey out and
    // lift, so the blast leaves a mark on the frame for a beat afterwards.
    //
    // Kept well inside the rim on purpose. Smoke is the one layer that outlives
    // the ring, so if it sprawled past the boundary it would be the last thing on
    // screen and the player would remember ITS size as the blast's.
    private static void SpawnSmoke(Transform parent, float radius)
    {
        var host = new GameObject("Smoke");
        host.transform.SetParent(parent, false);

        var smoke = Build(host);

        const float maxLifetime = 0.9f;
        float originRadius = radius * 0.18f;

        var main = smoke.main;
        main.startColor = new Color(0.62f, 0.6f, 0.58f, 0.7f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, maxLifetime);
        // Worst case — top speed against the longest life — lands at about three
        // quarters of the radius, leaving the outer quarter to the rim alone
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.15f,
                                                         radius * 0.55f / maxLifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);

        // Smoke grows instead of shrinking — the opposite of the flash, and of
        // every Ember effect, which is most of what sells it as smoke
        var sizeOverLifetime = smoke.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.5f, 0.48f, 0.46f), 0f),
                new GradientColorKey(new Color(0.34f, 0.33f, 0.33f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // All three axes have to be set to the same curve MODE or the particle
        // system logs "Velocity curves must all be in the same mode" every blast —
        // setting y alone leaves x and z as plain constants.
        //
        // The lift is scaled off the radius too: a fixed 0.8/s rise is most of a
        // small bomb's radius and nothing to a keg's, so only one of the two would
        // have read as smoke drifting off a blast of that size.
        var velocity = smoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(radius * -0.07f, radius * 0.07f);
        velocity.y = new ParticleSystem.MinMaxCurve(radius * 0.1f, radius * 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = originRadius;

        smoke.Play();
        smoke.Emit(Mathf.RoundToInt(10f + radius * 5f));
    }

    // Shared shell: world space so the blast stays where it went off, emission
    // driven entirely by the explicit Emit calls and bursts above
    private static ParticleSystem Build(GameObject host)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        main.playOnAwake = false;
        // Nothing here repeats. A looping system would re-fire SpawnRim's timed
        // burst on every cycle, and that is the one layer which no longer just
        // fires once the moment it is built.
        main.loop = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.25f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

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
            renderer.sortingOrder = 25; // over the knights and the track alike
        }

        return system;
    }
}

/// <summary>
/// The blast front, drawn as an actual circle: it opens to the damage radius in
/// a fraction of a second, then holds on that radius while it fades out.
///
/// This is the whole reason the effect can be trusted. Particles say "something
/// went off near here"; a line that stops on the boundary and stays there for a
/// third of a second says how far, and a player who has seen it twice knows
/// whether the next keg can reach the knight it is rolling past.
///
/// A LineRenderer rather than a sprite because the radius is a runtime number —
/// every caller passes its own, and a scaled ring texture would go soft on the
/// big ones and bloom on the small ones. Positions are rewritten each frame
/// rather than scaling the transform: LineRenderer width does not follow
/// transform scale, so scaling would taper the line as it grew.
///
/// It lives here rather than in BombFx because it is no longer the bomb's: every
/// explosion in the game draws one, at its own radius, and they all have to open
/// and fade on the same clock or the ring stops being a unit of measurement and
/// starts being four different flourishes.
/// </summary>
public class BlastRing : MonoBehaviour
{
    /// <summary>
    /// How long the ring takes to reach full radius. Short enough to read as the
    /// blast front rather than as an animation, long enough to see it travel.
    /// Public because BombFx times its rim debris to arrive with the front.
    /// </summary>
    public const float ExpandSeconds = 0.08f;

    /// <summary>How long it then sits on the rim, fading. The measurement window.</summary>
    public const float HoldSeconds = 0.34f;

    // Enough that the circle does not read as a polygon at keg size, cheap enough
    // that rewriting all of them every frame for a third of a second is nothing
    private const int Segments = 48;

    // Where the ring starts, as a fraction of the target. Not zero: a ring grown
    // from a point spends its first frames as a dot, which reads as part of the
    // flash rather than as a front leaving it.
    private const float StartFraction = 0.3f;

    // The flash's palette one step hotter, so the front is obviously part of the
    // same explosion and not a separate tell. The default for every caller that
    // does not bring its own element's colours.
    private static readonly Color FireInner = new Color(1f, 0.93f, 0.7f);
    private static readonly Color FireOuter = new Color(1f, 0.6f, 0.25f);

    private LineRenderer _line;
    private float _radius;
    private float _expandSeconds;
    private float _holdSeconds;
    private float _age;
    private Color _inner = FireInner;
    private Color _outer = FireOuter;

    /// <summary>
    /// Hung on an existing blast host, at its origin. For callers that already
    /// build a GameObject for their particle layers — the ring rides along with
    /// it and is torn down with it.
    /// </summary>
    public static BlastRing Build(Transform parent, float radius, float expandSeconds, float holdSeconds)
    {
        var host = new GameObject("BlastRing");
        host.transform.SetParent(parent, false);
        host.transform.localPosition = Vector3.zero;
        return Attach(host, radius, expandSeconds, holdSeconds);
    }

    /// <summary>
    /// Standing on its own at <paramref name="position"/>, on the house timings.
    /// For callers whose burst host has a lifetime of its own that has nothing to
    /// do with the ring's — the ring cleans itself up the frame it finishes.
    /// </summary>
    public static BlastRing Spawn(Vector2 position, float radius)
    {
        return Spawn(position, radius, FireInner, FireOuter);
    }

    /// <summary>
    /// The same ring on the same clock, in another element's colours — for a burst
    /// that is not fire (Rimeblade's cold) but still has to show its edge the way
    /// every explosion does. <paramref name="inner"/> and <paramref name="outer"/>
    /// are the two ends of the line, as the fire ring's pale and orange are.
    /// </summary>
    public static BlastRing Spawn(Vector2 position, float radius, Color inner, Color outer)
    {
        var host = new GameObject("BlastRing");
        host.transform.position = position;
        var ring = Attach(host, radius, ExpandSeconds, HoldSeconds);
        ring._inner = inner;
        ring._outer = outer;
        ring.Draw(radius * StartFraction, 1f);
        return ring;
    }

    private static BlastRing Attach(GameObject host, float radius, float expandSeconds, float holdSeconds)
    {
        var line = host.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.material = new Material(Shader.Find("Sprites/Default"));
        // Above the motes: the boundary is the reading, so nothing the blast
        // throws is allowed to bury it
        line.sortingOrder = 26;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        var ring = host.AddComponent<BlastRing>();
        ring._line = line;
        ring._radius = radius;
        ring._expandSeconds = Mathf.Max(0.01f, expandSeconds);
        ring._holdSeconds = Mathf.Max(0.01f, holdSeconds);
        ring.Draw(radius * StartFraction, 1f);

        return ring;
    }

    private void Update()
    {
        _age += Time.deltaTime;

        if (_age < _expandSeconds)
        {
            // Eased out, so the front is quickest at the start and settles onto
            // the boundary rather than slamming into it. The eye tracks where it
            // slows down, and where it slows down is the answer.
            float t = _age / _expandSeconds;
            float eased = 1f - (1f - t) * (1f - t);
            Draw(Mathf.Lerp(_radius * StartFraction, _radius, eased), 1f);
            return;
        }

        float fade = (_age - _expandSeconds) / _holdSeconds;
        if (fade >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Radius is pinned to the real one for the whole fade — the ring never
        // drifts past the damage circle or shrinks back inside it
        Draw(_radius, 1f - fade);
    }

    private void Draw(float radius, float alpha)
    {
        if (_line == null) return;

        // Thins as it fades so the ring reads as dissipating rather than as a
        // painted-on circle, but the RADIUS it sits on never moves
        _line.widthMultiplier = Mathf.Lerp(0.03f, 0.09f, alpha);

        _line.startColor = new Color(_inner.r, _inner.g, _inner.b, alpha);
        _line.endColor = new Color(_outer.r, _outer.g, _outer.b, alpha * 0.85f);

        float step = Mathf.PI * 2f / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = step * i;
            _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }
}
