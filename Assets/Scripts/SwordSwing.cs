using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class SwordSwing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference swordSwingAction;
    // Stick click: the same swing, but straight behind the knight - the way the
    // shield is NOT facing. Shares the blade and its cooldown with the trigger.
    [SerializeField] private InputActionReference backSwingAction;

    [Header("Settings")]
    [SerializeField] private float swingDuration = 0.15f;
    [SerializeField] private float totalEffectDuration = 0.3f;
    [SerializeField] private float swingAngleRange = 60f;
    [SerializeField] private int swingDamage = 10;
    [SerializeField] private float cooldownTime = 1f;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private InputAction swordInputAction;
    private InputAction backSwingInputAction;
    private bool canSwing = true;
    private bool _backSwing; // The swing in progress was the stick click's
    private int currentSwingDamage; // Full damage for real swings, halved for Phantom Blade echoes
    private Transform swordSpriteTransform;
    private Vector3 _bladeBaseScale = Vector3.one;
    private Transform slashSpriteTransform;
    private HashSet<GameObject> damagedEnemies;
    private ShieldOrbit shield;
    private GameObject owningKnight;

    /// <summary>The knight this blade belongs to. Exposed because things the sword
    /// touches sometimes have to pay that knight rather than damage what they hit —
    /// an orb the blade sweeps through is collected for them, exactly as an arrow
    /// that reaches it is. Null until the first swing wires the shield up.</summary>
    public GameObject OwningKnight => owningKnight;

    // Long Sword lives on the knight, so it is looked up per swing rather than
    // cached in Awake: the upgrade can land between one swing and the next.
    private LongSwordBoost LongSword =>
        owningKnight != null ? owningKnight.GetComponent<LongSwordBoost>() : null;

    // Stock values unless a Long Sword rank says otherwise.
    private float BladeReach => LongSword != null ? LongSword.LengthMultiplier : 1f;
    private float SwingSlow => LongSword != null ? LongSword.SlowMultiplier : 1f;
    private int EffectiveSwingDamage
    {
        get
        {
            var boost = LongSword;
            return boost != null && boost.BaseDamage > 0 ? boost.BaseDamage : swingDamage;
        }
    }

    void Awake()
    {
        swordSpriteTransform = transform.Find("Sword");
        // The prefab's own blade scale is not 1 - Long Sword multiplies THIS,
        // never replaces it, or a knight without the upgrade gets a sword that
        // silently grew to full size the first time it swung.
        if (swordSpriteTransform != null) _bladeBaseScale = swordSpriteTransform.localScale;
        slashSpriteTransform = transform.Find("Slash");
        shield = GetComponentInParent<ShieldOrbit>();
        if (shield == null && transform.parent != null)
            shield = transform.parent.GetComponentInChildren<ShieldOrbit>();
        if (swordSwingAction != null)
        {
            swordInputAction = swordSwingAction.action;
            swordInputAction.Enable();
        }
        if (backSwingAction != null)
        {
            backSwingInputAction = backSwingAction.action;
            backSwingInputAction.Enable();
        }
        AddDamageDetection(swordSpriteTransform);
        AddDamageDetection(slashSpriteTransform);

        // The sword attack lives under its knight (knight_left/knight_right) —
        // Serpent's Breath reads the knight's poison boost per swing
        var parentHealth = GetComponentInParent<PlayerHealth>();
        if (parentHealth != null)
        {
            owningKnight = parentHealth.gameObject;
        }
    }

    void Update()
    {
        if (!InputEnabled || !canSwing || shield == null) return;

        bool forward = swordInputAction != null && swordInputAction.WasPressedThisFrame();
        bool backward = backSwingInputAction != null && backSwingInputAction.WasPressedThisFrame();
        if (!forward && !backward) return;

        // Both on the same frame: the trigger wins, as it is the swing the knight
        // is facing into
        _backSwing = !forward;
        AudioManager.Instance.PlaySFX(AudioManager.Instance.swordSwing);
        StartCoroutine(PerformSwordSwing());
    }

    // The way the swing in progress faces: along the shield, or straight behind
    // it for a back-swing. Everything a swing throws off (breath, Firebrand,
    // Rimeblade, the Acid Dagger, Phantom echoes) follows the blade, not the guard.
    private float SwingAngle => shield.CurrentAngle + (_backSwing ? 180f : 0f);
    private Vector2 SwingDirection => _backSwing ? -shield.Direction : shield.Direction;

    /// <summary>
    /// Drops a swing still in the air and puts the blade away.
    ///
    /// Every wait inside PerformSwordSwing runs on SCALED time, so a swing loosed on
    /// the last frame of a wave does not finish while an NPC scene holds timeScale at
    /// zero — the sword simply stands there, mid-arc, for the whole conversation.
    /// Called by ArenaSweep before anyone walks on.
    ///
    /// The cooldown goes with it rather than being served out: the knight is not
    /// swinging at anything during a scene, and holding the blade back afterwards
    /// would be a punishment for a wave that had already ended.
    /// </summary>
    public void CancelSwing()
    {
        StopAllCoroutines();

        // Tint before hiding: a swing cancelled inside a Phantom Blade echo would
        // otherwise come back purple on the next wave.
        SetSwingTint(Color.white);
        if (swordSpriteTransform != null) swordSpriteTransform.gameObject.SetActive(false);
        if (slashSpriteTransform != null) slashSpriteTransform.gameObject.SetActive(false);
        // StopAllCoroutines also kills an Acid Dagger jab, which is the one other
        // coroutine this component runs and the one other sprite it leaves showing.
        if (_daggerTransform != null) _daggerTransform.gameObject.SetActive(false);
        if (shield != null) shield.SetSwordReloadBarVisible(false);

        _inEchoPhase = false;
        currentSwingDamage = EffectiveSwingDamage;
        canSwing = true;
    }

    private IEnumerator PerformSwordSwing()
    {
        canSwing = false;
        damagedEnemies = new HashSet<GameObject>();
        currentSwingDamage = EffectiveSwingDamage;
        ExhaleSerpentsBreath();
        TryHurlFirebrand();
        TryReleaseRimeblade();
        TryQueueAcidDagger();
        if (swordSpriteTransform == null || slashSpriteTransform == null)
        {
            canSwing = true;
            yield break;
        }
        // The blue bar beside the bow's red one: full while the blade is out, then
        // draining across the cooldown the way the red one drains across the bow's
        shield.SetSwordReloadBarVisible(true);
        shield.SetSwordReloadBarFill(1f);
        float shieldAngle = SwingAngle;
        // Feat tracking: a phase is the real swing or one echo. The Shadow quest
        // wants a swing where the real strike AND both echoes each connected.
        _phaseLanded = false;
        _phasesLanded = 0;

        // Only the ARC is slowed by a long blade, not the hold at the end of it:
        // the weight is meant to be felt in the sweep the player is waiting on.
        //
        // The position radius passed here is pinned at 1, NOT BladeReach: the hilt
        // stays where it always was, and only the blade sprite (bladeScale.y below)
        // grows outward from it. Long Sword's reach is entirely in that growth - see
        // LongSwordBoost's header comment for why the tip works out to 1 + a fraction
        // of LengthMultiplier rather than the whole arc scaling by it. Passing
        // BladeReach here instead once made the hilt itself swing a Long Sword's
        // width out from the knight, which read as the sword spawning far off to the
        // side and looking longer than it actually was.
        yield return StartCoroutine(AnimateSwingArc(shieldAngle, totalEffectDuration - swingDuration,
                                                    swingDuration * SwingSlow, 1f));
        if (_phaseLanded) _phasesLanded++;

        // Phantom Blade (Shadow Order): dark after-images repeat the swing. They
        // trail the real swing after a short beat, then sweep through at the real
        // swing's own pace. They used to whip through at twice that, which was too
        // quick to follow (owner, 2026-09-26).
        NinjaBoost ninja = owningKnight != null ? owningKnight.GetComponent<NinjaBoost>() : null;
        int echoCount = ninja != null ? ninja.TotalPhantomEchoes : 0;
        for (int i = 0; i < echoCount; i++)
        {
            yield return new WaitForSeconds(0.1f);
            damagedEnemies = new HashSet<GameObject>();
            _phaseLanded = false;
            _inEchoPhase = true;
            currentSwingDamage = Mathf.Max(1, EffectiveSwingDamage / 2);
            SetSwingTint(PhantomTint);
            AudioManager.Instance.PlaySFX(AudioManager.Instance.phantomStrike);
            yield return StartCoroutine(AnimateSwingArc(shieldAngle, 0.025f, swingDuration * SwingSlow,
                                                       PhantomEchoReach(i)));
            SetSwingTint(Color.white);
            _inEchoPhase = false;
            if (_phaseLanded) _phasesLanded++;
        }
        currentSwingDamage = EffectiveSwingDamage;

        // Three connected phases can only happen with two echoes behind the real
        // swing, which is what "a fully upgraded Phantom Blade" means
        if (echoCount >= 2 && _phasesLanded >= 3) Feats.Record(Feats.PhantomFullThree);

        // Scaled time, like the WaitForSeconds this replaced, so a scene holding
        // timeScale at zero holds the bar where it is
        float elapsed = 0f;
        while (elapsed < cooldownTime)
        {
            shield.SetSwordReloadBarFill(1f - elapsed / cooldownTime);
            yield return null;
            elapsed += Time.deltaTime;
        }
        shield.SetSwordReloadBarVisible(false);
        canSwing = true;
    }

    // Phantom Blade echoes arc just past the real swing, so the dark repeat sweeps
    // ground the knight's own reach never covers instead of redrawing itself on
    // top of the strike that just happened. Each echo's hilt sits this far beyond
    // the point of the blade before it, so the set fans outward in a row of
    // evenly spaced blades — the third swing of a full Phantom Blade lands past
    // the second, not on top of it.
    //
    // A quarter unit is the gap the stock sword always had (hilt at 1, point at
    // 1.75, first echo's hilt at 2). It is measured off the POINT, not multiplied
    // through the whole radius, because Long Sword only lengthens the blade: the
    // old "2 x BladeReach" put a rank III echo's hilt nearly seven units out, a
    // full three past where the real blade stopped.
    private const float PhantomGap = 0.25f;

    // Arc radius (hilt distance) for echo `echoIndex`, 0 being the first. Every
    // blade in the set is the same length - the echoes are drawn with Long Sword's
    // stretch too - so each step outward is one blade plus the gap.
    private float PhantomEchoReach(int echoIndex)
    {
        float bladeLength = SwordTipReach() - 1f;
        return 1f + (echoIndex + 1) * (bladeLength + PhantomGap);
    }

    // How far inside the hilt's arc the blade starts and ends its sweep, and how
    // far inside it the slash is drawn. Fixed distances rather than fractions of
    // `reach`: as fractions they were right for the real swing and drifted inward
    // with every unit an echo was pushed out, which left a Long Sword echo's slash
    // floating well short of the blade it belonged to.
    private const float SwingEndInset = 0.2f;
    private const float SlashInset = 0.4f;

    // One full swing animation pass (sprites on -> arc -> hold -> sprites off);
    // shared by the real swing and its Phantom Blade echoes. `reach` is how far
    // from the knight the hilt's arc is drawn — 1 is the sword's own reach.
    private IEnumerator AnimateSwingArc(float shieldAngle, float endHold, float arcDuration,
                                        float reach = 1f)
    {
        float startAngle = shieldAngle - 45f;
        float endAngle = shieldAngle + 45f;
        swordSpriteTransform.gameObject.SetActive(true);
        // Position slash sprite but keep it disabled for now
        float shieldAngleRad = shieldAngle * Mathf.Deg2Rad;
        Vector3 slashPosition = (new Vector3(Mathf.Cos(shieldAngleRad), Mathf.Sin(shieldAngleRad), 0) * (reach - SlashInset)) + rotationOffset;
        slashSpriteTransform.localPosition = slashPosition;
        slashSpriteTransform.localRotation = Quaternion.Euler(0, 0, shieldAngle - 90f);
        // Position and rotate sword sprite at starting angle
        // The blade is drawn longer, not just swung wider - the sword sprite is
        // rotated so its length runs along local Y, and scaling that also grows
        // the box collider added in AddDamageDetection, so what the player sees
        // and what actually connects stay the same shape.
        Vector3 bladeScale = _bladeBaseScale;
        bladeScale.y = _bladeBaseScale.y * BladeReach;
        swordSpriteTransform.localScale = bladeScale;

        float startAngleRad = startAngle * Mathf.Deg2Rad;
        Vector3 swordStartPos = (new Vector3(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad), 0) * (reach - SwingEndInset)) + rotationOffset;
        swordSpriteTransform.localPosition = swordStartPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, startAngle - 90f);

        // Activate slash sprite immediately
        slashSpriteTransform.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / arcDuration;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
            float currentAngleRad = currentAngle * Mathf.Deg2Rad;
            Vector3 swordCurrentPos = (new Vector3(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad), 0) * reach) + rotationOffset;
            swordSpriteTransform.localPosition = swordCurrentPos;
            swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, currentAngle - 90f);
            yield return null;
        }
        float endAngleRad = endAngle * Mathf.Deg2Rad;
        Vector3 swordEndPos = (new Vector3(Mathf.Cos(endAngleRad), Mathf.Sin(endAngleRad), 0) * (reach - SwingEndInset)) + rotationOffset;
        swordSpriteTransform.localPosition = swordEndPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, endAngle - 90f);
        yield return new WaitForSeconds(endHold);
        swordSpriteTransform.gameObject.SetActive(false);
        slashSpriteTransform.gameObject.SetActive(false);
    }

    private static readonly Color PhantomTint = new Color(0.45f, 0.4f, 0.75f, 0.75f);

    private void SetSwingTint(Color tint)
    {
        // Null-checked on the transforms as well as the renderers: CancelSwing can
        // reach this before ShieldOrbit has finished building the blade.
        var swordSprite = swordSpriteTransform != null
            ? swordSpriteTransform.GetComponent<SpriteRenderer>() : null;
        if (swordSprite != null) swordSprite.color = tint;
        var slashSprite = slashSpriteTransform != null
            ? slashSpriteTransform.GetComponent<SpriteRenderer>() : null;
        if (slashSprite != null) slashSprite.color = tint;
    }

    // Serpent's Breath: EVERY swing exhales venom along the shield facing. What
    // comes out - one bead, a fan of three, or a pair of clouds - and how long it
    // lasts is the knight's rank, and lives on PoisonTipBoost.
    //
    // No roll here any more (owner, 2026-09-17). A swing the player chose to make
    // is already the cadence; asking a coin whether it counted meant the discipline
    // could not be aimed, and aiming it is the entire reason it comes off the sword
    // rather than off the bow.
    //
    // Beads carry the arrow's own venom numbers so the tick bonus flows through
    // the exhale the same way it flows through a trail.
    private const int BreathPoisonDamage = 2;
    private const float BreathPoisonSeconds = 30f;
    private const float BreathPoisonTickRate = 1f;

    // Halved from 2 (owner, 2026-09-23), so the beads now drift out slower than
    // rank III's cloud (1.5) and it runs on ahead of the fan rather than behind it.
    private const float BreathBeadSpeed = 1f;

    // A NARROW fan. Beads are quarter-size, so eleven degrees apart still reads as
    // three distinct things without the outer two looking like misses.
    private const float BreathBeadFanStep = 11f;

    private void ExhaleSerpentsBreath()
    {
        if (owningKnight == null || shield == null) return;

        PoisonTipBoost boost = owningKnight.GetComponent<PoisonTipBoost>();
        if (boost == null || boost.BreathBeadCount <= 0) return;

        Vector2 facing = SwingDirection;
        Vector2 origin = (Vector2)owningKnight.transform.position + facing * 1.3f;
        string knightTag = owningKnight.tag;

        float[] angles = BreathFanAngles(boost.BreathBeadCount, BreathBeadFanStep);
        foreach (float angle in angles)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * facing;
            PoisonTrailBubble.Launch(origin, direction, BreathBeadSpeed,
                boost.NextBreathSeconds(),
                BreathPoisonDamage + boost.TickDamageBonus,
                BreathPoisonSeconds,
                BreathPoisonTickRate,
                knightTag);
        }

        // Rank III's cloud goes straight down the facing, through the middle of the
        // fan rather than beside it. The beads are the part that can miss - they are
        // spent on first contact - so the cloud belongs on the one line the player
        // actually aimed at, holding the ground the fan was thrown over.
        if (boost.BreathSendsCloud)
        {
            PoisonCloud.SpawnTraveling(origin, facing, PoisonTipBoost.BreathCloudSeconds,
                boost.BreathLargeCloud, knightTag);
        }
    }

    // Centred on the facing and spread evenly, so an odd count always sends one
    // straight down the aim and an even count straddles it - the same rule
    // Firebrand's spread follows, for the same reason: what comes out has to agree
    // with where the player was pointing.
    private static float[] BreathFanAngles(int count, float step)
    {
        if (count <= 1) return new float[] { 0f };

        var angles = new float[count];
        float start = -step * (count - 1) * 0.5f;
        for (int i = 0; i < count; i++) angles[i] = start + step * i;
        return angles;
    }

    // Firebrand (Ember Order): every swing lobs fire along the shield facing — one
    // fireball at ranks I and II, two at III — and it comes down and bursts a
    // short step out (0.5u, then 1.5u from rank II). See EmberBoost.SetFirebrand.
    //
    // Slow on purpose: a quarter of the old 7 u/s, so the lob reads as an arc the
    // eye can follow rather than a shot (owner's call, 2026-09-25). The lifetime
    // is only a backstop now; a lob bursts when it has covered its distance.
    private const float FirebrandSpeed = 1.75f;
    private const float FirebrandLifetime = 4f;
    private const float FirebrandDirectDamageFactor = 1.5f;

    // Rimeblade (Frigid Order): every swing throws off a burst of cold around the
    // knight, dealing the sword's own damage to everything it catches, and then
    // the rank's frost damage on top (5 / 10 / 15).
    //
    // Every Order hangs a discipline off the sword and each one does something
    // different with it - Serpent exhales a cloud, Shadow echoes the swing, Ember
    // throws ordnance. Frigid's blade is cold iron, and it answers the thing that
    // has ALREADY closed the distance: a body that was chilled by an arrow on the
    // way in gets stopped dead at arm's length, which is the strongest single
    // moment in the Order and costs two picks from different chains to reach.
    //
    // Note what this is not: a projectile. It does not travel — it opens once,
    // one step out along the swing, and is gone. Nudging it a unit down the facing
    // rather than sitting it on the knight's own feet means the half of the circle
    // behind the knight (where nothing the sword just hit can be) stops being
    // wasted, and the burst covers the ground the blade actually swept.
    private const float RimebladeForwardOffset = 1f;

    private void TryReleaseRimeblade()
    {
        if (owningKnight == null) return;

        FrigidBoost boost = owningKnight.GetComponent<FrigidBoost>();
        if (boost == null || !boost.ShouldReleaseRimeblade()) return;

        float radius = boost.RimebladeRadius;
        Vector2 facing = shield != null ? SwingDirection : Vector2.zero;
        Vector2 center = (Vector2)owningKnight.transform.position + facing * RimebladeForwardOffset;
        int damage = EffectiveSwingDamage;
        int frost = boost.RimebladeFrostDamage;

        FrostFx.Burst(center, radius);
        // Same centre and radius as the OverlapCircleAll below, so the blue ring is
        // exactly the ground this swing's cold reached
        FrostFx.BurstRing(center, radius);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.frostChill);
        }

        // A temporary carrier so the damage lands credited to the right knight,
        // the same trick OnEnemyHit already uses for the blade itself - sword
        // objects are untagged children, so the tag has to be built here.
        GameObject carrier = new GameObject("RimebladeBurst");
        carrier.tag = owningKnight.tag + "Projectile";

        Collider2D[] caught = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < caught.Length; i++)
        {
            EnemyBase enemy = caught[i] != null ? caught[i].GetComponent<EnemyBase>() : null;
            if (enemy == null || enemy.IsDead) continue;

            // Damage first, cold second - the same order an arrow uses, and for
            // the same reason. A blow ends a freeze, so chilling first would let
            // one swing stop a body and then break its own ice on the next line.
            // This way a swing shatters what was already frozen and leaves it
            // chilled again, or stops what was already chilled. Never both.
            int blow = EquipmentBoost.ScaleHit(damage, enemy, owningKnight.tag);
            bool shatterHit = enemy.IsFrozen && boost.ShatterMultiplier > 1f;
            if (shatterHit)
            {
                blow = Mathf.CeilToInt(blow * boost.ShatterMultiplier);
            }
            // Read before the blow: TakeDamage can kill, and a corpse reports
            // neither its position usefully nor whether it was frozen.
            bool wasFrozen = enemy.IsFrozen;
            float reach = owningKnight != null
                ? Vector2.Distance(owningKnight.transform.position, enemy.transform.position)
                : 0f;
            Vector2 where = enemy.transform.position;

            enemy.TakeDamage(blow, carrier);
            // Counted only when this blow actually broke the ice.
            if (shatterHit && !enemy.IsFrozen)
            {
                PlayerStats.Increment("frigid.shattered");
                // A burst that breaks ice is a shatter like any other, so it throws
                // Shatter's frost blast too. That blast only chills, so it cannot
                // set off another one.
                boost.ReleaseShatterBlast(where, enemy);
            }

            // The frost rides on the blow as its own pale-blue number. Flat: Shatter
            // multiplies the blow, not this, and it never wears down ice - it is
            // cold, and cold does not break its own ice (see ApplyFrostDamage).
            if (frost > 0 && !enemy.IsDead) enemy.TakeFrostDamage(frost, owningKnight.tag);

            if (wasFrozen)
            {
                QuestTally.Total(OrderStats.FrozenTargetDamage, blow);
                if (shatterHit) QuestTally.Wave(OrderStats.ShatterDamageWaveMax, blow);
            }

            if (enemy.IsDead)
            {
                if (_inEchoPhase) QuestTally.Wave(OrderStats.PhantomKillsWaveMax);
                if (reach >= LongReach) QuestTally.Wave(OrderStats.LongRangeSwordKillsWaveMax);
            }

            // isBlow: true. This is a hit the knight swung for, so it is one of
            // the two things in the game allowed to freeze.
            //
            // wasFrozen gates it for the same reason the arrow's does: a blow into
            // ice ends the freeze and leaves the body FREE, not chilled. Freeing it
            // is what this swing bought.
            if (!wasFrozen && !enemy.IsDead) boost.TouchWithCold(enemy, true);
        }

        Destroy(carrier);
    }

    // Acid Dagger (Serpent capstone): half a second after the swing starts, a small
    // dagger jabs straight out along wherever the shield points at that moment — out,
    // a beat held, back. No arc and no sweep: one short, narrow box that only hurts on
    // the way out and while held, so it reads as a stab rather than a second swing.
    // Its point stops at two thirds of the sword's reach (Long Sword included),
    // measured off the sword's own sprite so the two can never drift apart.
    //
    // Its own coroutine rather than a step in PerformSwordSwing, because Phantom
    // Blade's echoes are still arcing at the half-second mark and the jab must neither
    // wait for them nor share their sprites.
    private const float AcidDaggerDelay = 0.5f;
    private const float AcidDaggerReachFraction = 0.66f;
    private const float AcidDaggerStartFraction = 0.3f; // point starts tucked in by the knight
    private const float AcidDaggerOutSeconds = 0.06f;
    private const float AcidDaggerHoldSeconds = 0.06f;
    private const float AcidDaggerBackSeconds = 0.06f;

    // The poison it leaves is a Venom Tip arrow's (see PoisonProjectile), bent by
    // the tick bonus the same way
    private const int AcidDaggerPoisonDamage = 2;
    private const float AcidDaggerPoisonSeconds = 30f;
    private const float AcidDaggerPoisonTickRate = 1f;

    // The blade's box in the dagger's own space, measured off acid_dagger.aseprite
    // (9x17 at 32 PPU, centre pivot): the ten rows above the guard, five pixels
    // across. The guard and grip never hurt anything.
    private static readonly Vector2 AcidDaggerBladeSize = new Vector2(5f / 32f, 10f / 32f);
    private static readonly Vector2 AcidDaggerBladeCenter = new Vector2(0f, 3.5f / 32f);
    private const float AcidDaggerTipOffset = 8.5f / 32f; // pivot to the point

    // The stock sword's point, for when its sprite cannot be measured
    private const float FallbackSwordTipReach = 1.375f;

    // Enemy colliders are triggers, so the filter has to ask for them (as ShieldSight does)
    private static readonly Collider2D[] DaggerHits = new Collider2D[32];
    private static readonly ContactFilter2D DaggerFilter = new ContactFilter2D { useTriggers = true };
    private Transform _daggerTransform;
    private SpriteRenderer _daggerRenderer;

    private void TryQueueAcidDagger()
    {
        if (owningKnight == null || shield == null) return;

        PoisonTipBoost boost = owningKnight.GetComponent<PoisonTipBoost>();
        if (boost == null || !boost.AcidDagger) return;

        StartCoroutine(AcidDaggerJab(boost, _backSwing));
    }

    private IEnumerator AcidDaggerJab(PoisonTipBoost boost, bool backSwing)
    {
        yield return new WaitForSeconds(AcidDaggerDelay);
        if (shield == null || owningKnight == null) yield break;

        EnsureDagger(boost);

        // Locked at the moment it leaves — a jab is a straight line, so it does not
        // follow the shield round while it is out. Behind the shield if the swing
        // that carried it was a back-swing.
        float angle = shield.CurrentAngle + (backSwing ? 180f : 0f);
        float radians = angle * Mathf.Deg2Rad;
        Vector3 facing = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);

        float swordTip = SwordTipReach();
        float tipOffset = AcidDaggerTipOffset * _daggerTransform.localScale.y;
        float from = swordTip * AcidDaggerStartFraction - tipOffset;
        float to = swordTip * AcidDaggerReachFraction - tipOffset;

        _daggerTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        _daggerTransform.localPosition = facing * from + rotationOffset;
        _daggerTransform.gameObject.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.swordSwing);

        var struck = new HashSet<EnemyBase>();
        int damage = boost.AcidDaggerDamage;

        // Out fast and settling at the end of the reach
        float elapsed = 0f;
        while (elapsed < AcidDaggerOutSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / AcidDaggerOutSeconds);
            float eased = 1f - (1f - progress) * (1f - progress);
            _daggerTransform.localPosition = facing * Mathf.Lerp(from, to, eased) + rotationOffset;
            StrikeWithDagger(boost, damage, struck);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < AcidDaggerHoldSeconds)
        {
            elapsed += Time.deltaTime;
            StrikeWithDagger(boost, damage, struck);
            yield return null;
        }

        // Drawn back harmlessly
        elapsed = 0f;
        while (elapsed < AcidDaggerBackSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / AcidDaggerBackSeconds);
            _daggerTransform.localPosition = facing * Mathf.Lerp(to, from, progress) + rotationOffset;
            yield return null;
        }

        _daggerTransform.gameObject.SetActive(false);
    }

    // Built once, beside the blade, and drawn like it: same sorting, same material so
    // the same lights fall on it, and the same scale so its pixels are the sword's size
    private void EnsureDagger(PoisonTipBoost boost)
    {
        if (_daggerTransform == null)
        {
            var dagger = new GameObject("AcidDagger");
            dagger.SetActive(false);
            _daggerTransform = dagger.transform;
            _daggerTransform.SetParent(transform, false);
            _daggerRenderer = dagger.AddComponent<SpriteRenderer>();

            var swordRenderer = swordSpriteTransform != null
                ? swordSpriteTransform.GetComponent<SpriteRenderer>()
                : null;
            if (swordRenderer != null)
            {
                _daggerRenderer.sharedMaterial = swordRenderer.sharedMaterial;
                _daggerRenderer.sortingLayerID = swordRenderer.sortingLayerID;
                _daggerRenderer.sortingOrder = swordRenderer.sortingOrder;
            }
        }

        _daggerTransform.localScale = _bladeBaseScale;
        _daggerRenderer.sprite = boost.AcidDaggerSprite;
    }

    // How far from the swing's centre the sword's point reaches: the arc radius plus
    // the blade above its pivot, both stretched by Long Sword exactly as
    // AnimateSwingArc stretches them
    private float SwordTipReach()
    {
        // Matches the pinned-hilt arc in AnimateSwingArc: the 1 is the fixed radius,
        // and only the blade's own length past that point is stretched by BladeReach.
        var swordRenderer = swordSpriteTransform != null
            ? swordSpriteTransform.GetComponent<SpriteRenderer>()
            : null;
        if (swordRenderer == null || swordRenderer.sprite == null)
        {
            return 1f + (FallbackSwordTipReach - 1f) * BladeReach;
        }
        return 1f + BladeReach * swordRenderer.sprite.bounds.max.y * _bladeBaseScale.y;
    }

    private void StrikeWithDagger(PoisonTipBoost boost, int damage, HashSet<EnemyBase> struck)
    {
        Vector2 center = _daggerTransform.TransformPoint(AcidDaggerBladeCenter);
        Vector3 scale = _daggerTransform.lossyScale;
        Vector2 size = new Vector2(AcidDaggerBladeSize.x * Mathf.Abs(scale.x),
                                   AcidDaggerBladeSize.y * Mathf.Abs(scale.y));

        int count = Physics2D.OverlapBox(center, size, _daggerTransform.eulerAngles.z, DaggerFilter, DaggerHits);
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = DaggerHits[i] != null ? DaggerHits[i].GetComponent<EnemyBase>() : null;
            if (enemy == null || enemy.IsDead || !struck.Add(enemy)) continue;
            LandDagger(enemy, boost, damage);
        }
    }

    private void LandDagger(EnemyBase enemy, PoisonTipBoost boost, int damage)
    {
        string knightTag = owningKnight.tag;

        // Credited like the blade: the sword objects are untagged, so the carrier
        // wears the knight's projectile tag for the length of the hit
        GameObject carrier = new GameObject("AcidDaggerHit");
        carrier.tag = knightTag + "Projectile";
        enemy.TakeDamage(EquipmentBoost.ScaleHit(damage, enemy, knightTag), carrier);
        Destroy(carrier);

        // The capstone quest asks for kills the DAGGER made, not kills the swing
        // that carried it made, so this is counted here rather than in the strike.
        if (enemy.IsDead) QuestTally.Total(OrderStats.AcidDaggerKills);

        // Damage first, venom second — the order an arrow uses — so a jab that kills
        // leaves nothing on the body and is not counted as a venom kill
        if (!enemy.IsDead)
        {
            enemy.ApplyPoisonFromTag(
                AcidDaggerPoisonDamage + boost.TickDamageBonus,
                AcidDaggerPoisonSeconds,
                AcidDaggerPoisonTickRate,
                knightTag);
        }
    }

    private void TryHurlFirebrand()
    {
        if (owningKnight == null || shield == null) return;

        EmberBoost boost = owningKnight.GetComponent<EmberBoost>();
        if (boost == null || boost.FireballPrefab == null || !boost.ShouldHurlFirebrand()) return;

        // One fireball goes straight down the facing; two straddle it. Driven off
        // the count rather than off the rank so the spread and the tier can never
        // disagree — a single fireball lobbed at -15 degrees would look like a miss.
        float[] angles = boost.FirebrandCount >= 2
            ? new float[] { -15f, 15f }
            : new float[] { 0f };

        Vector2 facing = SwingDirection;
        Vector2 origin = (Vector2)owningKnight.transform.position + facing * 1.1f;
        int directDamage = Mathf.Max(1, Mathf.RoundToInt(EffectiveSwingDamage * FirebrandDirectDamageFactor));

        foreach (float angle in angles)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * facing;
            FireballProjectile lob = FireballProjectile.Spawn(boost.FireballPrefab, origin, direction,
                FirebrandSpeed, directDamage, EffectiveSwingDamage, boost.FirebrandBlastRadius, boost,
                owningKnight.tag, FirebrandLifetime, boost.FirebrandLobDistance);
            if (lob != null)
            {
                lob.craterRadius = EmberBoost.FirebrandCraterRadius;
                lob.craterDuration = EmberBoost.FirebrandCraterDuration;
            }
        }
    }

    private void AddDamageDetection(Transform spriteTransform)
    {
        if (spriteTransform == null) return;
        Collider2D collider = spriteTransform.GetComponent<Collider2D>();
        if (collider == null)
            collider = spriteTransform.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        SwordDamageDetector damageDetector = spriteTransform.gameObject.AddComponent<SwordDamageDetector>();
        damageDetector.Initialize(this, swingDamage);
    }

    // Set by whichever swing phase is currently arcing; read at the end of each
    private bool _phaseLanded;

    // True only while a PHANTOM echo is arcing, so a kill can tell whether the
    // real blade or its shadow made it. The Shadow quest asks for six from the
    // echoes specifically, and the real swing must not be allowed to pay it.
    private bool _inEchoPhase;

    /// <summary>
    /// How far from the knight a body has to fall for the blow to count as long
    /// reach. Just past the unupgraded swing, so it is a Long Sword question:
    /// without the Order's reach the sword simply cannot land out here.
    /// </summary>
    private const float LongReach = 2.55f;
    private int _phasesLanded;

    /// <summary>
    /// Whether the trigger or stick click swings this blade. Only the tutorial lowers it. Lives on
    /// the sword object rather than the knight, so reach it with
    /// GetComponentsInChildren — ShieldOrbit builds the sword at runtime.
    /// See PlayerShooter.InputEnabled.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// Raised the moment a swing lands: the enemy's instance id, and whether that
    /// blow was the one that killed it. An id and not the object because the kill
    /// may already have destroyed it. The tutorial listens so it can tell a sword
    /// kill from an arrow kill and hold the player to the one it asked for.
    /// </summary>
    public static event System.Action<int, bool> OnSwordLanded;

    public void OnEnemyHit(GameObject enemy)
    {
        if (damagedEnemies.Contains(enemy)) return;
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            // Shatter (Frigid): the blade breaks ice like anything else the knight
            // lands. Asked before the damage, because landing it is what breaks it.
            FrigidBoost frigid = owningKnight != null ? owningKnight.GetComponent<FrigidBoost>() : null;
            bool frigidShatter = frigid != null && enemyBase.IsFrozen && frigid.ShatterMultiplier > 1f;

            GameObject tempProjectile = new GameObject("SwordHit");
            // Credit the OWNING knight: the sword object itself is untagged, so
            // using its own tag produced "UntaggedProjectile" and sword kills
            // fed the wrong knight's special
            tempProjectile.tag = (owningKnight != null ? owningKnight.tag : gameObject.tag) + "Projectile";

            // Equipment banes apply to the sword too — an item that says vermin
            // take more damage would read as broken if only arrows honoured it
            int swingDamage = EquipmentBoost.ScaleHit(currentSwingDamage, enemyBase,
                                                      owningKnight != null ? owningKnight.tag : null);

            if (frigidShatter)
            {
                swingDamage = Mathf.CeilToInt(swingDamage * frigid.ShatterMultiplier);
            }

            int enemyId = enemy.GetInstanceID();
            Vector3 hitPoint = enemyBase.transform.position;
            enemyBase.TakeDamage(swingDamage, tempProjectile);

            // The burst and tally belong to the blow that actually broke the ice.
            if (frigidShatter && !enemyBase.IsFrozen)
            {
                PlayerStats.Increment("frigid.shattered");
                // The same frost blast an arrow's shatter throws. A blade used to
                // break ice with a puff and nothing else.
                frigid.ReleaseShatterBlast(hitPoint, enemyBase);
            }

            // "A safe distance" (Guardian): a kill landed at the sword's own furthest
            // reach. Only Long Sword stretches that far — see LongReach.
            if (enemyBase.IsDead && owningKnight != null
                && Vector2.Distance(owningKnight.transform.position, hitPoint) >= LongReach)
            {
                QuestTally.Wave(OrderStats.LongRangeSwordKillsWaveMax);
            }

            OnSwordLanded?.Invoke(enemyId, enemyBase.IsDead);
            Destroy(tempProjectile);
            damagedEnemies.Add(enemy);
            _phaseLanded = true;
        }
    }

    void OnDisable()
    {
        // A jab cut off mid-thrust stops its coroutine with it, and would otherwise
        // come back standing in the air the next time this is enabled
        if (_daggerTransform != null) _daggerTransform.gameObject.SetActive(false);
        // The bar lives on the knight, not here, so it would otherwise stay up
        // frozen part-way with nothing left to drain it
        if (shield != null) shield.SetSwordReloadBarVisible(false);
    }

    void OnDestroy()
    {
        if (swordInputAction != null)
            swordInputAction.Disable();
        if (backSwingInputAction != null)
            backSwingInputAction.Disable();
    }
}
