public interface IInteractable
{
    bool CanInteract(PlayerInteractor interactor);
    string GetInteractionPrompt(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
