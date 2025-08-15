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

    // [Header("Music")]
    // public AudioClip backgroundMusic;
    // private AudioSource _musicSource;

    private AudioSource _sfxSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfxSource = gameObject.AddComponent<AudioSource>();

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