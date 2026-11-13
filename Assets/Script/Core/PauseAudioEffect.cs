using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseAudioEffect : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float pausedVolume = 0.1f;
    [SerializeField, Range(200f, 8000f)] private float pausedCutoff = 650f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 1.4f;

    private struct FilterState
    {
        public AudioLowPassFilter Filter;
        public float Cutoff;
        public bool Enabled, Owned;
    }
    private readonly List<FilterState> filters = new List<FilterState>();
    private readonly Dictionary<AudioSource, bool> sourceStates = new Dictionary<AudioSource, bool>();
    private bool paused, captured;
    private float amount, nextScan, originalVolume;

    public void SetPaused(bool value)
    {
        paused = value;
        if (!value || captured) return;
        captured = true;
        originalVolume = AudioListener.volume;
        ScanSources();
    }

    private void ScanSources()
    {
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.GetComponent<PauseAudioUiExempt>() != null || sourceStates.ContainsKey(source)) continue;
            sourceStates.Add(source, source.ignoreListenerVolume);
            // Music must obey the pause gain even if its authored source ignores listener volume.
            source.ignoreListenerVolume = false;
            var filter = source.GetComponent<AudioLowPassFilter>();
            bool tracked = false;
            foreach (var state in filters) if (state.Filter == filter && filter != null) { tracked = true; break; }
            if (tracked) continue;
            bool owned = filter == null;
            if (owned) filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            var saved = new FilterState { Filter = filter, Cutoff = filter.cutoffFrequency, Enabled = !owned && filter.enabled, Owned = owned };
            if (!saved.Enabled) filter.cutoffFrequency = 22000f;
            filter.enabled = true;
            filters.Add(saved);
        }
        nextScan = Time.unscaledTime + 0.25f;
    }

    private void Update()
    {
        if (!captured) return;
        if (Time.unscaledTime >= nextScan) ScanSources();
        float duration = paused ? fadeInDuration : fadeOutDuration;
        amount = Mathf.MoveTowards(amount, paused ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        float blend = Mathf.SmoothStep(0f, 1f, amount);
        AudioListener.volume = originalVolume * Mathf.Lerp(1f, pausedVolume, blend);
        foreach (var state in filters)
        {
            if (state.Filter == null) continue;
            float clear = state.Enabled ? state.Cutoff : 22000f;
            state.Filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(clear), Mathf.Log(Mathf.Min(clear, pausedCutoff)), blend));
        }
        if (!paused && amount <= 0f) RestoreImmediate();
    }

    public void RestoreImmediate()
    {
        paused = false;
        amount = 0f;
        if (!captured) return;
        captured = false;
        AudioListener.volume = originalVolume;
        foreach (var pair in sourceStates) if (pair.Key != null) pair.Key.ignoreListenerVolume = pair.Value;
        sourceStates.Clear();
        foreach (var state in filters)
        {
            if (state.Filter == null) continue;
            state.Filter.cutoffFrequency = state.Cutoff;
            state.Filter.enabled = state.Enabled;
            if (state.Owned) Destroy(state.Filter);
        }
        filters.Clear();
    }

    private void OnDisable() { RestoreImmediate(); }
    private void OnDestroy() { RestoreImmediate(); }
}
