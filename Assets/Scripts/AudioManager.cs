using UnityEngine;

[System.Serializable]
public class SoundEffect
{
    public AudioClip clip;
    [Range(0, 1)] public float volume = 1f;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Sound Effects")]
    public SoundEffect playerHurt;
    public SoundEffect projectileSpawn;
    public SoundEffect playerProjectile;
    // No longer played by anything — the reload cry was cut from PlayerShooter.
    // Kept wired so it can be put back without redoing the scene reference.
    public SoundEffect reload;
    public SoundEffect projectileShield;
    public SoundEffect enemyShield;
    public SoundEffect enemyPlayer;
    public SoundEffect rightMulti2;
    public SoundEffect rightMulti3;
    public SoundEffect rightMulti4;
    public SoundEffect leftMulti2;
    public SoundEffect leftMulti3;
    public SoundEffect leftMulti4;
    public SoundEffect multiFull;
    public SoundEffect healSpecial;
    // One soft chime shared by every Dawn Order effect — see PlayerHealth.PlayBlessingChime
    public SoundEffect dawnBlessing;
    public SoundEffect leftSpecial;
    public SoundEffect rightSpecial;
    public SoundEffect ratChase;
    public SoundEffect ratDeath;
    public SoundEffect ratSpawn;
    public SoundEffect ratHurt;
    public SoundEffect orbFlyBy;
    public SoundEffect orbCollect;
    public SoundEffect slimeHit;
    public SoundEffect slimeDeath;
    public SoundEffect slimeSplit;
    public SoundEffect poisoned;
    public SoundEffect swordSwing;
    public SoundEffect arrowIgnite;
    public SoundEffect fireballLaunch;
    public SoundEffect fireballExplode;
    public SoundEffect shurikenThrow;
    public SoundEffect shadowArrow;
    public SoundEffect phantomStrike;
    public SoundEffect executeFlash;
    public SoundEffect poisonBurst;
    // A vial of venom coming apart on the ground. Short, bright and quiet - it
    // fires every fourth or fifth shot of a knight who has the upgrade, so it has
    // to sit under the fight rather than announce itself the way a fireball does.
    public SoundEffect glassBreak;
    // One soft puff per burn tick, deliberately near the noise floor: fire damage
    // lands once a second on every burning body at once, so this has to sit under
    // the fight rather than in it. Globally rate-limited — see EnemyBase.PlayBurnTick.
    public SoundEffect burnTick;

    // Frigid. Three, not one per discipline: the player should learn "cold landed",
    // "it stopped" and "it broke", never five separate cues for the same Order.
    public SoundEffect frostChill;
    public SoundEffect frostFreeze;
    public SoundEffect frostShatter;

    // A knight rotting, one tick a second for twenty-five seconds. Deliberately
    // NOT the unified playerHurt: that sound is sized for a hit worth fifteen, and
    // twenty-five of them in a row would read as the knight being beaten to death
    // by something invisible. This one is a small wet bubble, and it is the only
    // damage source in the game allowed its own voice — see KnightPoison.
    public SoundEffect knightPoisonTick;
    public SoundEffect confusion;
    public SoundEffect batScreech;
    public SoundEffect batFlutter;
    public SoundEffect batHurt;
    public SoundEffect batDeath;
    public SoundEffect batBite;
    public SoundEffect sonarPing;
    public SoundEffect wolfHowl;
    public SoundEffect wolfGrowl;
    // The moment a wolf leaves its path and picks a knight. Louder and shorter
    // than the growl, because it is a warning the player has to act on.
    public SoundEffect wolfBark;
    public SoundEffect wolfHurt;
    public SoundEffect wolfDeath;
    public SoundEffect wolfBite;
    public SoundEffect slimeSpawn;
    public SoundEffect slimeSmack;
    // Crimson Twins: deeper and wetter than slimeHit/slimeDeath, which stay on the
    // ordinary slimes — the boss is several times their size and needs its own weight
    public SoundEffect giantSlimeHurt;
    public SoundEffect giantSlimeDeath;
    public SoundEffect bossBanner;
    public SoundEffect bossTelegraph;
    public SoundEffect bossFan;
    public SoundEffect bossSummon;
    public SoundEffect bossRoar;
    public SoundEffect bossHurt;
    public SoundEffect bossDeath;
    // Two blasts off a mine whistle, and the only announcement the Millstone
    // makes: it sounds when the wheel changes gear, so the players hear the ring
    // speed up on the same beat they see it
    public SoundEffect cartWhistle;
    public SoundEffect waveStart;
    public SoundEffect waveComplete;
    public SoundEffect victoryFanfare;
    public SoundEffect deathSting;
    public SoundEffect questComplete;
    public SoundEffect rankUp;
    public SoundEffect uiMove;
    public SoundEffect uiConfirm;
    public SoundEffect uiCancel;
    public SoundEffect uiOpen;
    public SoundEffect upgradeMenuOpen;
    public SoundEffect upgradeConfirm;
    // Upgrade card entrance: each card knocked down into place on card_click and the
    // confirm button landed on card_settle, the same knock pitched lower. NOTHING PLAYS
    // THESE ANY MORE — the upgrade menu was quietened down to the confirm alone. The
    // slots and their wiring are kept so the cards can be given their knock back
    // without re-authoring anything.
    public SoundEffect cardClick;
    public SoundEffect cardSettle;

    // [Header("Music")]
    // public AudioClip backgroundMusic;
    // private AudioSource _musicSource;

    private AudioSource _sfxSource;
    private AudioSource _loopSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfxSource = gameObject.AddComponent<AudioSource>();

            // A second source for held sounds. PlayOneShot cannot loop, and faking
            // one by re-firing a clip on the shared source both drifts and fights
            // every other effect for that source.
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.loop = true;
            _loopSource.playOnAwake = false;

            // Uncomment for music setup
            // _musicSource = gameObject.AddComponent<AudioSource>();
            // _musicSource.loop = true;
            // _musicSource.clip = backgroundMusic;
            // _musicSource.Play();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaySFX(SoundEffect soundEffect)
    {
        if (soundEffect.clip != null)
        {
            _sfxSource.PlayOneShot(soundEffect.clip, soundEffect.volume);
        }
    }

    // Held sounds — something is happening RIGHT NOW and the player should hear
    // it for as long as it lasts. The clip comes from the caller rather than a
    // field here on purpose: a prefab that owns the state can then own its sound
    // too, and wiring it never has to touch a scene.
    //
    // Starting the loop it is already playing is a no-op, so several sources of
    // the same state (three delivery carts on the track) share one voice instead
    // of stacking into a drone.
    public void PlayLoop(SoundEffect soundEffect)
    {
        if (_loopSource == null || soundEffect == null || soundEffect.clip == null) return;
        if (_loopSource.isPlaying && _loopSource.clip == soundEffect.clip) return;

        _loopSource.clip = soundEffect.clip;
        _loopSource.volume = soundEffect.volume;
        _loopSource.Play();
    }

    public void StopLoop()
    {
        if (_loopSource == null) return;
        _loopSource.Stop();
        _loopSource.clip = null;
    }

    public void StopSFX()
    {
        if (_sfxSource != null)
        {
            _sfxSource.Stop();
        }
    }

    // Uncomment for music control
    // public void SetMusicVolume(float volume)
    // {
    //     _musicSource.volume = Mathf.Clamp01(volume);
    // }
}