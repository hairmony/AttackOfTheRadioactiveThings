using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// SFX + music mixer buses (set from <see cref="SettingsAudioBootstrap"/>).
/// Not a MonoBehaviour — do not add this script to a GameObject.
/// </summary>
public static class GameAudio
{
    public static AudioMixerGroup SfxOutputGroup { get; private set; }
    public static AudioMixerGroup MusicOutputGroup { get; private set; }

    public static void RegisterSfxOutput(AudioMixerGroup group)
    {
        SfxOutputGroup = group;
    }

    public static void RegisterMusicOutput(AudioMixerGroup group)
    {
        MusicOutputGroup = group;
    }

    /// <summary>
    /// Plays a one-shot through the SFX mixer bus (volume slider applies via mixer).
    /// <paramref name="volumeScale"/> is per-clip 0–1, not the global SFX setting.
    /// If no SFX group is registered, uses <see cref="AudioSource.PlayClipAtPoint"/> scaled by <see cref="GameSettings.SfxVolume"/>.
    /// </summary>
    public static void PlaySfx(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f)
    {
        if (clip == null) return;
        GameSettings.EnsureLoaded();
        volumeScale = Mathf.Clamp01(volumeScale);

        if (SfxOutputGroup != null)
        {
            var go = new GameObject("OneShotSFX");
            go.transform.position = worldPosition;
            var src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = SfxOutputGroup;
            src.spatialBlend = 0f;
            src.PlayOneShot(clip, volumeScale);
            Object.Destroy(go, clip.length + 0.05f);
            return;
        }

        float v = volumeScale * GameSettings.SfxVolume * GameSettings.MixerFullSliderLinearCap;
        if (v <= 0.0001f) return;
        AudioSource.PlayClipAtPoint(clip, worldPosition, v);
    }
}
