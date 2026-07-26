using System.Collections;
using UnityEngine;

// The Mine's first wave, and the proving ground for the rail system: a track
// slams into place above the knights while a slow, widely spaced projectile
// cycle keeps both shields honest. No carts yet — this wave exists to verify
// that a RailLayout builds, cascades, and clears correctly.
//
// The projectile cycle is a fixed four-position rotation (no dice — see the
// no-randomness pillar): above-left, above-right, below-left, below-right.
[CreateAssetMenu(fileName = "ChooChoo", menuName = "Waves/Choo Choo")]
public class ChooChoo : BaseWave
{
    [Header("Track")]
    [Tooltip("Laid the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private RailLayout railLayout;

    [Header("Projectiles")]
    [Tooltip("Seconds between shots")]
    [SerializeField] private float projectileInterval = 5f;
    [Tooltip("Length of the firing window. Shots land at 0s, then every interval up to and including this.")]
    [SerializeField] private float projectileWindow = 15f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        var rails = spawner.Rails;
        if (rails != null)
        {
            // Fire-and-forget: the cascade runs on the network's own clock so
            // the projectile cycle starts immediately alongside it
            rails.Lay(railLayout);
        }
        else if (railLayout != null)
        {
            Debug.LogWarning("[ChooChoo] No RailNetwork in the scene — the track will not appear.");
        }

        float interval = Mathf.Max(0.1f, projectileInterval);
        int shots = Mathf.FloorToInt(projectileWindow / interval) + 1;

        for (int i = 0; i < shots; i++)
        {
            SpawnShot(spawner, i);
            if (i < shots - 1) yield return new WaitForSeconds(interval);
        }

        MarkSpawningComplete();
        yield return null;
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
