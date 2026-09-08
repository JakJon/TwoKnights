using UnityEngine;

// Rides on an ordinary arrow and turns it into a sleeping dart: it wears the
// dart sprite and, on the enemy it hits, starts the sleep. Added at spawn time
// by PlayerShooter rather than living on a prefab of its own, which is what lets
// the dart inherit every other thing the shot already had - damage bonuses,
// poison tips, the owner's equipment - instead of being a second projectile that
// quietly missed out on all of them.
public class SleepDartProjectile : MonoBehaviour
{
    private float _sleepSeconds = 5f;

    public void Configure(float sleepSeconds, Sprite dartSprite)
    {
        _sleepSeconds = Mathf.Max(0.1f, sleepSeconds);

        // The sprite travels with the upgrade asset (same as the shuriken and
        // the fireball) rather than being loaded by name: an .aseprite sprite
        // reference has to be set through the AssetDatabase, so the asset is the
        // only place it can honestly live.
        if (dartSprite == null) return;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = dartSprite;
    }

    public void ApplySleepToEnemy(EnemyBase enemy)
    {
        if (enemy == null) return;
        enemy.Sleep(_sleepSeconds);
    }
}
