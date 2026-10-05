using UnityEngine;

// What a shot going through a mirror looks like.
//
// Until this existed the trip was silent: an arrow reached a pane and was simply
// somewhere else on the next frame, and the only way to know which pane it had
// come out of was to already know the pairs. A redirect nobody can read is a
// redirect nobody can aim with, so both ends now say what happened, in the
// pair's own colour:
//
//   * the pane it went INTO takes a small splash back toward the shooter, the
//     way a stone goes into water;
//   * the pane it comes OUT of throws a longer spray along the new heading, so
//     the eye is handed the direction the shot is now travelling.
//
// Each end also gets one wide, short-lived flare over the glass, and the panes
// themselves swell for a moment (MirrorPane.Pulse).
//
// Code-built like FireFx, ShadowFx and DawnFx, and it borrows the same white
// mote they do — that sprite exists to be tinted. The scatter is rolled, which
// is allowed here: the no-randomness rule is about what a wave sends, and this
// is only how a thing that has already happened is drawn.
public static class MirrorFx
{
    private const int SplashMotes = 9;
    private const int SprayMotes = 16;

    /// <summary>The colour a pair glows, matched to the glass in the pane art.</summary>
    public static Color GlassColor(MirrorColor color)
    {
        switch (color)
        {
            case MirrorColor.Cyan: return new Color(122f / 255f, 236f / 255f, 250f / 255f);
            case MirrorColor.Vermilion: return new Color(250f / 255f, 112f / 255f, 80f / 255f);
            case MirrorColor.Magenta: return new Color(240f / 255f, 112f / 255f, 214f / 255f);
            default: return new Color(126f / 255f, 246f / 255f, 152f / 255f);
        }
    }

    /// <summary>
    /// One shot's trip, shown at both panes. Called by MirrorPane at the moment
    /// it moves the shot, with where it went in and where it was put down.
    /// </summary>
    public static void Passage(MirrorPane from, Vector2 entry, Vector2 inDirection,
                               MirrorPane to, Vector2 exit, Vector2 outDirection)
    {
        if (from == null || to == null) return;
        Color glass = GlassColor(from.PaneColor);

        // In: a short splash thrown back the way the shot came.
        Burst(entry, -inDirection, 70f, SplashMotes, 1.2f, 3.6f, 0.16f, 0.32f, glass, from);
        // Out: a longer, tighter spray along the heading it now has.
        Burst(exit, outDirection, 32f, SprayMotes, 2.6f, 7.5f, 0.22f, 0.46f, glass, to);
    }

    private static void Burst(Vector2 at, Vector2 heading, float halfAngle, int motes,
                              float slowest, float fastest, float shortest, float longest,
                              Color glass, MirrorPane pane)
    {
        var host = new GameObject("MirrorPassage");
        host.transform.position = new Vector3(at.x, at.y, 0f);
        ParticleSystem system = Build(host, pane);

        float baseAngle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < motes; i++)
        {
            float angle = (baseAngle + Random.Range(-halfAngle, halfAngle)) * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            emit.position = host.transform.position + direction * Random.Range(0f, 0.15f);
            emit.velocity = direction * Random.Range(slowest, fastest);
            emit.startLifetime = Random.Range(shortest, longest);
            emit.startSize = Random.Range(0.10f, 0.24f);
            // Every third mote is nearly white, so the spray has a hot core
            emit.startColor = i % 3 == 0 ? Color.Lerp(glass, Color.white, 0.75f) : glass;
            system.Emit(emit, 1);
        }

        // The flare: one wide mote that is gone almost at once.
        emit.position = host.transform.position;
        emit.velocity = Vector3.zero;
        emit.startLifetime = 0.14f;
        emit.startSize = 1.5f;
        emit.startColor = new Color(glass.r, glass.g, glass.b, 0.55f);
        system.Emit(emit, 1);

        Object.Destroy(host, 0.8f);
    }

    private static ParticleSystem Build(GameObject host, MirrorPane pane)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        // Motes shrink to nothing rather than popping out
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        // They slow as they go, so the spray fans out and hangs instead of flying off
        var limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = 0.6f;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            Sprite mote = FireResourceManager.Instance != null
                ? FireResourceManager.Instance.GetEmberMoteSprite()
                : null;
            if (mote != null)
            {
                material.mainTexture = mote.texture;
            }
            renderer.material = material;
            // Over the pane it belongs to, whatever layer the panes are drawn on
            renderer.sortingLayerID = pane.SortingLayerId;
            renderer.sortingOrder = pane.SortingOrder + 5;
        }

        system.Play();
        return system;
    }
}
