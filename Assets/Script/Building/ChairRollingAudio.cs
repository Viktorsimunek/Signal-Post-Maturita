using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public sealed class ChairRollingAudio : MonoBehaviour
{
    [SerializeField, Min(0f)] private float minimumSpeed = 0.04f;
    [SerializeField, Min(0.01f)] private float fullVolumeSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.55f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField, Min(0.01f)] private float responseSpeed = 4f;
    private Rigidbody body;
    private AudioSource source;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.pitch = pitchRange.x;
    }

    private void Update()
    {
        if (source.clip == null || Time.timeScale <= 0f) return;

        Vector3 velocity = body.linearVelocity;
        float speed = body.IsSleeping() ? 0f : new Vector2(velocity.x, velocity.z).magnitude;
        float amount = Mathf.InverseLerp(minimumSpeed, fullVolumeSpeed, speed);
        float step = responseSpeed * Time.deltaTime;
        source.volume = Mathf.MoveTowards(source.volume, amount * maximumVolume, step);
        source.pitch = Mathf.MoveTowards(source.pitch, Mathf.Lerp(pitchRange.x, pitchRange.y, amount), step);

        if (amount > 0f && !source.isPlaying) source.Play();
        else if (amount == 0f && source.volume <= 0.001f)
        {
            source.Stop();
            source.volume = 0f;
        }
    }

    private void OnDisable()
    {
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }
}
