using UnityEngine;
using UnityEngine.UI;

public class TransmitSymbolAnimator : MonoBehaviour
{
    [SerializeField] private TerminalPowerController terminal;
    [SerializeField] private Image image;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 8f;

    private void Awake()
    {
        if (image == null)
        {
            image = GetComponentInChildren<Image>(true);
        }

        if (image != null)
        {
            image.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (image != null)
        {
            image.enabled = false;
        }
    }

    private void Update()
    {
        if (image == null)
        {
            return;
        }

        bool show = terminal != null && terminal.ShowTransmitSymbol;
        image.enabled = show;

        if (!show || frames == null || frames.Length == 0)
        {
            return;
        }

        int length = frames.Length;
        int loopLength = length <= 1 ? 1 : (length * 2) - 2;
        int index = Mathf.FloorToInt(Time.time * fps) % loopLength;
        if (index >= length)
        {
            index = loopLength - index;
        }

        image.sprite = frames[index];
    }
}
