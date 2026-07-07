using UnityEngine;

/// <summary>
/// Global SFX playback. Call SoundManager.PlaySound(SFX.CardDealt) from anywhere.
/// Sounds are mapped to clips via a SoundLibrary asset assigned in the inspector.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private SoundLibrary _library;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _musicSource;

    private BGM? _currentMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (_sfxSource == null)
            _sfxSource = GetComponent<AudioSource>();

        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
        }
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;

        //?GameSettings.MusicEnabledChanged += HandleMusicEnabledChanged;
    }

    private void OnDestroy()
    {
        //?GameSettings.MusicEnabledChanged -= HandleMusicEnabledChanged;

        if (Instance == this)
            Instance = null;
    }

    public static void PlaySound(SFX id) => Instance?.Play(id);
    public static void PlayMusic(BGM id) => Instance?.Play(id);
    public static void StopMusic() => Instance?.Stop();

    private void Play(SFX id)
    {
        //?
        // if (!GameSettings.SfxEnabled)
        //     return;

        if (_library == null) return;

        if (_library.TryGetClip(id, out AudioClip clip, out float volume))
            _sfxSource.PlayOneShot(clip, volume);
    }

    private void Play(BGM id)
    {
        if (_library == null) return;

        if (_library.TryGetMusicClip(id, out AudioClip clip, out float volume))
        {
            if (_currentMusic == id && _musicSource.clip == clip && _musicSource.isPlaying)
                return;

            _currentMusic = id;
            _musicSource.clip = clip;
            _musicSource.volume = volume;

            //? if (!GameSettings.MusicEnabled)
            // {
            //     _musicSource.Stop();
            //     return;
            // }

            _musicSource.Play();
        }
    }

    private void Stop()
    {
        _currentMusic = null;
        _musicSource.Stop();
    }

    private void HandleMusicEnabledChanged(bool enabled)
    {
        if (_musicSource == null)
            return;

        if (!enabled)
        {
            _musicSource.Pause();
            return;
        }

        if (_currentMusic.HasValue)
            Play(_currentMusic.Value);
    }
}
