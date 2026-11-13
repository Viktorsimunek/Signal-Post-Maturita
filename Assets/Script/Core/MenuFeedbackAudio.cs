using UnityEngine;

public static class MenuFeedbackAudio
{
    public static void Play(ref AudioSource output, Transform parent, AudioSource template, AudioClip clip, float pitch)
    {
        if (template == null || clip == null) return;
        if (output == null)
        {
            var child = new GameObject("Menu UI Audio");
            child.transform.SetParent(parent, false);
            child.AddComponent<PauseAudioUiExempt>();
            output = child.AddComponent<AudioSource>();
            output.playOnAwake = false;
            output.spatialBlend = 0f;
            output.ignoreListenerVolume = true;
        }
        // Mixer-routed sources still receive listener gain in Unity despite ignoreListenerVolume.
        // UI therefore goes directly to the listener and applies user sliders explicitly.
        output.outputAudioMixerGroup = null;
        output.volume = template.volume * GameSettings.MasterVolume * GameSettings.SfxVolume;
        output.mute = template.mute;
        output.priority = template.priority;
        output.pitch = pitch;
        output.PlayOneShot(clip);
    }
}
