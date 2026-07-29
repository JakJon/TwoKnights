using System.Collections;
using UnityEngine;

// The Mine's first wave, and the proving ground for the rail system: a track
// slams into place above the knights, carts start rolling along it once it has
// settled, and a slow, widely spaced projectile cycle keeps both shields honest.
//
// Both cadences are fixed (no dice — see the no-randomness pillar): the
// projectile cycle is a four-position rotation — above-left, above-right,
// below-left, below-right — and carts go out one per line in round-robin.
[CreateAssetMenu(fileName = "ChooChoo", menuName = "Waves/Choo Choo")]
public class ChooChoo : BaseWave
{
    [Header("Track")]
    [Tooltip("Laid the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private RailLayout railLayout;

    [Header("Carts")]
    [Tooltip("Needs a MineCart component. Leave empty to run the wave as track-and-projectiles only.")]
    [SerializeField] private GameObject cartPrefab;
    [Tooltip("Seconds after the last rail piece lands before the first cart is released")]
    [SerializeField] private float firstCartDelay = 0.75f;
    [Tooltip("Seconds between carts")]
    [SerializeField] private float cartInterval = 4f;
    [Tooltip("Carts released over the wave, dealt out across the layout's runs in order")]
    [SerializeField] private int cartCount = 4;
    [Tooltip("World units per second. 0 leaves the prefab's own speed alone.")]
    [SerializeField] private float cartSpeed = 6f;

    [Header("Projectiles")]
    [Tooltip("Seconds between shots")]
    [SerializeField] private float projectileInterval = 5f;
    [Tooltip("Length of the firing window. Shots land at 0s, then every interval up to and including this.")]
    [SerializeField] private float projectileWindow = 15f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        var rails = spawner.Rails;
        float layDuration = 0f;

        if (rails != null)
        {
            // Fire-and-forget: the cascade runs on the network's own clock so
            // the projectile cycle starts immediately alongside it
            layDuration = rails.Lay(railLayout);
        }
        else if (railLayout != null)
        {
            Debug.LogWarning("[ChooChoo] No RailNetwork in the scene — the track will not appear.");
        }

        // Carts run on their own clock beside the projectiles, but the wave's
        // spawn phase still has to cover them, so hold the coroutine and join
        // it before reporting the wave spawned out
        Coroutine carts = null;
        if (rails != null && cartPrefab != null && cartCount > 0)
        {
            carts = spawner.StartCoroutine(ReleaseCarts(rails, layDuration));
        }

        float interval = Mathf.Max(0.1f, projectileInterval);
        int shots = Mathf.FloorToInt(projectileWindow / interval) + 1;

        for (int i = 0; i < shots; i++)
        {
            SpawnShot(spawner, i);
            if (i < shots - 1) yield return new WaitForSeconds(interval);
        }

        if (carts != null) yield return carts;

        MarkSpawningComplete();
        yield return null;
    }

    // One cart at a time, dealt round-robin across the layout's runs so a
    // multi-track layout fills evenly without the wave knowing its shape
    private IEnumerator ReleaseCarts(RailNetwork rails, float layDuration)
    {
        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, firstCartDelay));

        if (rails.LineCount == 0)
        {
            Debug.LogWarning("[ChooChoo] The laid layout has no runs — there is no track for a cart to ride.");
            yield break;
        }

        float interval = Mathf.Max(0.1f, cartInterval);

        for (int i = 0; i < cartCount; i++)
        {
            // Re-checked every pass: the track can be torn down under us
            // between releases, and a modulo by zero would take the wave with it
            if (rails.LineCount == 0) yield break;
            rails.SpawnCart(cartPrefab, i % rails.LineCount, cartSpeed);

            if (i < cartCount - 1) yield return new WaitForSeconds(interval);
        }
    }

    // Each shot drops straight down (or straight up) onto its own knight, so a
    // volley never crosses the other knight's guard
    private void SpawnShot(Spawner spawner, int index)
    {
        switch (index % 4)
        {
            case 0:
                spawner.SpawnProjectile(spawner.LeftPlayer, spawner.aboveLeftPlayer);
                break;
            case 1:
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.aboveRightPlayer);
                break;
            case 2:
                spawner.SpawnProjectile(spawner.LeftPlayer, spawner.belowLeftPlayer);
                break;
            default:
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.belowRightPlayer);
                break;
        }
    }
}
