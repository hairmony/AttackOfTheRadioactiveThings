using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Looping background music for menu scenes. Add to a GameObject (e.g. Canvas or "Audio"),
/// assign a clip, and optionally route through the mixer Music group via bootstrap or override.
/// </summary>
[DefaultExecutionOrder(10)]
public class MenuBackgroundMusic : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    [Tooltip("When set, uses this mixer group. Otherwise uses GameAudio.MusicOutputGroup from SettingsAudioBootstrap.")]
    [SerializeField] private AudioMixerGroup musicOutputOverride;
    [Tooltip("Multiplier on the AudioSource (use 1 when the Music mixer slider should control level).")]
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool stopOnDisable = true;

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.loop = true;
        _source.clip = music;
        _source.volume = Mathf.Clamp01(volume);
        ApplyMixerRoute();
    }

    /// <summary>
    /// Mixer must be set before <see cref="AudioSource.Play"/>; <c>OnEnable</c> runs before <c>Start</c>,
    /// so routing in <c>Start</c> meant music briefly played outside the mixer at full level.
    /// </summary>
    private void ApplyMixerRoute()
    {
        if (_source == null) return;
        if (musicOutputOverride != null)
            _source.outputAudioMixerGroup = musicOutputOverride;
        else if (GameAudio.MusicOutputGroup != null)
            _source.outputAudioMixerGroup = GameAudio.MusicOutputGroup;
    }

    private void OnEnable()
    {
        if (!playOnEnable || music == null || _source == null) return;
        ApplyMixerRoute();
        TryResolveBootstrapIfUnrouted();
        _source.Play();
    }

    private void Start() => TryResolveBootstrapIfUnrouted();

    /// <summary>Ensures the AudioSource uses the Music mixer bus so settings sliders apply.</summary>
    private void TryResolveBootstrapIfUnrouted()
    {
        if (_source == null || musicOutputOverride != null) return;
        if (_source.outputAudioMixerGroup != null) return;

        foreach (var boot in FindObjectsByType<SettingsAudioBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            boot.ApplyNow();
            ApplyMixerRoute();
            return;
        }

        ApplyMixerRoute();
    }

    private void OnDisable()
    {
        if (!stopOnDisable || _source == null) return;
        _source.Stop();
    }

    /// <summary>Swap clip at runtime (e.g. different screen).</summary>
    public void SetMusic(AudioClip clip, bool playIfWasPlaying = true)
    {
        music = clip;
        if (_source == null) return;
        bool was = _source.isPlaying;
        _source.Stop();
        _source.clip = clip;
        ApplyMixerRoute();
        if (clip != null && (playIfWasPlaying || was))
            _source.Play();
    }
}
