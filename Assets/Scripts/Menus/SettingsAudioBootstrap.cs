using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Drop on a persistent object (e.g. Main Menu) with your Audio Mixer assigned.
/// Re-applies saved volumes when the scene loads.
/// </summary>
[DefaultExecutionOrder(-500)]
public class SettingsAudioBootstrap : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVol";
    [SerializeField] private string musicParam = "MusicVol";
    [SerializeField] private string sfxParam = "SfxVol";
    [Tooltip("Drag the SFX group from the Audio Mixer window so one-shots (e.g. enemy cues) use the SFX slider.")]
    [SerializeField] private AudioMixerGroup sfxOutputGroup;
    [Tooltip("Drag the Music group so MenuBackgroundMusic and other music sources follow the Music slider.")]
    [SerializeField] private AudioMixerGroup musicOutputGroup;

    private void Awake() => ApplyNow();

    /// <summary>Re-register mixer groups and apply saved volumes (e.g. <see cref="MenuBackgroundMusic"/> if routing was still null).</summary>
    public void ApplyNow()
    {
        GameSettings.EnsureLoaded();
        GameSettings.ApplyAudio(mixer, masterParam, musicParam, sfxParam);
        if (sfxOutputGroup != null)
            GameAudio.RegisterSfxOutput(sfxOutputGroup);
        if (musicOutputGroup != null)
            GameAudio.RegisterMusicOutput(musicOutputGroup);
    }
}
