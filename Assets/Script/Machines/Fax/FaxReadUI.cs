using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FaxReadUI : MonoBehaviour
{
    [SerializeField] private Animator panel;
    [SerializeField] private Animator viewOffsetPrefab;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text sender;
    [SerializeField] private TMP_Text body;
    [SerializeField] private UnityEngine.UI.ScrollRect scroll;

    private readonly TextShuffle shuffle = new TextShuffle();
    private Animator viewOffset;
    private string heading;
    private LookFocus focus;
    private bool reading;
    private bool open;

    public void Show(FaxMessage fax, LookFocus lookFocus)
    {
        if (fax == null || string.IsNullOrWhiteSpace(fax.body)) return;
        if (viewOffset == null)
        {
            var camera = Camera.main.transform;
            viewOffset = Instantiate(viewOffsetPrefab, camera.parent, false);
            camera.SetParent(viewOffset.transform, true);
        }
        focus = lookFocus;
        GetComponent<Canvas>().worldCamera = Camera.main;
        heading = FaxRenderer.ResolveText(fax.title);
        title.text = heading;
        sender.text = FaxRenderer.ResolveText(fax.sender);
        body.text = FaxRenderer.ResolveText(fax.body);
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        reading = true;
        UpdateHint();
    }

    public void Hide()
    {
        reading = false;
        SetOpen(false);
        if (focus != null) focus.SetActionHint("");
        focus = null;
    }

    private void Update()
    {
        if (!reading || Time.timeScale == 0f) return;
        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && GameSettings.WasKeyboardBindingPressed(GameSettings.RebindableAction.ReadFax);
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && GameSettings.WasGamepadBindingPressed(GameSettings.RebindableAction.ReadFax);
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        if (keyboardPressed || gamepadPressed) SetOpen(!open);
        UpdateHint();
        if (open && CanScroll && GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            float input = Gamepad.current.rightStick.y.ReadValue();
            float height = scroll.content.rect.height - scroll.viewport.rect.height;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + input * 450f * Time.unscaledDeltaTime / height);
        }
    }

    private bool CanScroll => scroll.content.rect.height > scroll.viewport.rect.height + 1f;

    private void UpdateHint()
    {
        bool gamepad = InputPromptDisplay.IsGamepadScheme(this);
        string binding = GameSettings.GetBindingDisplay(GameSettings.RebindableAction.ReadFax, gamepad);
        string hint = "[" + binding + "] " + (open ? "Close text" : "Read text");
        if (open && CanScroll) hint += gamepad ? "\n[Right stick] Scroll" : "\n[Mouse wheel] Scroll";
        focus.SetActionHint(hint);
    }

    private void SetOpen(bool value)
    {
        open = value;
        panel.SetBool("Open", value);
        if (viewOffset != null) viewOffset.SetBool("Open", value);
        StopAllCoroutines();
        if (open) StartCoroutine(shuffle.Play(title, heading, 0.16f, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"));
    }

    private void OnDisable()
    {
        Hide();
    }
}
