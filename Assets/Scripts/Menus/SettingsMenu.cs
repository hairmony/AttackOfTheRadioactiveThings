using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wire UI controls here (sliders, toggles, dropdown). Optionally assign an AudioMixer;
/// expose float params named like masterParamName (dB) for each group.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SettingsMenu : MonoBehaviour
{
    [Header("UI — optional, assign what you use")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Audio (optional)")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterMixerParameter = "MasterVol";
    [SerializeField] private string musicMixerParameter = "MusicVol";
    [SerializeField] private string sfxMixerParameter = "SfxVol";

    private void Awake()
    {
        TryBindSlidersFromHierarchy();
    }

    private void OnEnable()
    {
        GameSettings.EnsureLoaded();
        TryBindSlidersFromHierarchy();
        WireToggleHandlers();
        RefreshUIFromSettings();
    }

    private void OnDisable()
    {
        UnwireToggleHandlers();
    }

    private void WireToggleHandlers()
    {
        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
        if (vsyncToggle != null)
        {
            vsyncToggle.onValueChanged.RemoveListener(OnVsyncChanged);
            vsyncToggle.onValueChanged.AddListener(OnVsyncChanged);
        }
    }

    private void UnwireToggleHandlers()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.RemoveListener(OnVsyncChanged);
    }

    /// <summary>Bind sliders by name when references are missing (SettingsManager is not parented under the canvas).</summary>
    private void TryBindSlidersFromHierarchy()
    {
        if (masterSlider != null && musicSlider != null && sfxSlider != null)
            return;
        foreach (var s in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            switch (s.gameObject.name)
            {
                case "MasterSlider":
                    if (masterSlider == null) masterSlider = s;
                    break;
                case "MusicSlider":
                    if (musicSlider == null) musicSlider = s;
                    break;
                case "SFXSlider":
                    if (sfxSlider == null) sfxSlider = s;
                    break;
            }
        }
    }

    public void RefreshUIFromSettings()
    {
        GameSettings.EnsureLoaded();

        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        if (vsyncToggle != null)
            vsyncToggle.SetIsOnWithoutNotify(GameSettings.Vsync);

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            foreach (string name in QualitySettings.names)
                options.Add(new TMP_Dropdown.OptionData(name));
            qualityDropdown.AddOptions(options);
            qualityDropdown.SetValueWithoutNotify(GameSettings.QualityLevel);
            qualityDropdown.RefreshShownValue();
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        GameSettings.SetMasterVolume(value);
        GameSettings.ApplyAudio(audioMixer, masterMixerParameter, musicMixerParameter, sfxMixerParameter);
    }

    public void OnMusicVolumeChanged(float value)
    {
        GameSettings.SetMusicVolume(value);
        GameSettings.ApplyAudio(audioMixer, masterMixerParameter, musicMixerParameter, sfxMixerParameter);
    }

    public void OnSfxVolumeChanged(float value)
    {
        GameSettings.SetSfxVolume(value);
        GameSettings.ApplyAudio(audioMixer, masterMixerParameter, musicMixerParameter, sfxMixerParameter);
    }

    public void OnFullscreenChanged(bool value)
    {
        GameSettings.SetFullscreen(value);
        GameSettings.ApplyDisplay();
    }

    public void OnVsyncChanged(bool value)
    {
        GameSettings.SetVsync(value);
        GameSettings.ApplyDisplay();
    }

    public void OnQualityChanged(int index)
    {
        GameSettings.SetQualityLevel(index);
        GameSettings.ApplyDisplay();
    }

    public void OnSaveSettings()
    {
        GameSettings.ApplyDisplay();
        GameSettings.ApplyAudio(audioMixer, masterMixerParameter, musicMixerParameter, sfxMixerParameter);
        GameSettings.Save();
    }

    public void OnResetDefaults()
    {
        GameSettings.ResetToDefaults();
        GameSettings.ApplyAudio(audioMixer, masterMixerParameter, musicMixerParameter, sfxMixerParameter);
        RefreshUIFromSettings();
    }
}
