using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string AmbientVolumeKey = "Audio.AmbientVolume";

    [Header("Library")]
    [SerializeField] private AudioCueLibrary library;

    [Header("Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField, Min(1)] private int sfxPoolSize = 8;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float masterSfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float masterMusicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float masterAmbientVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool logCuePlayback = false;

    private readonly List<AudioSource> sfxSources = new();
    private AudioCue currentMusicCue = AudioCue.None;
    private AudioCue currentAmbientCue = AudioCue.None;
    private float currentMusicCueVolume = 1f;
    private float currentAmbientCueVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSavedVolumes();
        EnsureSources();
        ApplyLoopVolumes();
    }

    public static void PlayCue(AudioCue cue)
    {
        if (cue == AudioCue.None) return;
        Instance?.Play(cue);
    }

    public static void StopMusicCue()
    {
        Instance?.StopMusic();
    }

    public static void StopAmbientCue()
    {
        Instance?.StopAmbient();
    }

    public static void StopAllCues()
    {
        Instance?.StopAll();
    }

    public static void SetSfxVolume(float volume, bool save = true)
    {
        if (Instance != null)
            Instance.SetVolume(AudioCuePlaybackType.OneShot, volume, save);
    }

    public static void SetMusicVolume(float volume, bool save = true)
    {
        if (Instance != null)
            Instance.SetVolume(AudioCuePlaybackType.MusicLoop, volume, save);
    }

    public static void SetAmbientVolume(float volume, bool save = true)
    {
        if (Instance != null)
            Instance.SetVolume(AudioCuePlaybackType.AmbientLoop, volume, save);
    }

    public void Play(AudioCue cue)
    {
        if (logCuePlayback)
            Debug.Log($"[Audio] Cue requested: {cue}");

        if (library == null)
        {
            if (logCuePlayback)
                Debug.LogWarning($"[Audio] No AudioCueLibrary assigned. Cue skipped: {cue}");
            return;
        }

        if (!library.TryGetCue(cue, out var entry))
        {
            if (logCuePlayback)
                Debug.LogWarning($"[Audio] Cue not found or has no clips. Cue skipped: {cue}");
            return;
        }

        var clip = entry.GetRandomClip();
        if (clip == null)
        {
            if (logCuePlayback)
                Debug.LogWarning($"[Audio] Cue has no playable clip. Cue skipped: {cue}");
            return;
        }

        EnsureSources();

        if (logCuePlayback)
            Debug.Log($"[Audio] Playing {cue} as {entry.PlaybackType}: {clip.name}");

        switch (entry.PlaybackType)
        {
            case AudioCuePlaybackType.MusicLoop:
                PlayLoop(musicSource, cue, clip, entry.Volume, masterMusicVolume, entry.GetRandomPitch(), ref currentMusicCue, ref currentMusicCueVolume);
                break;
            case AudioCuePlaybackType.AmbientLoop:
                PlayLoop(ambientSource, cue, clip, entry.Volume, masterAmbientVolume, entry.GetRandomPitch(), ref currentAmbientCue, ref currentAmbientCueVolume);
                break;
            default:
                PlaySfx(clip, entry.Volume * masterSfxVolume, entry.GetRandomPitch());
                break;
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
        currentMusicCue = AudioCue.None;
        currentMusicCueVolume = 1f;
    }

    public void StopAmbient()
    {
        if (ambientSource != null)
            ambientSource.Stop();
        currentAmbientCue = AudioCue.None;
        currentAmbientCueVolume = 1f;
    }

    public void StopAll()
    {
        StopMusic();
        StopAmbient();

        foreach (var source in sfxSources)
        {
            if (source != null)
                source.Stop();
        }
    }

    private void PlaySfx(AudioClip clip, float volume, float pitch)
    {
        var source = GetAvailableSfxSource();
        if (source == null || clip == null) return;

        source.pitch = pitch;
        source.volume = 1f;
        source.PlayOneShot(clip, volume);
    }

    private void PlayLoop(AudioSource source, AudioCue cue, AudioClip clip, float cueVolume, float masterVolume, float pitch, ref AudioCue currentCue, ref float currentCueVolume)
    {
        if (source == null || clip == null) return;

        currentCueVolume = cueVolume;

        if (currentCue == cue && source.clip == clip && source.isPlaying)
        {
            source.volume = cueVolume * masterVolume;
            source.pitch = pitch;
            return;
        }

        source.clip = clip;
        source.volume = cueVolume * masterVolume;
        source.pitch = pitch;
        source.loop = true;
        source.Play();
        currentCue = cue;
    }

    private void EnsureSources()
    {
        sfxSource = EnsureSource(sfxSource, false);
        musicSource = EnsureSource(musicSource, true);
        ambientSource = EnsureSource(ambientSource, true);

        if (!sfxSources.Contains(sfxSource))
            sfxSources.Add(sfxSource);

        while (sfxSources.Count < sfxPoolSize)
            sfxSources.Add(EnsureSource(null, false));
    }

    private AudioSource EnsureSource(AudioSource source, bool loop)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

    private AudioSource GetAvailableSfxSource()
    {
        EnsureSources();

        foreach (var source in sfxSources)
        {
            if (source != null && !source.isPlaying)
                return source;
        }

        return sfxSources.Count > 0 ? sfxSources[0] : null;
    }

    private void SetVolume(AudioCuePlaybackType type, float volume, bool save)
    {
        volume = Mathf.Clamp01(volume);

        switch (type)
        {
            case AudioCuePlaybackType.MusicLoop:
                masterMusicVolume = volume;
                if (save) PlayerPrefs.SetFloat(MusicVolumeKey, volume);
                break;
            case AudioCuePlaybackType.AmbientLoop:
                masterAmbientVolume = volume;
                if (save) PlayerPrefs.SetFloat(AmbientVolumeKey, volume);
                break;
            default:
                masterSfxVolume = volume;
                if (save) PlayerPrefs.SetFloat(SfxVolumeKey, volume);
                break;
        }

        if (save)
            PlayerPrefs.Save();

        ApplyLoopVolumes();
    }

    private void LoadSavedVolumes()
    {
        masterSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, masterSfxVolume);
        masterMusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, masterMusicVolume);
        masterAmbientVolume = PlayerPrefs.GetFloat(AmbientVolumeKey, masterAmbientVolume);
    }

    private void ApplyLoopVolumes()
    {
        if (musicSource != null)
            musicSource.volume = currentMusicCueVolume * masterMusicVolume;

        if (ambientSource != null)
            ambientSource.volume = currentAmbientCueVolume * masterAmbientVolume;
    }

    private void OnValidate()
    {
        masterSfxVolume = Mathf.Clamp01(masterSfxVolume);
        masterMusicVolume = Mathf.Clamp01(masterMusicVolume);
        masterAmbientVolume = Mathf.Clamp01(masterAmbientVolume);
        sfxPoolSize = Mathf.Max(1, sfxPoolSize);
    }
}
