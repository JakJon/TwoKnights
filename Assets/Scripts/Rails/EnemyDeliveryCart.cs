using System.Collections.Generic;
using UnityEngine;

// A cart hauling something alive under a grate, bound for a flagged spot on the
// track.
//
// This is the piece that makes the rail matter to the rest of the game. Every
// other cart is a thing the mine does TO you; this one is logistics. The flag
// tells you where the cargo is going to be let out and the track tells you how
// long you have to stop it, so the whole route is a race you can see the shape
// of from the moment the cart appears: shoot it before the flag and nothing ever
// arrives, miss the window and you fight what it brought, on its terms, at the
// far end of the board.
//
// The cargo is authored, never rolled — same wave, same passengers, always (see
// the no-randomness pillar). What varies is whether the player got there first.
public class EnemyDeliveryCart : EnemyMineCart
{
    public enum Cargo
    {
        BrownRat = 0,
        GreyRat = 1,
        BlackRat = 2,
        Bat = 3,
        Slime = 4
    }

    [Header("Cargo")]
    [Tooltip("What gets let out at a flag. Authored per wave — never rolled.")]
    [SerializeField] private Cargo cargo = Cargo.GreyRat;

    [Tooltip("How many flags this cart can serve before it is empty")]
    [SerializeField] private int passengers = 1;

    [Tooltip("Slime cargo only: 1 is the smallest")]
    [SerializeField] private int slimeSize = 1;

    [Tooltip("Where the passenger lands relative to the CART, not the flag — a hair clear of the track so it isn't born inside the rail")]
    [SerializeField] private Vector2 dropOffset = new Vector2(0f, -0.9f);

    [Header("Audio")]
    [Tooltip("Held for as long as ANY loaded cart is on the track — the sound of something alive being hauled about")]
    [SerializeField] private SoundEffect haulLoop;

    [Tooltip("Played as the cargo comes out")]
    [SerializeField] private SoundEffect dropSound;

    [Header("Wreck")]
    [Tooltip("What keeps rolling once this cart is empty or dead. Needs a MineCart.")]
    [SerializeField] private GameObject wreckPrefab;

    // How many loaded carts are on the track. The loop is a property of that
    // COUNT, not of any one cart: three carts rolling at once is still one thing
    // happening, and starting a voice per cart would treble the volume of it.
    private static int _loaded;

    // A cart the player has not dealt with yet is a threat that has not happened
    // yet, so the wave must not be allowed to end while one is still in transit.
    // The moment it is empty it stops counting — see Retire.
    protected override bool TracksWaveCompletion => true;

    // The cargo is alive, so this cart is not merely iron: fire and poison are a
    // legitimate way to stop a delivery. The wreck it leaves once it is empty is
    // a plain cart again, and immune. See EnemyBase.ImmuneToAreaDamage.
    public override bool ImmuneToAreaDamage => false;

    private readonly HashSet<RailDropPoint> _served = new HashSet<RailDropPoint>();
    private Spawner _spawner;
    private int _remaining;
    private bool _counted;

    protected override void Start()
    {
        base.Start();
        _remaining = Mathf.Max(0, passengers);
        if (_remaining == 0)
        {
            Retire();
            return;
        }

        _counted = true;
        _loaded++;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLoop(haulLoop);
    }

    // Every exit — delivered out, shot, or torn down with the track between waves
    // — comes through here, which is what keeps the count honest. A loop left
    // running because one path forgot to decrement would play for the rest of the
    // run with nothing on screen to explain it.
    private void OnDestroy()
    {
        if (!_counted) return;
        _counted = false;

        _loaded = Mathf.Max(0, _loaded - 1);
        if (_loaded == 0 && AudioManager.Instance != null) AudioManager.Instance.StopLoop();
    }

    private void Update()
    {
        if (isDead || _remaining <= 0) return;
        if (Cart == null || !Cart.IsRiding) return;
        if (RailNetwork.Instance == null) return;

        var drops = RailNetwork.Instance.Drops;
        for (int i = 0; i < drops.Count; i++)
        {
            RailDropPoint drop = drops[i];
            if (drop == null || drop.LineIndex != Cart.Line.Index) continue;
            if (_served.Contains(drop)) continue;
            if (Cart.Along < drop.DistanceAlong) continue;

            _served.Add(drop);
            Unload(drop);
            if (_remaining <= 0)
            {
                Retire();
                return;
            }
        }
    }

    private void Unload(RailDropPoint drop)
    {
        _remaining--;

        Spawner spawner = ResolveSpawner();
        if (spawner == null)
        {
            Debug.LogWarning("[EnemyDeliveryCart] No Spawner in the scene — the cargo is lost.");
            return;
        }

        // Off the CART, not off the flag. The two are within a frame's travel of
        // each other, but the passenger has to look like it came out of the cart
        // that was carrying it — spawning it on the marker instead reads as the
        // flag producing enemies.
        Vector2 at = (Vector2)transform.position + dropOffset;
        Transform target = NearestKnight(at, spawner);

        if (dropSound != null && dropSound.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(dropSound);
        }

        switch (cargo)
        {
            case Cargo.BrownRat:
                spawner.SpawnRat(at, spawner.brownRat, 0f, target);
                break;
            case Cargo.GreyRat:
                spawner.SpawnRat(at, spawner.greyRat, 0f, target);
                break;
            case Cargo.BlackRat:
                spawner.SpawnRat(at, spawner.blackRat, 0f, target);
                break;
            case Cargo.Bat:
                // Bats pick their own knight, and the dark-bat cadence is counted
                // inside SpawnBat so the pattern stays the wave's, not the cart's
                spawner.SpawnBat(at, 0f);
                break;
            case Cargo.Slime:
                spawner.SpawnSlime(Mathf.Max(1, slimeSize), at, 0f, target);
                break;
        }
    }

    // Empty. It is furniture now, so it hands the ride over to a plain cart and
    // stops holding the wave open. Same swap the gnome carts do when their rider
    // dies — one frame, no break in stride.
    private void Retire()
    {
        BaseWave.UnregisterEnemy(gameObject);
        LeaveWreck();
        Destroy(gameObject);
    }

    protected override void OnDeath()
    {
        // Shot before it delivered: whatever is left in the cart never arrives.
        // That is the entire reward for winning the race.
        LeaveWreck();
        base.OnDeath();
    }

    private void LeaveWreck()
    {
        if (wreckPrefab == null || Cart == null || !Cart.IsRiding) return;

        GameObject go = Instantiate(wreckPrefab, transform.position, Quaternion.identity, transform.parent);
        MineCart wreck = go.GetComponent<MineCart>();
        if (wreck == null)
        {
            Debug.LogWarning($"[EnemyDeliveryCart] {wreckPrefab.name} has no MineCart; the wreck cannot ride on.");
            Destroy(go);
            return;
        }

        wreck.Resume(Cart.Line, Cart.Along, Cart.Speed);
        if (RailNetwork.Instance != null) RailNetwork.Instance.Adopt(wreck);
    }

    private Spawner ResolveSpawner()
    {
        if (_spawner == null) _spawner = FindFirstObjectByType<Spawner>(FindObjectsInactive.Include);
        return _spawner;
    }

    // Nearest rather than alternating: a counter carried between waves would make
    // which knight gets chased depend on what happened earlier in the run, and
    // wave content has to be reproducible on its own.
    private static Transform NearestKnight(Vector2 from, Spawner spawner)
    {
        Transform left = spawner.LeftPlayer;
        Transform right = spawner.RightPlayer;
        if (left == null) return right;
        if (right == null) return left;

        float toLeft = ((Vector2)left.position - from).sqrMagnitude;
        float toRight = ((Vector2)right.position - from).sqrMagnitude;
        return toLeft <= toRight ? left : right;
    }
}
