using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TowerLadder : MonoBehaviour, IInteractable
{
    public Transform lowerLanding;
    public Transform upperLanding;
    public Transform lowerRung;
    public Transform upperRung;
    public bool descend;
    [Min(.1f)] public float climbSpeed = 2.5f;
    private FirstPersonController player;
    private PlayerInteractor interactor;
    private PlayerInteractionHud hud;
    private bool movementBefore, collisionBefore, interactionBefore;
    private Vector3 safePosition;
    public bool IsClimbing => player != null;

    public bool CanInteract(PlayerInteractor actor) => !IsClimbing && actor != null && actor.GetComponent<FirstPersonController>().IsMovementEnabled;
    public string GetInteractionPrompt(PlayerInteractor actor) => descend ? "Slézt po žebříku" : "Vylézt na další plošinu";
    public void Interact(PlayerInteractor actor)
    {
        if (!CanInteract(actor) || !lowerLanding || !upperLanding || !lowerRung || !upperRung) return;
        player = actor.GetComponent<FirstPersonController>(); interactor = actor; hud = actor.GetComponent<PlayerInteractionHud>();
        safePosition = actor.transform.position;
        movementBefore = player.IsMovementEnabled; collisionBefore = player.IsCharacterControllerEnabled; interactionBefore = actor.IsInteractionEnabled;
        player.SetMovementEnabled(false); player.SetCharacterControllerEnabled(false); actor.SetInteractionEnabled(false);
        hud?.SetActionHint(this, "W / S — nahoru / dolů • levá páčka — lezení");
        StartCoroutine(Climb());
    }
    private IEnumerator Climb()
    {
        var cc = player.Controller;
        float footOffset = (cc.height * .5f - cc.center.y) * player.transform.lossyScale.y;
        float t = descend ? 1f : 0f;
        float length = Vector3.Distance(lowerRung.position, upperRung.position);
        player.transform.position = Vector3.Lerp(lowerRung.position, upperRung.position, t) + Vector3.up * footOffset;
        // Requiring a frame of movement prevents an instant exit at the starting landing.
        yield return null;
        while (player)
        {
            float input = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue().y : 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input = 1;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input = -1;
            }
            if (Mathf.Abs(input) > .15f)
            {
                t = Mathf.Clamp01(t + input * climbSpeed * Time.deltaTime / Mathf.Max(.01f, length));
                player.transform.position = Vector3.Lerp(lowerRung.position, upperRung.position, t) + Vector3.up * footOffset;
                if ((t >= 1 && input > 0) || (t <= 0 && input < 0))
                {
                    safePosition = (t >= 1 ? upperLanding : lowerLanding).position + Vector3.up * footOffset;
                    Restore(); yield break;
                }
            }
            yield return null;
        }
    }
    private void Restore()
    {
        if (!player) return;
        player.transform.position = safePosition;
        player.SetCharacterControllerEnabled(collisionBefore); player.SetMovementEnabled(movementBefore);
        if (interactor) interactor.SetInteractionEnabled(interactionBefore);
        hud?.ClearActionHint(this); player = null; interactor = null; hud = null;
    }
    private void OnDisable() { StopAllCoroutines(); Restore(); }
}
