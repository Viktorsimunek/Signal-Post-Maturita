using UnityEngine;

[DisallowMultipleComponent]
public class DebugEndingInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Debug ending";
    [SerializeField] private TransmissionSequenceController transmissionSequence;
    [SerializeField] private EndingSequenceController endingSequence;

    private bool used;

    private void Awake()
    {
        if (transmissionSequence == null)
        {
            transmissionSequence = FindFirstObjectByType<TransmissionSequenceController>();
        }

        if (endingSequence == null)
        {
            endingSequence = FindFirstObjectByType<EndingSequenceController>();
        }
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !used && isActiveAndEnabled;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        return prompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        used = true;

        if (transmissionSequence != null)
        {
            transmissionSequence.StartDebugEnding();
            return;
        }

        if (endingSequence != null)
        {
            endingSequence.Play();
        }
    }
}
