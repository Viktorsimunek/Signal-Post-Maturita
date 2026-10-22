using System;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class AmbientSoundController : MonoBehaviour
{
    [Serializable]
    private class AmbientLayer
    {
        public string name = "Ambient";
        public AudioSource source;
        public AudioClip clip;
        [Min(0f)] public float volume = 1f;
        public AnimationCurve timeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1f));
        public AnimationCurve stormCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1f));
        [Min(0f)] public float outsideMultiplier = 1f;
        [Min(0f)] public float insideMultiplier = 0.2f;
        [Min(0.01f)] public float pitch = 1f;
        public bool muffleInside = true;
        [Min(10f)] public float outsideCutoff = 22000f;
        [Min(10f)] public float insideCutoff = 1400f;
    }

    [Serializable]
    private struct AmbientZone
    {
        public Collider collider;
        [Range(0f, 1f)] public float insideAmount;
    }

    [Header("References")]
    [SerializeField] private TimeOfDayController timeOfDayController;
    [SerializeField] private Transform listener;
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("Tuning")]
    [SerializeField, Min(0f)] private float masterVolume = 1f;
    [SerializeField, Min(0.01f)] private float volumeLerpSpeed = 1.8f;
    [SerializeField, Min(0.01f)] private float insideLerpSpeed = 2.4f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
    [SerializeField, Range(0f, 1f)] private float manualInsideAmount;
    [SerializeField] private bool keepLoopsAlive = true;

    [Header("Zones")]
    [SerializeField] private AmbientZone[] zones = Array.Empty<AmbientZone>();

    [Header("Layers")]
    [SerializeField] private AmbientLayer[] layers = Array.Empty<AmbientLayer>();

    private float currentInsideAmount;

    private void Reset()
    {
        timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        FindListener();

        if (layers == null || layers.Length == 0)
        {
            layers = new[]
            {
                new AmbientLayer
                {
                    name = "Day",
                    volume = 0.8f,
                    timeCurve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.22f, 0f),
                        new Keyframe(0.34f, 1f),
                        new Keyframe(0.66f, 1f),
                        new Keyframe(0.82f, 0f),
                        new Keyframe(1f, 0f)),
                    stormCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(1f, 0.35f)),
                    outsideMultiplier = 1f,
                    insideMultiplier = 0.15f,
                    pitch = 1f,
                    muffleInside = true,
                    outsideCutoff = 22000f,
                    insideCutoff = 1800f
                },
                new AmbientLayer
                {
                    name = "Night",
                    volume = 0.7f,
                    timeCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(0.34f, 0f),
                        new Keyframe(0.66f, 0f),
                        new Keyframe(0.82f, 1f),
                        new Keyframe(1f, 1f)),
                    stormCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(1f, 0.5f)),
                    outsideMultiplier = 1f,
                    insideMultiplier = 0.2f,
                    pitch = 1f,
                    muffleInside = true,
                    outsideCutoff = 22000f,
                    insideCutoff = 1500f
                },
                new AmbientLayer
                {
                    name = "DawnDusk",
                    volume = 0.45f,
                    timeCurve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.2f, 0f),
                        new Keyframe(0.25f, 1f),
                        new Keyframe(0.34f, 0f),
                        new Keyframe(0.66f, 0f),
                        new Keyframe(0.75f, 1f),
                        new Keyframe(0.84f, 0f),
                        new Keyframe(1f, 0f)),
                    stormCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(1f, 0.6f)),
                    outsideMultiplier = 1f,
                    insideMultiplier = 0.2f,
                    pitch = 1f,
                    muffleInside = true,
                    outsideCutoff = 22000f,
                    insideCutoff = 2000f
                },
                new AmbientLayer
                {
                    name = "Storm",
                    volume = 0.85f,
                    timeCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(1f, 1f)),
                    stormCurve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.2f, 0f),
                        new Keyframe(0.5f, 0.35f),
                        new Keyframe(1f, 1f)),
                    outsideMultiplier = 1f,
                    insideMultiplier = 0.45f,
                    pitch = 1f,
                    muffleInside = true,
                    outsideCutoff = 22000f,
                    insideCutoff = 900f
                }
            };
        }
    }

    private void Awake()
    {
        FindListener();
        ConfigureSources();
    }

    private void OnEnable()
    {
        FindListener();
        ConfigureSources();
    }

    private void OnDisable()
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            AmbientLayer layer = layers[i];
            if (layer == null || layer.source == null)
            {
                continue;
            }

            layer.source.volume = 0f;
            if (layer.source.isPlaying)
            {
                layer.source.Stop();
            }
        }
    }

    private void OnValidate()
    {
        masterVolume = Mathf.Max(0f, masterVolume);
        volumeLerpSpeed = Mathf.Max(0.01f, volumeLerpSpeed);
        insideLerpSpeed = Mathf.Max(0.01f, insideLerpSpeed);
        manualInsideAmount = Mathf.Clamp01(manualInsideAmount);

        ConfigureSources();
    }

    private void Update()
    {
        if (timeOfDayController == null)
        {
            timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        }

        FindListener();

        float targetInside = Mathf.Max(manualInsideAmount, GetZoneInsideAmount());
        currentInsideAmount = Mathf.MoveTowards(currentInsideAmount, targetInside, insideLerpSpeed * Time.deltaTime);

        float time01 = timeOfDayController != null ? timeOfDayController.Time01 : 0f;
        float storm01 = timeOfDayController != null ? timeOfDayController.StormAmount01 : 0f;

        for (int i = 0; i < layers.Length; i++)
        {
            AmbientLayer layer = layers[i];
            if (layer == null || layer.source == null)
            {
                continue;
            }

            if (layer.clip != null && layer.source.clip != layer.clip)
            {
                layer.source.clip = layer.clip;
            }

            float timeValue = layer.timeCurve == null || layer.timeCurve.length == 0 ? 1f : Mathf.Max(0f, layer.timeCurve.Evaluate(time01));
            float stormValue = layer.stormCurve == null || layer.stormCurve.length == 0 ? 1f : Mathf.Max(0f, layer.stormCurve.Evaluate(storm01));
            float insideValue = Mathf.Lerp(layer.outsideMultiplier, layer.insideMultiplier, currentInsideAmount);
            float targetVolume = masterVolume * layer.volume * timeValue * stormValue * insideValue;

            if (Application.isPlaying && layer.clip != null)
            {
                if (keepLoopsAlive || targetVolume > 0.001f)
                {
                    if (!layer.source.isPlaying)
                    {
                        layer.source.Play();
                    }
                }
                else if (layer.source.isPlaying)
                {
                    layer.source.Stop();
                }
            }

            layer.source.volume = Mathf.MoveTowards(layer.source.volume, targetVolume, volumeLerpSpeed * Time.deltaTime);

            AudioLowPassFilter lowPass = layer.source.GetComponent<AudioLowPassFilter>();
            if (layer.muffleInside)
            {
                if (lowPass == null && Application.isPlaying)
                {
                    lowPass = layer.source.gameObject.AddComponent<AudioLowPassFilter>();
                }

                if (lowPass != null)
                {
                    lowPass.enabled = currentInsideAmount > 0.001f;
                    lowPass.cutoffFrequency = Mathf.Lerp(layer.outsideCutoff, layer.insideCutoff, currentInsideAmount);
                }
            }
            else if (lowPass != null)
            {
                lowPass.enabled = false;
            }
        }
    }

    private void FindListener()
    {
        if (listener != null)
        {
            return;
        }

        AudioListener audioListener = FindFirstObjectByType<AudioListener>();
        if (audioListener != null)
        {
            listener = audioListener.transform;
            return;
        }

        if (Camera.main != null)
        {
            listener = Camera.main.transform;
        }
    }

    private void ConfigureSources()
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            AmbientLayer layer = layers[i];
            if (layer == null || layer.source == null)
            {
                continue;
            }

            layer.volume = Mathf.Max(0f, layer.volume);
            layer.outsideMultiplier = Mathf.Max(0f, layer.outsideMultiplier);
            layer.insideMultiplier = Mathf.Max(0f, layer.insideMultiplier);
            layer.pitch = Mathf.Max(0.01f, layer.pitch);
            layer.outsideCutoff = Mathf.Max(10f, layer.outsideCutoff);
            layer.insideCutoff = Mathf.Max(10f, layer.insideCutoff);

            layer.source.playOnAwake = false;
            layer.source.loop = true;
            layer.source.spatialBlend = spatialBlend;
            layer.source.dopplerLevel = 0f;
            layer.source.pitch = layer.pitch;
            layer.source.outputAudioMixerGroup = outputMixerGroup;

            if (layer.clip != null)
            {
                layer.source.clip = layer.clip;
            }
        }
    }

    private float GetZoneInsideAmount()
    {
        if (listener == null || zones == null || zones.Length == 0)
        {
            return 0f;
        }

        Vector3 point = listener.position;
        float best = 0f;

        for (int i = 0; i < zones.Length; i++)
        {
            Collider zone = zones[i].collider;
            if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closest = zone.ClosestPoint(point);
            if ((closest - point).sqrMagnitude <= 0.0001f)
            {
                best = Mathf.Max(best, Mathf.Clamp01(zones[i].insideAmount));
            }
        }

        return best;
    }
}
