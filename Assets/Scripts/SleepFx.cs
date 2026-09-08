using UnityEngine;

// The tell for a sleeping enemy: two Zs drifting up off it, over and over, for
// exactly as long as it is under. The effect owns its own lifetime - it watches
// the enemy's IsAsleep and takes itself off the moment that goes false - so
// nothing that can end sleep (the timer, a cleanse, dying) has to remember to
// clean up after it.
public static class SleepFx
{
    private const string HolderName = "SleepZs";

    // Set by SleepingDartUpgrade when a knight takes it. Static because the Zs
    // are raised from inside EnemyBase, which has no business knowing which
    // knight's upgrade caused them - and the dart is the only thing in the game
    // that can put anything to sleep.
    private static Sprite _zSprite;

    public static void SetZSprite(Sprite sprite)
    {
        if (sprite != null) _zSprite = sprite;
    }

    internal static Sprite ZSprite => _zSprite;

    /// <summary>The tell over a sleeping enemy. Lasts as long as the sleep does.</summary>
    public static void ShowZs(GameObject enemy)
    {
        var target = enemy != null ? enemy.GetComponent<EnemyBase>() : null;
        if (target == null) return;

        Attach(enemy, new Vector3(0.25f, 0.65f, 0f), 1f,
               () => target != null && target.IsAsleep);
    }

    /// <summary>
    /// The tell on the KNIGHT'S shield saying the next arrow is the dart, the
    /// same promise Ember's smouldering shield makes about the fireball. Same
    /// Zs as the enemy wears, drawn smaller and tighter to the shield face so
    /// the two readings are obviously the same thing: cause, then effect.
    /// </summary>
    public static void ShowShieldTell(GameObject shield, SleepBoost boost)
    {
        if (shield == null || boost == null) return;

        Attach(shield, new Vector3(0f, 0.3f, 0f), 0.6f,
               () => boost != null && boost.NextShotIsDart);
    }

    private static void Attach(GameObject host, Vector3 localOffset, float scale,
                               System.Func<bool> keepShowing)
    {
        if (host == null) return;
        if (host.transform.Find(HolderName) != null) return;

        var holder = new GameObject(HolderName);
        holder.transform.SetParent(host.transform, false);
        holder.transform.localPosition = localOffset;
        holder.AddComponent<SleepZs>().Bind(host, scale, keepShowing);
    }

    public static void HideZs(GameObject host)
    {
        if (host == null) return;
        var holder = host.transform.Find(HolderName);
        if (holder != null) Object.Destroy(holder.gameObject);
    }
}

public class SleepZs : MonoBehaviour
{
    private const float RiseSpeed = 0.6f;
    private const float Travel = 0.55f;      // world units before a Z restarts
    private const float Stagger = 0.5f;      // seconds between the two Zs

    private GameObject _host;
    private float _scale = 1f;
    private System.Func<bool> _keepShowing;
    private readonly Transform[] _zs = new Transform[2];
    private readonly float[] _phase = new float[2];

    public void Bind(GameObject host, float scale, System.Func<bool> keepShowing)
    {
        _host = host;
        _scale = scale;
        _keepShowing = keepShowing;
    }

    private void Start()
    {
        Sprite sprite = SleepFx.ZSprite;

        for (int i = 0; i < _zs.Length; i++)
        {
            var z = new GameObject($"Z{i}");
            z.transform.SetParent(transform, false);

            var renderer = z.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            // Sits with the enemy it belongs to, one step in front so it is never
            // swallowed by the body it is drawn over
            var hostRenderer = _host != null ? _host.GetComponentInChildren<SpriteRenderer>() : null;
            if (hostRenderer != null)
            {
                renderer.sortingLayerID = hostRenderer.sortingLayerID;
                renderer.sortingOrder = hostRenderer.sortingOrder + 1;
            }

            _zs[i] = z.transform;
            // Deterministic offsets, not random: the pair always reads as one
            // trailing the other rather than as noise
            _phase[i] = i * Stagger * RiseSpeed;
        }
    }

    private void Update()
    {
        // Gone, dead, awake, or the dart has been spent - the tell goes with it
        if (_keepShowing == null || !_keepShowing())
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < _zs.Length; i++)
        {
            if (_zs[i] == null) continue;

            _phase[i] += RiseSpeed * Time.deltaTime;
            float t = Mathf.Repeat(_phase[i], Travel);
            float progress = t / Travel;

            _zs[i].localPosition = new Vector3(progress * 0.18f * _scale, t * _scale, 0f);
            _zs[i].localScale = Vector3.one * _scale * Mathf.Lerp(0.7f, 1.1f, progress);

            var renderer = _zs[i].GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color c = renderer.color;
                // Fades out over the top third of the climb
                c.a = Mathf.Clamp01(1f - Mathf.InverseLerp(0.65f, 1f, progress));
                renderer.color = c;
            }
        }
    }
}
