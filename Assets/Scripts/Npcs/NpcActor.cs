using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One NPC on the board for the length of a quest scene: how they arrive, how they
/// stand there, and how they leave. No two of the five share an entrance, and no
/// exit is a replay of its entrance.
///
/// Everything here runs on UNSCALED time. The scene freezes the arena around it, so
/// WaitForSeconds and Time.deltaTime would both stall.
///
/// The actor carries NO COLLIDER. EnemyBase destroys itself on contact with a
/// knight or shield collider and would eat anything crossing the arena; the same
/// trap TutorialStage documents from the other direction.
/// </summary>
public class NpcActor : MonoBehaviour
{
    /// <summary>Where an NPC stands to talk: between the knights, a unit above them.</summary>
    public static readonly Vector3 Mark = new Vector3(0f, 0.5f, 0f);

    /// <summary>A pair stands either side of the mark so neither hides the other.</summary>
    public const float PairOffset = 1.6f;

    private const float WalkSpeed = 3.2f;
    private const float FadeSeconds = 0.55f;
    private const float GlowLeadSeconds = 0.45f;
    private const float GlowTrailSeconds = 1f;

    /// <summary>
    /// How long a puff of smoke or a burst of light sits on screen, ALONE, before
    /// the figure inside it is revealed. Short enough felt like the NPC and the
    /// particle popped in on the same frame — the effect needs its own beat to
    /// register as the thing that is hiding him, not a decoration on his arrival.
    /// </summary>
    private const float RevealDelaySeconds = 0.35f;

    /// <summary>
    /// The beat after an exit has finished — the walk has carried him off, the
    /// smoke has swallowed him, the light has gone — before the actor is torn
    /// down and the scene is free to move on. Without it the dialogue box, the
    /// reward notices and the arena's own HUD all land on the very frame the exit
    /// animation stops, which reads as the menu shouldering the NPC off screen
    /// rather than as him having actually left.
    /// </summary>
    private const float ExitHoldSeconds = 0.4f;

    /// <summary>
    /// Just short of the end of a clip. Never 1 exactly: a looping state takes
    /// normalized time modulo 1, so asking for 1.0 wraps straight back to frame
    /// zero — the map would snap shut through the very hold it is meant to stay
    /// open for. Anything inside the final frame's slice lands on it, whatever
    /// the frame count.
    /// </summary>
    private const float LastFrameNormalized = 0.999f;

    private NpcCatalog.Entry _entry;
    private UpgradeOrder _accent;
    private Animator _animator;
    private SpriteRenderer[] _renderers;
    private SpriteRenderer _crown;
    // The King's crown is off until Ruckus in the Wood pays it back. Held as state
    // rather than only written once — see LateUpdate.
    private bool _crownVisible = true;
    private ParticleSystem _trail;
    // The wizard's element, standing on him for the whole visit rather than only
    // for the walk in — see NpcFx.Element.
    private ParticleSystem _element;
    // The paladin's light and his ring of orbs. Sprites on a driven orbit rather
    // than particles — see NpcAura for why.
    private NpcAura _glow, _orbs;

    public NpcId Id => _entry != null ? _entry.id : NpcId.None;

    public static NpcActor Spawn(NpcCatalog.Entry entry, UpgradeOrder accent, Vector3 position)
    {
        if (entry == null || entry.prefab == null) return null;

        var go = Instantiate(entry.prefab, position, Quaternion.identity);
        go.name = "NPC " + entry.id;
        // The art is 32x32 at 32 PPU, exactly like a knight, which puts an NPC on
        // the board at the same size as the things fighting on it. They are meant
        // to read as people who have walked into the scene, so they stand taller.
        if (entry.worldScale > 0f) go.transform.localScale = Vector3.one * entry.worldScale;

        var actor = go.AddComponent<NpcActor>();
        actor._entry = entry;
        actor._accent = accent;
        actor.Prepare();
        return actor;
    }

    private void Prepare()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            // The scene runs with timeScale at zero, and an Animator on Normal
            // update mode is scaled by it — so the walk cycle never advanced a
            // single frame and every NPC slid across the board in a fixed pose.
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);

        // The King's crown is his TOP layer, and the importer writes the layer index
        // into each renderer's sortingOrder bottom-up. So find it BEFORE the branch
        // below possibly overwrites those orders — GetComponentsInChildren returns
        // hierarchy order, which usually matches but is not promised to.
        if (_entry.id == NpcId.King) _crown = TopmostRenderer();

        // Above the arena, below the dialogue. Set here rather than on the prefab so
        // the importer regenerating it cannot quietly drop the NPC behind a wolf.
        //
        // Art that imports as individual layers gets a SortingGroup on the root, and
        // under one a child renderer's layer and order are RELATIVE to the group —
        // the group alone decides where the whole figure lands. Writing the children
        // in that case would leave the King sorting at the group's default and
        // standing behind the scenery, so the group is what gets written instead.
        var group = GetComponentInChildren<SortingGroup>(true);
        if (group != null)
        {
            group.sortingLayerName = "Default";
            group.sortingOrder = NpcFx.NpcOrder;
        }
        else
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sortingLayerName = "Default";
                _renderers[i].sortingOrder = NpcFx.NpcOrder + i;
            }
        }

        // Nothing an NPC owns may collide. See the class note.
        foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;

        if (_entry.id == NpcId.King && _crown == null)
        {
            Debug.LogWarning($"[NpcActor] The King imported as {_renderers.Length} renderer(s), so " +
                             "his crown is baked into the sprite and cannot be hidden. His .aseprite " +
                             "needs Layer Import Mode = Individual Layers (layerImportMode: 0 — the " +
                             "enum reads IndividualLayers = 0, MergeFrame = 1, which is the wrong way " +
                             "round from what the name order suggests).");
        }

        SetAlpha(0f);
    }

    /// <summary>
    /// The renderer drawn last — the top layer of the art. Read off sortingOrder as
    /// the importer left it, which is the layer index counted from the bottom.
    /// Null unless there are enough layers for a top one to mean anything.
    /// </summary>
    private SpriteRenderer TopmostRenderer()
    {
        if (_renderers == null || _renderers.Length < 3) return null;
        SpriteRenderer top = null;
        int best = int.MinValue;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            if (_renderers[i].sortingOrder <= best) continue;
            best = _renderers[i].sortingOrder;
            top = _renderers[i];
        }
        return top;
    }

    // ---------- entrances ----------

    public IEnumerator Enter(Vector3 mark)
    {
        switch (_entry.arrival)
        {
            case NpcCatalog.Arrival.Smoke: yield return EnterSmoke(mark); break;
            case NpcCatalog.Arrival.Elemental: yield return EnterElemental(mark); break;
            case NpcCatalog.Arrival.Radiant: yield return EnterRadiant(mark); break;
            default: yield return EnterWalking(mark); break;
        }
    }

    /// <summary>
    /// King and Cartographer: walks down into view, and freezes on frame zero the
    /// moment he stops. A walk cycle left running under a stationary figure reads as
    /// a treadmill, and stopping mid-stride reads as the animation having broken.
    /// </summary>
    private IEnumerator EnterWalking(Vector3 mark)
    {
        // In from ABOVE, out through the bottom — one direction of travel across the
        // whole visit, rather than arriving and retreating the same way. Started
        // clear of the frame for the same reason the exit now clears it: a flat
        // five units up is y = 5.5 against a top edge of 5.625, so his feet were
        // showing before he had taken a step.
        transform.position = new Vector3(mark.x, OffScreenAboveY(), mark.z);
        SetAlpha(1f);
        PlayWalk();
        yield return WalkTo(mark);
        FreezeOnFirstFrame();
    }

    private IEnumerator EnterSmoke(Vector3 mark)
    {
        transform.position = mark;
        NpcFx.Smoke(mark);
        // Behind the smoke, already moving: he was never seen to arrive, which is
        // the whole idea. The puff gets a beat to itself before he does.
        yield return WaitUnscaled(RevealDelaySeconds);
        SetAlpha(1f);
        PlayLoop();
        yield return WaitUnscaled(0.5f);
    }

    private IEnumerator EnterElemental(Vector3 mark)
    {
        transform.position = new Vector3(mark.x, OffScreenAboveY(), mark.z);
        SetAlpha(1f);
        PlayWalk();
        _trail = NpcFx.Trail(gameObject, _accent);
        yield return WalkTo(mark);
        NpcFx.Release(_trail);
        _trail = null;
        FreezeOnFirstFrame();
        // The trail is the WALK; this is him standing in his element while he
        // talks. Handing off at the mark means there is no frame where the wizard
        // has no element on him at all.
        _element = NpcFx.Element(gameObject, _accent);
    }

    /// <summary>
    /// Paladin: the light arrives first and at full strength, then he fades in
    /// inside it with the orbs already turning. The order is the effect — a figure
    /// who appears and then lights up is a sprite with a glow on it.
    /// </summary>
    private IEnumerator EnterRadiant(Vector3 mark)
    {
        transform.position = mark;
        _glow = NpcFx.Glow(gameObject, _accent);
        // The light sits alone at full strength before he fades in inside it.
        yield return WaitUnscaled(GlowLeadSeconds);

        PlayLoop();
        _orbs = NpcFx.Orbs(gameObject);
        yield return FadeTo(1f);
    }

    // ---------- exits ----------

    public IEnumerator Leave()
    {
        switch (_entry.arrival)
        {
            case NpcCatalog.Arrival.Smoke: yield return LeaveSmoke(); break;
            case NpcCatalog.Arrival.Elemental: yield return LeaveElemental(); break;
            case NpcCatalog.Arrival.Radiant: yield return LeaveRadiant(); break;
            default: yield return LeaveWalking(); break;
        }
        // A beat once the exit itself is done, so the scene does not snap straight
        // to whatever comes next on the very frame the last step or the last
        // particle lands.
        yield return WaitUnscaled(ExitHoldSeconds);
        Destroy(gameObject);
    }

    /// <summary>Walks DOWNWARD out of the view — not back the way he came.</summary>
    private IEnumerator LeaveWalking()
    {
        PlayWalk();
        yield return WalkTo(new Vector3(transform.position.x, OffScreenBelowY(), transform.position.z));
    }

    /// <summary>
    /// A Y far enough below the camera that the whole figure has gone.
    ///
    /// This used to walk a flat six units down from the mark, which lands at
    /// y = -5.5 — and the bottom of the frame is at -5.625. The NPC stopped a
    /// hair inside the view and stood there, which is exactly what "they stop
    /// moving at the bottom instead of walking off" looks like. The edge is
    /// measured rather than assumed so a camera change cannot silently restore
    /// the bug.
    /// </summary>
    private float OffScreenBelowY()
    {
        // The sprite's origin is at its feet and it stands one unit before
        // scale, so the feet must clear the edge by the full height for the
        // head to follow them out.
        return CameraEdgeY(below: true) - (transform.localScale.y + 0.5f);
    }

    /// <summary>
    /// Where a walker starts, clear of the top of the frame. Feet resting on the
    /// top edge already hides the whole figure — the art stands UPWARD from its
    /// origin — so this only needs a small margin beyond it.
    /// </summary>
    private float OffScreenAboveY()
    {
        return CameraEdgeY(below: false) + 0.5f;
    }

    /// <summary>Where the frame ends, top or bottom, with a sane fallback.</summary>
    private static float CameraEdgeY(bool below)
    {
        var cam = Camera.main;
        float centre = 0f, half = 5.625f;
        if (cam != null && cam.orthographic)
        {
            centre = cam.transform.position.y;
            half = cam.orthographicSize;
        }
        return below ? centre - half : centre + half;
    }

    private IEnumerator LeaveSmoke()
    {
        NpcFx.Smoke(transform.position);
        yield return WaitUnscaled(RevealDelaySeconds);
        SetAlpha(0f);
    }

    private IEnumerator LeaveElemental()
    {
        // The element goes out under the veil rather than after it: what is still
        // in the air fades on its own clock while the burst covers the swap.
        NpcFx.Release(_element);
        _element = null;
        NpcFx.Veil(gameObject, _accent);
        yield return WaitUnscaled(0.2f);
        yield return FadeTo(0f);
    }

    /// <summary>Paladin and orbs first, then the light a beat behind them.</summary>
    private IEnumerator LeaveRadiant()
    {
        NpcFx.Release(_orbs);
        _orbs = null;
        yield return FadeTo(0f);

        yield return WaitUnscaled(GlowTrailSeconds);
        NpcFx.Release(_glow);
        _glow = null;
    }

    // ---------- the King's own business ----------

    /// <summary>
    /// The crown, hidden through the quest that asks for it back and revealed the
    /// moment he has it. Only the King has one; for everyone else this does nothing
    /// rather than complaining, because a directive in prose is not worth a warning.
    /// </summary>
    public void SetCrownVisible(bool visible)
    {
        _crownVisible = visible;
        if (_crown != null) _crown.enabled = visible;
    }

    /// <summary>
    /// Re-asserts the crown every frame, AFTER the Animator has run.
    ///
    /// The importer's generated clips key each layer's renderer, so an Animator
    /// playing the walk cycle puts the crown back the moment it is switched off.
    /// Update order is what settles it: animation evaluates in Update, this runs
    /// in LateUpdate, so this is the last word.
    /// </summary>
    private void LateUpdate()
    {
        if (_crown != null && _crown.enabled != _crownVisible) _crown.enabled = _crownVisible;
    }

    /// <summary>
    /// Walks two units down, stops, and waits — the beat the King takes mid-sentence
    /// before turning back with the rest of it.
    /// </summary>
    public IEnumerator StepAway()
    {
        PlayWalk();
        yield return WalkTo(transform.position + Vector3.down * 2f);
        FreezeOnFirstFrame();
        yield return WaitUnscaled(0.25f);
    }

    /// <summary>
    /// The cartographer's loop: open the map, hold it open to read for five
    /// seconds, close it, then stand on the walk cycle's frozen first frame for
    /// another five before opening it again.
    ///
    /// "open map" and "close map" are TWO FRAMES each — a fifth of a second. The
    /// old version played one and then waited 3.3s with the Animator still running
    /// at speed 1, so the flourish replayed some sixteen times under a figure that
    /// was supposed to be standing there reading. That is the "plays over and over".
    /// Each state is now played through exactly once, timed off its own real clip
    /// length, and then explicitly frozen — which holds whether or not the
    /// generated clip happens to be marked looping.
    /// </summary>
    public IEnumerator ConsultMap()
    {
        if (string.IsNullOrEmpty(_entry.openState)) yield break;

        yield return PlayStateOnce(_entry.openState);   // ends held on the last frame
        yield return WaitUnscaled(5f);

        if (!string.IsNullOrEmpty(_entry.closeState))
        {
            yield return PlayStateOnce(_entry.closeState);
        }

        StandStill();
        yield return WaitUnscaled(5f);
    }

    /// <summary>
    /// Back to the standing pose: the walk cycle's first frame, not animated.
    ///
    /// Public because the SCENE has to settle him when the map loop stops. The
    /// loop is killed wherever it happens to be, so without this he spends the
    /// reward and unlock cards holding whatever pose he was caught in — the map
    /// open on some runs and shut on others, which is the inconsistency.
    /// </summary>
    public void StandStill()
    {
        FreezeOnFirstFrame();
    }

    /// <summary>
    /// Runs one Animator state through exactly once, and leaves it held on its
    /// final frame.
    ///
    /// The Animator does not drive this: speed stays at ZERO and the normalized
    /// time is written every frame. Letting it play itself and freezing after a
    /// wall-clock wait is what still made the map flicker — these clips are two
    /// frames (0.2s) and they LOOP, so a single late frame was enough to start a
    /// second pass before the freeze landed. Driving the time here, clamped below
    /// 1, makes a second pass unreachable no matter how the frames fall.
    /// </summary>
    private IEnumerator PlayStateOnce(string state)
    {
        if (_animator == null || string.IsNullOrEmpty(state)) yield break;

        // SPEED MUST BE 1 FOR THE LENGTH READ. A state reports its length as the
        // clip length divided by the Animator's speed, so reading it while frozen
        // hands back Infinity — and then `elapsed < Infinity` never ends and
        // `elapsed / Infinity` is always zero. That is precisely what pinned the
        // cartographer on the first frame of his open-map tag forever.
        _animator.speed = 1f;
        _animator.Play(state, 0, 0f);
        // Forces the deferred Play to evaluate now, so the length read below
        // belongs to THIS state rather than whatever was playing a moment ago.
        _animator.Update(0f);

        float length = _animator.GetCurrentAnimatorStateInfo(0).length;
        if (length <= 0f || float.IsInfinity(length) || float.IsNaN(length)) yield break;

        // From here WE drive the playhead. The Animator must not advance it as
        // well, or a looping two-frame clip slips into a second pass between our
        // writes — the flicker this method exists to remove.
        _animator.speed = 0f;

        float elapsed = 0f;
        while (elapsed < length)
        {
            elapsed += Time.unscaledDeltaTime;
            _animator.Play(state, 0, Mathf.Min(elapsed / length, LastFrameNormalized));
            _animator.Update(0f);
            yield return null;
        }

        _animator.Play(state, 0, LastFrameNormalized);
        _animator.Update(0f);
    }

    // ---------- movement and animation ----------

    private IEnumerator WalkTo(Vector3 destination)
    {
        while ((transform.position - destination).sqrMagnitude > 0.001f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, destination, WalkSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    private void PlayWalk()
    {
        if (_animator == null) return;
        _animator.speed = 1f;
        if (!string.IsNullOrEmpty(_entry.walkState)) _animator.Play(_entry.walkState, 0, 0f);
    }

    /// <summary>For the two with no Aseprite tags: one clip, always running.</summary>
    private void PlayLoop()
    {
        if (_animator == null) return;
        _animator.speed = 1f;
    }

    /// <summary>
    /// Holds the walk on FRAME ZERO. These sprites have no idle pose, so the
    /// first frame of the cycle is what has to read as standing — stopping
    /// wherever the loop happened to be leaves the figure mid-stride, one leg in
    /// the air, looking like the animation broke.
    ///
    /// The state is rewound by name only when there is a name to use. The untagged
    /// NPCs import as one clip whose name we do not know, and the King is one of
    /// them — asking for "" rewound nothing at all, which is exactly the bug this
    /// reads as. Rewinding whatever state the Animator is already in covers both.
    /// </summary>
    private void FreezeOnFirstFrame()
    {
        if (_animator == null) return;
        if (!string.IsNullOrEmpty(_entry.walkState)) _animator.Play(_entry.walkState, 0, 0f);
        else _animator.Play(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
        // Forces the deferred Play to evaluate now, so the pose is frame zero
        // before the speed drops rather than a frame later.
        _animator.Update(0f);
        _animator.speed = 0f;
    }

    // ---------- fading ----------

    private void SetAlpha(float alpha)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            // The crown keeps its own visibility; alpha must not switch it back on.
            var c = _renderers[i].color;
            _renderers[i].color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    private IEnumerator FadeTo(float target)
    {
        float from = _renderers != null && _renderers.Length > 0 ? _renderers[0].color.a : 0f;
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, target, elapsed / FadeSeconds));
            yield return null;
        }
        SetAlpha(target);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }

    private void OnDestroy()
    {
        NpcFx.Release(_glow);
        NpcFx.Release(_orbs);
        NpcFx.Release(_trail);
        NpcFx.Release(_element);
    }
}
