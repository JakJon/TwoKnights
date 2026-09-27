using UnityEngine;

// Bolted onto a fireball at spawn (same pattern as PoisonProjectile): the prefab
// carries the sprite and collider identity, this carries the per-shot state.
//
// A fireball is a SANCTIONED IGNITION SOURCE — one of only two, alongside an arrow
// that rolled ignite. See Docs/Design/ember-order.md.
public class FireballProjectile : MonoBehaviour
{
    public float blastRadius = 1.5f;
    public int blastDamage = 10;

    // Dawn's share of blastDamage, for the split number over the body. Already
    // inside blastDamage — display only, exactly as PlayerProjectile.holyDamage is.
    public int blastHolyDamage;

    // The burning ground it leaves. Firebrand's lobs leave a smaller, shorter one
    // than a shot fireball; see EmberBoost.FirebrandCraterRadius.
    public float craterRadius = EmberBoost.CraterRadius;
    public float craterDuration = EmberBoost.CraterDuration;

    public EmberBoost ownerBoost;
    public string ownerTag; // "PlayerLeft" / "PlayerRight"

    private bool _exploded;
    private bool _burnedOut;

    // Standalone spawn for fireballs that aren't the main shot — Firebrand's sword
    // toss. PlayerShooter configures its own in place, since there the fireball
    // replaces the arrow rather than being an extra projectile.
    //
    // lobDistance above zero makes it a LOB: it comes down and bursts after
    // travelling that far, whether or not it met anything on the way. See BeginLob.
    public static FireballProjectile Spawn(GameObject prefab, Vector2 position, Vector2 direction, float speed,
        int directDamage, int blastDamage, float blastRadius, EmberBoost ownerBoost,
        string ownerTag, float lifetime, float lobDistance = 0f)
    {
        if (prefab == null) return null;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        go.tag = ownerTag + "Projectile";

        Rigidbody2D body = go.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = direction.normalized * speed;
        }

        PlayerProjectile projectile = go.GetComponent<PlayerProjectile>();
        if (projectile != null)
        {
            projectile.damage = directDamage;
            projectile.ignitesOnHit = true;
        }

        FireballProjectile fireball = go.AddComponent<FireballProjectile>();
        fireball.blastRadius = blastRadius;
        fireball.blastDamage = Mathf.Max(1, blastDamage);
        fireball.ownerBoost = ownerBoost;
        fireball.ownerTag = ownerTag;
        if (lobDistance > 0f) fireball.BeginLob(lobDistance);

        AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballLaunch);

        // Not Destroy(go, lifetime): a timed destroy is indistinguishable from any
        // other, and OnDestroy below has to be able to tell burning out from
        // hitting something
        fireball.StartCoroutine(fireball.BurnOutAfter(lifetime));
        return fireball;
    }

    // ---- the lob (Firebrand) ----
    //
    // A Firebrand fireball is lobbed rather than thrown (owner's call, 2026-09-25):
    // it covers a short, fixed distance, comes down and bursts. The arc is DRAWN,
    // not simulated. The body travels flat along the ground, so it still collides
    // exactly where it is drawn; the sprite swells toward the top of the arc and
    // shrinks back as it falls, and a dark shadow drops away beneath it and meets
    // it again at the landing, which is where the burst happens.

    /// <summary>The sprite's size at the top of the arc, against its size on the ground.</summary>
    private const float LobApexScale = 1.5f;

    /// <summary>How far the shadow sits below the fireball at the top of the arc, per
    /// unit of lob, with a floor so the shortest lob still visibly leaves the ground.</summary>
    private const float LobHeightPerUnit = 0.5f;
    private const float LobMinHeight = 0.3f;

    /// <summary>The shadow is near-black. It is the only thing that tells a lob from
    /// a fireball that just grew.</summary>
    private const float ShadowAlpha = 0.85f;

    /// <summary>The shadow's width against the fireball sprite's, and how much it
    /// shrinks at the top of the arc (further from the ground, a smaller shadow).</summary>
    private const float ShadowWidthFactor = 0.8f;
    private const float ShadowApexScale = 0.7f;

    private float _lobDistance;
    private Vector2 _lobStart;
    private Vector3 _groundScale;
    private float _lobHeight;
    private Transform _shadow;
    private Vector3 _shadowGroundScale;

    private void BeginLob(float distance)
    {
        _lobDistance = distance;
        _lobStart = transform.position;
        _groundScale = transform.localScale;
        _lobHeight = Mathf.Max(LobMinHeight, distance * LobHeightPerUnit);
        _shadow = CreateShadow();
    }

    private void Update()
    {
        if (_lobDistance <= 0f || _exploded) return;

        float t = Mathf.Clamp01(Vector2.Distance(_lobStart, transform.position) / _lobDistance);
        float arc = Mathf.Sin(t * Mathf.PI); // 0 on the ground, 1 at the top

        transform.localScale = _groundScale * Mathf.Lerp(1f, LobApexScale, arc);

        if (_shadow != null)
        {
            _shadow.position = transform.position + Vector3.down * (_lobHeight * arc);
            _shadow.localScale = _shadowGroundScale * Mathf.Lerp(1f, ShadowApexScale, arc);
        }

        // Down. It bursts where it lands whether or not anything is standing there.
        if (t >= 1f)
        {
            Explode(null);
            Destroy(gameObject);
        }
    }

    // Its own object rather than a child: a child would turn with the fireball's
    // heading and swell with the lob, and the shadow must do neither.
    private Transform CreateShadow()
    {
        var go = new GameObject("FireballShadow");
        go.transform.position = transform.position;

        var shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = ShadowSprite;
        shadow.color = new Color(0f, 0f, 0f, ShadowAlpha);

        float width = 0.35f;
        var body = GetComponent<SpriteRenderer>();
        if (body != null)
        {
            // Just under the fireball, so the two overlap cleanly at take-off and
            // landing instead of the shadow painting over the flame
            shadow.sortingLayerID = body.sortingLayerID;
            shadow.sortingOrder = body.sortingOrder - 1;
            if (body.sprite != null)
            {
                width = body.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x) * ShadowWidthFactor;
            }
        }

        // The sprite is one unit wide and half a unit tall, so this is its width
        _shadowGroundScale = new Vector3(width, width, 1f);
        go.transform.localScale = _shadowGroundScale;
        return go.transform;
    }

    private static Sprite _shadowSprite;

    /// <summary>
    /// A hard-edged oval, 16 x 8 pixels, drawn once. Pixel-sharp to sit with the
    /// rest of the art; generated rather than imported for the same reason
    /// NpcAura's disc is — it is a flat shape, not a drawing.
    /// </summary>
    private static Sprite ShadowSprite
    {
        get
        {
            if (_shadowSprite != null) return _shadowSprite;

            const int w = 16;
            const int h = 8;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "FireballShadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                // Generated, not an asset — without this it leaks into the scene on
                // every domain reload in the editor.
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                    float dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    bool inside = dx * dx + dy * dy <= 1f;
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            // 16 pixels per unit: one unit wide, half a unit tall
            _shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
            _shadowSprite.name = "FireballShadow";
            _shadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _shadowSprite;
        }
    }

    /// <summary>
    /// The fireball reached the end of its flight without touching anything. Say
    /// so before destroying it, or OnDestroy reads the expiry as a hit and bursts
    /// it wherever it happened to be — which is off-screen, four seconds after
    /// the shot, as a bang with nothing behind it.
    /// </summary>
    public void MarkBurnedOut()
    {
        _burnedOut = true;
    }

    private System.Collections.IEnumerator BurnOutAfter(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        // A lob that never covered its distance — held up against something — still
        // comes down. Only a thrown fireball fizzles out.
        if (_lobDistance > 0f) Explode(null);
        else MarkBurnedOut();
        Destroy(gameObject);
    }

    /// <summary>
    /// Anything that takes a fireball off the board sets it off. A fireball is
    /// not an arrow — it is a thing carrying a blast, a crater and an ignition,
    /// and every one of those is owed to the player whether the shot ended on a
    /// rat, a pickaxe knocked out of the air, a bomb, a slime's ward, or an orb.
    ///
    /// It lives in OnDestroy rather than in a collision handler because the
    /// fireball is almost never the thing that ends the collision: a pickaxe
    /// destroys the arrow that hit it, a bomb does, a cart does, a sonar wave
    /// does, and not one of them knows or should know what it just swallowed. The
    /// only place that can be sure of catching all of them — and the ones nobody
    /// has written yet — is the moment the object goes.
    ///
    /// The direct-hit path in PlayerProjectile still calls Explode() itself, so
    /// the enemy it landed on is correctly left out of its own blast; this only
    /// ever fires for the endings nothing else claimed.
    /// </summary>
    private void OnDestroy()
    {
        // The lob's shadow is its own object, so it has to be taken down with the
        // fireball however the fireball went — a hit, a landing, a sweep.
        if (_shadow != null) Destroy(_shadow.gameObject);

        if (_exploded || _burnedOut) return;

        // Leaving play, changing scene, or the editor stopping: the whole board is
        // being torn down, and a blast that spawns a crater and a particle burst
        // in the middle of it is at best noise and at worst an error per fireball
        if (!Application.isPlaying || !gameObject.scene.isLoaded) return;

        Explode(null);
    }

    // directHit is excluded from the blast: it already ate the direct-hit damage,
    // and paying it both would make a point-blank fireball land 2.5x instead of 1.5x
    public void Explode(EnemyBase directHit)
    {
        // Per fireball that went off, once. A run's worth is the ask, so this is
        // a run tally rather than a lifetime total.
        QuestTally.Run(OrderStats.FireballRunMax);

        if (_exploded) return;
        _exploded = true;

        AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballExplode);

        Vector2 center = transform.position;

        // Damage covers a hair past the drawn blast so enemies at the visible edge
        // aren't skipped; FireFx.Burst below keeps the graphic at blastRadius.
        foreach (var col in Physics2D.OverlapCircleAll(center, blastRadius * FireField.HitboxScale))
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || enemy == directHit || enemy.IsDead) continue;

            // Splash, so iron shrugs it off — a cart is only answered by an aimed
            // shot, and the direct hit above is exempt from this check
            if (enemy.ImmuneToAreaDamage) continue;

            int dealt = EquipmentBoost.ScaleHit(blastDamage, enemy, ownerTag);
            int holyShare = (blastHolyDamage > 0 && blastDamage > 0)
                ? Mathf.Clamp(Mathf.RoundToInt(dealt * (float)blastHolyDamage / blastDamage), 0, dealt)
                : 0;
            enemy.TakeDamage(dealt, gameObject, holyShare);
            enemy.Ignite(ownerTag);
        }

        // The crater: this is the fireball's real contribution to the Order. Burning
        // ground for twice as long as it used to burn, and ground that lights NOTHING.
        // The blast above still ignites everything it catches, because that is the
        // fireball itself landing on them; what the mark it leaves does is deal damage
        // to whatever stands in it, for twelve seconds, or forever under Scorched
        // Earth.
        if (ownerBoost != null)
        {
            ownerBoost.PlaceZone(center, craterRadius, craterDuration);
        }

        // markRadius: the fire is bounded to blastRadius and a ring is drawn on
        // it. Ember's whole pitch is "this covers ground", and the player cannot
        // price that against a keg or a bomb unless all three explosions state
        // their reach the same way.
        FireFx.Burst(center, blastRadius, markRadius: true);
    }
}
