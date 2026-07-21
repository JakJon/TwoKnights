using UnityEngine;

// The knight's Shadow stat sheet: every Shadow Order upgrade writes into this
// component. PlayerShooter reads it when arrows fire (Shuriken Fan), projectiles
// read it on hit (Killing Blow), SwordSwing reads it per swing (Phantom Blade),
// and it listens for kill credit itself to open Thousand Cuts windows.
public class NinjaBoost : MonoBehaviour
{
    private int shurikenLevel = 0; // Shuriken Fan: 1 = pair at ±12°, 2 = + pair at ±24°
    private GameObject shurikenPrefab; // set by ShurikenFanUpgrade; PlayerShooter falls back to the arrow prefab when null
    private float executeThreshold = 0f; // Killing Blow: execute below this fraction of max HP
    private int phantomLevel = 0; // Phantom Blade: number of echo swings
    private bool thousandCuts = false; // Capstone: kills erase the firing cooldown

    private const float ThousandCutsWindow = 1f;
    // Firing floor during a Thousand Cuts window (seconds between shots).
    // Playtesting keeps pulling this down: 0.08 → 0.16 → 0.32 → 0.43
    // (25% slower rate), window 2s → 1s.
    private const float ThousandCutsFloor = 0.43f;

    private PlayerShooter shooter;

    private void Awake()
    {
        shooter = GetComponent<PlayerShooter>();
    }

    private void OnEnable()
    {
        EnemyBase.OnEnemyKilledBy += HandleKillCredit;
    }

    private void OnDisable()
    {
        EnemyBase.OnEnemyKilledBy -= HandleKillCredit;
    }

    private void HandleKillCredit(string playerTag)
    {
        if (!thousandCuts || shooter == null) return;
        if (!gameObject.CompareTag(playerTag)) return;
        shooter.OpenNoCooldownWindow(ThousandCutsWindow, ThousandCutsFloor);
    }

    // Tiers set absolute values; Max keeps late re-picks from downgrading
    public void SetShurikenFan(int level, GameObject prefab)
    {
        shurikenLevel = Mathf.Max(shurikenLevel, level);
        if (prefab != null) shurikenPrefab = prefab;
    }
    public void SetExecuteThreshold(float fraction) { executeThreshold = Mathf.Max(executeThreshold, fraction); }
    public void SetPhantomLevel(int level) { phantomLevel = Mathf.Max(phantomLevel, level); }
    public void EnableThousandCuts() { thousandCuts = true; }

    public int ShurikenLevel => shurikenLevel;
    public GameObject ShurikenPrefab => shurikenPrefab;
    public float ExecuteThreshold => executeThreshold;
    public int PhantomLevel => phantomLevel;
    public bool ThousandCuts => thousandCuts;
}
