using UnityEngine;

[DisallowMultipleComponent]
public class TransmitAudioFeedback : MonoBehaviour
{
    [SerializeField] private MessageTransmitter transmitter;
    [SerializeField] private AudioSource loopSource;
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioClip sendingLoop;
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip sentSound;
    [SerializeField] private AudioClip failSound;
    [SerializeField] private float loopVolume = 0.6f;
    [SerializeField] private float oneShotVolume = 1f;

    private bool wasSending;

    private void Reset()
    {
        transmitter = GetComponent<MessageTransmitter>();
        loopSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (transmitter == null)
        {
            transmitter = GetComponent<MessageTransmitter>();
        }

        if (loopSource == null)
        {
            loopSource = GetComponent<AudioSource>();
        }

        if (oneShotSource == null)
        {
            oneShotSource = loopSource;
        }
    }

    private void Update()
    {
        bool sending = transmitter != null && transmitter.IsSending;

        if (sending && !wasSending)
        {
            PlayStart();
        }

        if (!sending && wasSending)
        {
            PlayEnd();
        }

        wasSending = sending;
    }

    private void PlayStart()
    {
        PlayOneShot(startSound);

        if (loopSource == null || sendingLoop == null)
        {
            return;
        }

        loopSource.clip = sendingLoop;
        loopSource.loop = true;
        loopSource.volume = loopVolume;
        loopSource.Play();
    }

    private void PlayEnd()
    {
        if (loopSource != null)
        {
            loopSource.Stop();
        }

        if (transmitter != null && transmitter.Progress >= 1f)
        {
            PlayOneShot(sentSound);
        }
        else
        {
            PlayOneShot(failSound);
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (oneShotSource != null && clip != null)
        {
            oneShotSource.PlayOneShot(clip, oneShotVolume);
        }
    }
}
