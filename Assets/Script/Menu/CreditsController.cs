using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CreditsController : MonoBehaviour
{
    private enum LineType { Text, PlayerName, Blank, Divider }

    [System.Serializable]
    private struct CreditLine
    {
        public LineType type;
        [TextArea(1, 3)] public string text;
        [Range(0f, 2f)] public float pause;
    }

    [Header("UI")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private CanvasGroup creditsCanvasGroup;
    [SerializeField] private CanvasGroup scanBars;
    [SerializeField] private RectTransform textRoot;

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;

    [Header("Credits")]
    [SerializeField] private string title = "SIGNAL POST";
    [SerializeField] private CreditLine[] lines =
    {
        new CreditLine { type = LineType.Text, text = "A GAME BY VIKTOR SIMUNEK", pause = 0.35f },
        new CreditLine { type = LineType.Blank },
        new CreditLine { type = LineType.Text, text = "OVERLAY FX BY NIIDAE", pause = 0.35f },
        new CreditLine { type = LineType.Text, text = "MUSIC BY PLAYARUS", pause = 0.35f },
        new CreditLine { type = LineType.Text, text = "SFX BY VIKTOR SIMUNEK", pause = 0.35f },
        new CreditLine { type = LineType.Text, text = "MODELS / ENVIRONMENT / PROPS BY VIKTOR SIMUNEK", pause = 0.35f },
        new CreditLine { type = LineType.Blank },
        new CreditLine { type = LineType.Text, text = "SPECIAL THANKS", pause = 0.2f },
        new CreditLine { type = LineType.PlayerName, text = "OPERATOR: {PLAYER}", pause = 0.35f },
        new CreditLine { type = LineType.Divider },
        new CreditLine { type = LineType.Text, text = "THANK YOU FOR KEEPING THE LINE ALIVE", pause = 0.5f },
        new CreditLine { type = LineType.Text, text = "END OF TRANSMISSION" }
    };

    [Header("Timing")]
    [SerializeField, Range(0f, 4f)] private float startDelay = 1f;
    [SerializeField, Range(0f, 8f)] private float titleHold = 2.4f;
    [SerializeField, Range(5f, 90f)] private float minimumDuration = 35f;
    [SerializeField, Range(0f, 4f)] private float fadeInTime = 1.1f;
    [SerializeField, Range(0f, 4f)] private float fadeOutTime = 1.4f;
    [SerializeField, Range(0.003f, 0.08f)] private float typeDelay = 0.014f;
    [SerializeField, Range(0f, 120f)] private float scrollPixelsPerSecond = 14f;
    [SerializeField, Range(0f, 4f)] private float endPause = 1.5f;

    [Header("Navigation")]
    [SerializeField] private string menuScene = "Menu";
    [SerializeField] private string skipText = "ENTER / SPACE TO SKIP";

    private string output = "";
    private Vector2 startPos;
    private bool leaving;
    private bool scrolling;

    private void Awake()
    {
        if (textRoot == null && creditsText != null)
        {
            textRoot = creditsText.rectTransform;
        }

        if (textRoot != null)
        {
            startPos = textRoot.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        leaving = false;
        scrolling = false;
        output = "";
        SetAlpha(0f);
        SetScan(0f);

        if (textRoot != null)
        {
            textRoot.anchoredPosition = startPos;
        }

        if (creditsText != null)
        {
            creditsText.text = "";
        }

        if (statusText != null)
        {
            statusText.text = skipText;
        }

        StartCoroutine(PlayCredits());
    }

    private void Update()
    {
        if (!leaving && SkipPressed())
        {
            StartCoroutine(Leave());
        }

        if (!leaving && scrolling && textRoot != null)
        {
            textRoot.anchoredPosition += Vector2.up * (scrollPixelsPerSecond * Time.unscaledDeltaTime);
        }
    }

    private IEnumerator PlayCredits()
    {
        yield return Fade(0f, 1f, fadeInTime);
        yield return Wait(startDelay);

        yield return Write(title);
        yield return Wait(titleHold);
        yield return FlashScan();

        float creditsStartTime = Time.unscaledTime;
        scrolling = true;

        output = title + "\n\n";
        Refresh();
        yield return Wait(0.8f);

        for (int i = 0; i < lines.Length && !leaving; i++)
        {
            string line = BuildLine(lines[i]);

            if (lines[i].type == LineType.Blank)
            {
                output += "\n";
                Refresh();
            }
            else
            {
                yield return Write(line);
                output += line + "\n";
                Refresh();
            }

            yield return Wait(lines[i].pause);
        }

        yield return Wait(Mathf.Max(endPause, minimumDuration - (Time.unscaledTime - creditsStartTime)));
        if (!leaving)
        {
            StartCoroutine(Leave());
        }
    }

    private IEnumerator Write(string line)
    {
        string baseText = output;
        for (int i = 0; i <= line.Length && !leaving; i++)
        {
            if (creditsText != null)
            {
                creditsText.text = baseText + Glitch(line.Substring(0, i));
            }

            yield return new WaitForSecondsRealtime(typeDelay);
        }
    }

    private string BuildLine(CreditLine line)
    {
        if (line.type == LineType.Divider)
        {
            return "--------------------------------";
        }

        if (line.type == LineType.PlayerName)
        {
            return Token(string.IsNullOrWhiteSpace(line.text) ? "OPERATOR: {PLAYER}" : line.text);
        }

        return Token(line.text);
    }

    private static string Token(string text)
    {
        return string.IsNullOrEmpty(text) ? "" : text.Replace("{PLAYER}", PlayerProfile.PlayerName);
    }

    private static string Glitch(string text)
    {
        if (string.IsNullOrEmpty(text) || Random.value > 0.22f)
        {
            return text;
        }

        const string noise = "#$%01/\\=-_";
        char[] chars = text.ToCharArray();
        int index = Random.Range(0, chars.Length);
        if (chars[index] != ' ')
        {
            chars[index] = noise[Random.Range(0, noise.Length)];
        }

        return new string(chars);
    }

    private void Refresh()
    {
        if (creditsText != null)
        {
            creditsText.text = output;
        }
    }

    private IEnumerator Leave()
    {
        leaving = true;
        if (statusText != null)
        {
            statusText.text = "RETURNING TO MENU";
        }

        yield return FlashScan();

        float fromAlpha = creditsCanvasGroup != null ? creditsCanvasGroup.alpha : 1f;
        float fromVolume = musicSource != null ? musicSource.volume : 0f;
        float time = Mathf.Max(0.01f, fadeOutTime);

        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / time);
            SetAlpha(Mathf.Lerp(fromAlpha, 0f, p));

            if (musicSource != null)
            {
                musicSource.volume = Mathf.Lerp(fromVolume, 0f, p);
            }

            yield return null;
        }

        SetAlpha(0f);
        if (musicSource != null)
        {
            musicSource.volume = 0f;
        }

        SceneManager.LoadScene(menuScene);
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        if (time <= 0.01f)
        {
            SetAlpha(to);
            yield break;
        }

        for (float t = 0f; t < time && !leaving; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)));
            yield return null;
        }

        if (!leaving)
        {
            SetAlpha(to);
        }
    }

    private IEnumerator FlashScan()
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            SetScan(Mathf.Sin((t / 0.35f) * Mathf.PI) * 0.8f);
            yield return null;
        }

        SetScan(0f);
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !leaving; t += Time.unscaledDeltaTime)
        {
            yield return null;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (creditsCanvasGroup != null)
        {
            creditsCanvasGroup.alpha = alpha;
        }
        else
        {
            SetTextAlpha(creditsText, alpha);
            SetTextAlpha(statusText, alpha);
        }
    }

    private void SetScan(float alpha)
    {
        if (scanBars == null)
        {
            return;
        }

        scanBars.alpha = alpha;
        scanBars.gameObject.SetActive(alpha > 0.01f);
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    private static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.escapeKey.wasPressedThisFrame ||
             keyboard.backspaceKey.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null &&
               (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame);
    }
}
