using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SimpleInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private string interactionPrompt = "Interact";
    [SerializeField] private bool disableAfterUse;
    [SerializeField] private UnityEvent onInteract;

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canInteract && isActiveAndEnabled;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        return interactionPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        onInteract?.Invoke();

        if (disableAfterUse)
        {
            canInteract = false;
        }
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    public void SetInteractionPrompt(string prompt)
    {
        interactionPrompt = prompt;
    }
}
