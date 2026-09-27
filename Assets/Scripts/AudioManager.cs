using System.Collections;
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
    // Two quick rising chirps, repeated while a knight is low on health — every
    // 3s under 30 HP, every 1.5s under 10. See PlayerHealth.TickLowHealthWarning.
    public SoundEffect lowHealthChirp;
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
    // Guardian. Both are deliberately near the noise floor and both are textures
    // rather than events: a Reflector knight turns rocks around all wave, and a
    // guided knight locks on with most shots they take. Neither is allowed to
    // announce itself the way a fireball does — the particles are the loud half of
    // each tell, and these two just give it a body. The lock chime is additionally
    // rate-limited across every arrow on the field, in GuidedShot.
    public SoundEffect guardianReflect;
    public SoundEffect guardianGuide;

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
    // An Order trial between waves (see TrialRunner): the rising "you found it"
    // chime for a pass, the deflating womp-womp for a loss.
    public SoundEffect trialWon;
    public SoundEffect trialLost;

    [Header("NPC voices")]
    [Tooltip("One blip per NPC, played every few characters as their text types out. Pitch separates them further - see NpcCatalog.")]
    public SoundEffect npcVoiceKing;
    public SoundEffect npcVoiceCartographer;
    public SoundEffect npcVoiceNinja;
    public SoundEffect npcVoiceWizard;
    public SoundEffect npcVoicePaladin;
    [Tooltip("The little fanfare that plays over the Obtained box. Its LENGTH is the gate - the box cannot be dismissed until it finishes.")]
    public SoundEffect rewardJingle;
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

    [Header("Mix")]
    [Range(0f, 1f)]
    [Tooltip("Master level for every sound effect. The per-clip volume on each SoundEffect above is a BALANCE against its neighbours; this is the one the player owns.")]
    public float sfxVolume = 1f;

    // Where the two sliders are kept. PlayerPrefs rather than the save file on
    // purpose: how loud the game is belongs to the machine it is being played on,
    // not to the knight — switching files should not change the volume, and a
    // fresh file should not start at full blast.
    // Versioned because the meaning of the stored number CHANGED: it used to be raw
    // AudioSource gain, and it is now the slider fraction under MusicCeiling. A
    // value saved under the old key would read as 0.2 slider rather than 0.2 gain
    // — a fifth of what was asked for — and it would look correct on the slider
    // while sounding wrong, which is the worst kind of wrong to debug. Bumping the
    // key throws those away and hands everyone the new default once.
    private const string MusicVolumeKey = "audio.musicVolume.v2";
    private const string SfxVolumeKey = "audio.sfxVolume";

    [Header("Music")]
    // The score at full AudioSource gain drowns the fighting, and a slider whose
    // top two thirds are all unusable is a broken slider — the player drags it to
    // 60% and every setting above that is noise. So the slider does not address
    // raw gain: its 100% IS this number, and the whole 0-100 range maps into
    // something that sounds like a choice the whole way up.
    //
    // Raising this raises the ceiling for everyone, including saved settings —
    // someone sitting at 80% gets louder without touching anything. That is the
    // intended behaviour for a mix fix, but it is why this is not a tuning knob
    // to nudge casually once the game is out.
    private const float MusicCeiling = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Where the MUSIC SLIDER sits, 0-1, which is what the settings screen shows as 0-100%. Actual output is this times the 0.2 ceiling, so 0.8 here is 0.16 of full gain. Serialized value is the default for a machine that has never touched the slider; the saved setting wins over it at startup.")]
    public float musicVolume = 0.8f;

    /// <summary>
    /// What the music sources are actually set to: the player's slider scaled into
    /// the usable range. Everything that touches an AudioSource volume goes through
    /// here — never through musicVolume directly, or that source would jump to full
    /// gain and be five times louder than the rest of the mix.
    /// </summary>
    private float MusicGain => musicVolume * MusicCeiling;
    [Tooltip("Seconds to crossfade when the track changes. The wave-complete beat is roughly this long, so a track swap lands under the upgrade menu rather than on top of the fighting.")]
    public float musicCrossfade = 2f;

    // TWO sources, not one. A single source cannot crossfade with itself, and a
    // hard cut between the forest's calm track and its deep-wood track is audible
    // as a mistake even when the swap itself is correct. They alternate: whichever
    // one is idle takes the incoming track and fades up while the other fades out.
    private AudioSource _musicA;
    private AudioSource _musicB;
    private bool _musicOnA = true;
    private AudioClip _currentMusic;
    private Coroutine _musicFade;

    /// <summary>What is playing (or fading in) right now. Null between tracks.</summary>
    public AudioClip CurrentMusic => _currentMusic;

    private AudioSource _sfxSource;
    private AudioSource _loopSource;
    // The held sound's own balance, kept apart from the master so moving the
    // slider under a running loop rescales it instead of overwriting it.
    private float _loopVolume = 1f;
    private AudioSource _voiceSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Before any source exists, so the first track fades up to the player's
            // level rather than to the inspector default and then correcting itself.
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);

            _sfxSource = gameObject.AddComponent<AudioSource>();

            // A second source for held sounds. PlayOneShot cannot loop, and faking
            // one by re-firing a clip on the shared source both drifts and fights
            // every other effect for that source.
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.loop = true;
            _loopSource.playOnAwake = false;

            // A third source for the NPC voices. It needs its own because PITCH is
            // a property of the source, not of the call — PlayOneShot ignores it —
            // and a per-letter blip retuned on the shared source would bend every
            // other effect playing at the same time.
            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;

            _musicA = CreateMusicSource();
            _musicB = CreateMusicSource();
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
            _sfxSource.PlayOneShot(soundEffect.clip, soundEffect.volume * sfxVolume);
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
    /// <summary>
    /// One letter of an NPC's voice. Pitch is what tells the five of them apart —
    /// they share a blip and differ by where it sits — so it is applied to the
    /// source before the shot rather than baked into five near-identical clips.
    ///
    /// Callers rate-limit themselves: the dialogue box fires this every few
    /// characters, never every one, the same way burnTick and guardianGuide are
    /// throttled at their own call sites.
    /// </summary>
    public void PlayVoice(SoundEffect soundEffect, float pitch)
    {
        if (_voiceSource == null || soundEffect == null || soundEffect.clip == null) return;
        _voiceSource.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        _voiceSource.PlayOneShot(soundEffect.clip, soundEffect.volume * sfxVolume);
    }

    public void PlayLoop(SoundEffect soundEffect)
    {
        if (_loopSource == null || soundEffect == null || soundEffect.clip == null) return;
        if (_loopSource.isPlaying && _loopSource.clip == soundEffect.clip) return;

        _loopSource.clip = soundEffect.clip;
        _loopVolume = soundEffect.volume;
        _loopSource.volume = _loopVolume * sfxVolume;
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

    private AudioSource CreateMusicSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        // 2D, explicitly. The importer marks these clips 3D, and a 3D source on a
        // DontDestroyOnLoad object sits at the world origin — which is between the
        // two knights, so the score would pan and attenuate as the camera and the
        // listener moved. The score is not in the world; it is over it.
        source.spatialBlend = 0f;
        return source;
    }

    /// <summary>
    /// Put a track on. Asking for the track that is already playing is a NO-OP,
    /// which is what makes this safe to call every single wave — the caller does
    /// not have to know whether the music needs to change, only what it should be.
    /// Same contract as PlayLoop.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (_musicA == null || _musicB == null) return;
        if (clip == null) { StopMusic(); return; }
        if (clip == _currentMusic && ActiveMusicSource.isPlaying) return;

        _currentMusic = clip;

        AudioSource incoming = _musicOnA ? _musicB : _musicA;
        AudioSource outgoing = _musicOnA ? _musicA : _musicB;
        _musicOnA = !_musicOnA;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(Crossfade(incoming, outgoing, musicCrossfade));
    }

    /// <summary>Fade the score out entirely. Pass 0 for a hard cut.</summary>
    public void StopMusic(float fadeSeconds = -1f)
    {
        if (_musicA == null || _musicB == null) return;
        _currentMusic = null;
        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(
            Crossfade(null, ActiveMusicSource, fadeSeconds < 0f ? musicCrossfade : fadeSeconds));
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);

        // Whatever is mid-fade will pick the new target up on its next step; the
        // settled source needs telling directly.
        if (_musicFade == null && ActiveMusicSource != null)
            ActiveMusicSource.volume = MusicGain;
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);

        // One-shots read the master as they fire, so they need nothing. A HELD
        // sound is already playing and would otherwise stay at the old level until
        // whatever is making it stopped.
        if (_loopSource != null) _loopSource.volume = _loopVolume * sfxVolume;
    }

    /// <summary>
    /// Flush the mix to disk. The setters only write PlayerPrefs in memory, which
    /// is the right trade while a slider is being dragged — this is for the moment
    /// the player is done, so a crash before quit does not lose the setting.
    /// </summary>
    public void SaveMixSettings()
    {
        PlayerPrefs.Save();
    }

    private AudioSource ActiveMusicSource => _musicOnA ? _musicA : _musicB;

    // UNSCALED time throughout. The pause menu and the upgrade menu both stop the
    // clock, and a fade on scaled time would freeze halfway through with one track
    // half up and the other half down — which is exactly when a track swap happens,
    // because the swap rides the wave-complete beat into the upgrade menu.
    private IEnumerator Crossfade(AudioSource incoming, AudioSource outgoing, float seconds)
    {
        float fromOut = outgoing != null ? outgoing.volume : 0f;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
            if (incoming != null) incoming.volume = MusicGain * t;
            if (outgoing != null) outgoing.volume = Mathf.Lerp(fromOut, 0f, t);
            yield return null;
        }

        if (incoming != null) incoming.volume = MusicGain;
        if (outgoing != null)
        {
            outgoing.volume = 0f;
            outgoing.Stop();
            // Dropping the clip matters: a stopped source still holds its streamed
            // file open, and these are streaming clips.
            outgoing.clip = null;
        }
        _musicFade = null;
    }
}