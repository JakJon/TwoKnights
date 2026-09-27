using System.Collections;
using UnityEngine;

/// <summary>
/// One appearance of the Ninja in the Shadow Order's chase: he arrives in a puff of
/// smoke, stands for a moment, and is either hit or gone.
///
/// Rides on an ordinary NpcActor so he looks, sorts and animates exactly as he does
/// when he walks on to talk. NpcActor switches off every collider an NPC carries —
/// an enemy crossing the arena would otherwise eat him — so the hitbox here is
/// added AFTER it has done that, and only switched on while he can be seen.
///
/// A CLONE is the phase-three decoy: the same figure, see-through and violet, and it
/// arrives and leaves in a puff as see-through as it is (owner, 2026-09-25 — it used
/// to have no smoke at all). Hitting it loses the trial. The tint and the thin smoke
/// are the tells.
/// </summary>
public class NinjaTarget : MonoBehaviour
{
    /// <summary>The puff gets this long on its own before he is in it — NpcActor's own reveal beat.</summary>
    private const float RevealDelaySeconds = 0.3f;

    private static readonly Color CloneTint = new Color(0.69f, 0.54f, 0.95f, 0.5f);

    private SpriteRenderer[] _renderers;
    private BoxCollider2D _hitbox;
    private float _height = 1f;

    public bool IsClone { get; private set; }
    /// <summary>He can be seen and hit. The window starts here, not at the puff.</summary>
    public bool Revealed { get; private set; }
    public bool WasHit { get; private set; }

    /// <summary>Where the arrow should read him as standing, for the hit flash.</summary>
    public Vector2 Centre => (Vector2)transform.position + Vector2.up * (_height * 0.5f);

    public static NinjaTarget Appear(NpcCatalog.Entry entry, Vector2 feet, bool clone)
    {
        var actor = NpcActor.Spawn(entry, UpgradeOrder.Shadow, feet);
        if (actor == null) return null;
        actor.name = clone ? "Ninja Trial (clone)" : "Ninja Trial";

        var target = actor.gameObject.AddComponent<NinjaTarget>();
        target.Begin(clone);
        return target;
    }

    private void Begin(bool clone)
    {
        IsClone = clone;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        // The art stands one unit tall from its feet before the catalog's scale.
        _height = transform.lossyScale.y;

        // In local units, so the catalog's world scale sizes it with the figure.
        // A little narrower than the 32px canvas: the figure does not fill it, and
        // an arrow that clips empty canvas beside him should not count.
        _hitbox = gameObject.AddComponent<BoxCollider2D>();
        _hitbox.isTrigger = true;
        _hitbox.size = new Vector2(0.62f, 0.9f);
        _hitbox.offset = new Vector2(0f, 0.47f);
        _hitbox.enabled = false;

        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        Puff();
        yield return new WaitForSeconds(RevealDelaySeconds);

        if (IsClone) SetColour(CloneTint);
        else SetColour(Color.white);

        _hitbox.enabled = true;
        Revealed = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Revealed || WasHit) return;
        if (CollectibleOrb.CollectorFor(other) == null) return;

        // The arrow is spent on him: one shot, one hit, and it cannot sail on into
        // the next thing on the board.
        TrialOrb.SpendShot(other);
        WasHit = true;
        _hitbox.enabled = false;
    }

    /// <summary>Gone, in smoke — the copy's as thin as the copy.</summary>
    public void Vanish()
    {
        if (_hitbox != null) _hitbox.enabled = false;
        StopAllCoroutines();
        if (Revealed) Puff();
        Revealed = false;
        SetColour(new Color(1f, 1f, 1f, 0f));
        Destroy(gameObject, 0.1f);
    }

    /// <summary>His smoke; the copy's is thinned to the copy's own see-through.</summary>
    private void Puff()
    {
        NpcFx.Smoke(Centre, opacity: IsClone ? CloneTint.a : 1f);
    }

    private void SetColour(Color colour)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].color = colour;
        }
    }
}
