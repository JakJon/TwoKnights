using UnityEngine;

// The Overseer's crimson cart (owner, 2026-10-19). Three loops of them wall the
// knights in, and the Overseer rides outside all three.
//
// Most of the time nothing touches one: no damage from anything, no chill, no
// freeze, no sleep. Every hit prints a 0, and that 0 plus the crimson is the
// whole tell — the player is never left wondering why a cart did not break.
// Arrows still stop on it, which is what makes the loops walls.
//
// Then a blue gnome dies, and for a short window every crimson cart on the board
// is an ordinary grey empty cart: same 20 hit points, breakable by anything that
// breaks an empty cart. The window's last few seconds blink crimson so the
// player can see it closing. The window is one clock shared by every crimson
// cart, so a cart that arrives during it arrives grey.
//
// The picture is swapped rather than the Animator: the cart keeps the ordinary
// grey cart's rolling clip, and each frame the sprite it put up is traded for
// the crimson copy of the same frame. One clip, two palettes, no restarting
// the wheels every time the colour flips.
public class EnemyCrimsonCart : EnemyMineCart
{
    [Header("Crimson")]
    [Tooltip("The ordinary grey cart's frames, in clip order — what the Animator puts up.")]
    [SerializeField] private Sprite[] greyFrames;

    [Tooltip("The same frames in crimson, in the same order.")]
    [SerializeField] private Sprite[] crimsonFrames;

    [Tooltip("Seconds per flash while the window is closing")]
    [SerializeField] private float blinkInterval = 0.25f;

    [Tooltip("The 0 printed over a hit that did nothing")]
    [SerializeField] private Color zeroColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    // ---- the window, shared by every crimson cart ----

    private static float _softUntil = -1f;
    private static float _blinkFrom = -1f;

    // Statics outlive a Play session when domain reload is off, while Time.time
    // starts again from zero — an old deadline would open the next run's carts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _softUntil = -1f;
        _blinkFrom = -1f;
        WindowOpenedAt = -1f;
    }

    /// <summary>When the latest window opened — the moment its blue gnome died — or -1.</summary>
    public static float WindowOpenedAt { get; private set; } = -1f;

    /// <summary>When the latest window shuts (or shut), or -1 for none yet.</summary>
    public static float WindowEndsAt => _softUntil;

    /// <summary>
    /// Every crimson cart turns grey and breakable for <paramref name="seconds"/>,
    /// blinking crimson for the last <paramref name="blinkSeconds"/> of it. A
    /// second call while one is open starts the clock again.
    /// </summary>
    public static void OpenWindow(float seconds, float blinkSeconds)
    {
        seconds = Mathf.Max(0f, seconds);
        WindowOpenedAt = Time.time;
        _softUntil = Time.time + seconds;
        _blinkFrom = _softUntil - Mathf.Clamp(blinkSeconds, 0f, seconds);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CrimsonCart] Window open at t={Time.time:F2} until t={_softUntil:F2} " +
                  $"(blinking from t={_blinkFrom:F2}).");
#endif
    }

    /// <summary>Shut it now. Every fight that uses these starts with this.</summary>
    public static void CloseWindow()
    {
        _softUntil = -1f;
        _blinkFrom = -1f;
        WindowOpenedAt = -1f;
    }

    /// <summary>True while crimson carts are grey and can be broken.</summary>
    public static bool WindowOpen => Time.time < _softUntil;

    /// <summary>Every crimson cart still on the track. Read by the Overseer's ambushes.</summary>
    public static int LiveCount { get; private set; }

    // ---- what being crimson means ----

    public override bool ImmuneToCold => !WindowOpen;
    public override bool ImmuneToSleep => !WindowOpen;
    protected override bool RefusesHits => !WindowOpen;

    // A returned bomb breaks an empty cart, and while the window is open this is one
    public override bool BrokenByReturnedBomb => WindowOpen;

    private bool _counted;
    private bool _wasOpen;

    protected override void Awake()
    {
        base.Awake();
        LiveCount++;
        _counted = true;
    }

    protected virtual void OnDestroy()
    {
        if (_counted) LiveCount = Mathf.Max(0, LiveCount - 1);
        _counted = false;
    }

    public override void TakeDamage(int damage, GameObject projectile, int holyPortion = 0)
    {
        if (isDead) return;

        if (!WindowOpen)
        {
            ShowDamageText(0, zeroColor);
            return;
        }

        base.TakeDamage(damage, projectile, holyPortion);
    }

    // Broken during a window it is an empty cart broken, and counts as one
    protected override void OnDeath()
    {
        base.OnDeath();
        QuestTally.Total(MineStats.EmptyCartsBroken);
    }

    private void Update()
    {
        bool open = WindowOpen;

        // Hard again: whatever the window let on — ice, a dart — comes off with
        // it, because a crimson cart cannot be frozen or put to sleep
        if (_wasOpen && !open)
        {
            if (IsFrozen) PurgeFrost();
            if (IsAsleep) WakeUp();
        }
        _wasOpen = open;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (spriteRenderer == null) return;

        Sprite wanted = Swap(spriteRenderer.sprite, ShowCrimson());
        if (wanted != null && wanted != spriteRenderer.sprite) spriteRenderer.sprite = wanted;
    }

    private bool ShowCrimson()
    {
        if (!WindowOpen) return true;
        if (Time.time < _blinkFrom) return false;

        // Crimson first, so the first thing the closing window does is show the
        // colour it is about to go back to
        int flash = Mathf.FloorToInt((Time.time - _blinkFrom) / Mathf.Max(0.05f, blinkInterval));
        return flash % 2 == 0;
    }

    // The same frame in the palette asked for. A sprite in neither list is left
    // alone — a flash material or a hurt frame is not ours to repaint.
    private Sprite Swap(Sprite current, bool crimson)
    {
        if (current == null || greyFrames == null || crimsonFrames == null) return null;

        int count = Mathf.Min(greyFrames.Length, crimsonFrames.Length);
        for (int i = 0; i < count; i++)
        {
            if (current == greyFrames[i] || current == crimsonFrames[i])
            {
                return crimson ? crimsonFrames[i] : greyFrames[i];
            }
        }
        return null;
    }
}
