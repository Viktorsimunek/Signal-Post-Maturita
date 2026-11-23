using UnityEngine;
using UnityEngine.Audio;

public class GlobalAudioSettingsApplier : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer targetMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private static GlobalAudioSettingsApplier instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        GameSettings.SettingsChanged += ApplyToMixer;

        GameSettings.Load();
        GameSettings.ApplyRuntime();
        ApplyToMixer();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            GameSettings.SettingsChanged -= ApplyToMixer;
            instance = null;
        }
    }

    public void ApplyToMixer()
    {
        if (targetMixer == null)
        {
            return;
        }

        targetMixer.SetFloat(masterVolumeParam, AudioEffects.ToDecibels(GameSettings.MasterVolume));
        targetMixer.SetFloat(musicVolumeParam, AudioEffects.ToDecibels(GameSettings.MusicVolume));
        targetMixer.SetFloat(sfxVolumeParam, AudioEffects.ToDecibels(GameSettings.SfxVolume));
    }

}
