using UnityEngine;

public class FaxPaper : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Dat na nastenku";

    private FaxMessage message;
    private NoticeBoard noticeBoard;
    private bool canTake;

    public void Setup(FaxMessage faxMessage, NoticeBoard board)
    {
        message = faxMessage;
        noticeBoard = board;
    }

    public void SetCanTake(bool value)
    {
        canTake = value;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canTake && noticeBoard != null && message != null && isActiveAndEnabled;
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

        if (noticeBoard.AddFax(message))
        {
            Destroy(gameObject);
        }
    }
}
