using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Test generovani faxu. Spustit samostatnou test scenu.
public class FaxTest : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private ComputeShader inkEffect;
    [Header("Text faxu")]
    [SerializeField] private string title = "INCOMING FAX";
    [SerializeField] private string sender = "FROM: SIGNAL CONTROL / 07";
    [TextArea(6, 16)]
    [SerializeField] private string message = "Transmission received.\n\nCheck the main power supply.\nKeep the receiver connected.\nWait for further instructions.\n\nEND OF MESSAGE";
    [Header("Tisk")]
    [Range(0, 0.5f)] [SerializeField] private float inkBleed = 0.5f;
    [Range(0, 0.08f)] [SerializeField] private float noise = 0.08f;
    [Range(0, 0.05f)] [SerializeField] private float dropouts = 0.05f;
    [Range(1, 3)] [SerializeField] private int bleedRadius = 3;
    [Range(0, 0.7f)] [SerializeField] private float unevenInk = 0.469f;
    [Range(0, 0.5f)] [SerializeField] private float ghosting = 0.272f;
    [SerializeField] private Vector2 ghostOffset = new Vector2(2, 1);
    [Header("Zkresleni (pixely)")]
    [Range(0, 8)] [SerializeField] private float warp = 2.21f;
    [Range(0, 4)] [SerializeField] private float rowJitter = 0.83f;
    [Range(0, 12)] [SerializeField] private float bandShift = 3.83f;
    [Header("Papir")]
    [Range(0, 0.4f)] [SerializeField] private float paperMottle = 0.05f;
    [Range(0, 0.4f)] [SerializeField] private float edgeDirt = 0.139f;
    [Range(0, 0.3f)] [SerializeField] private float streaks = 0.2095f;
    [Header("Textury (bila = ciste misto)")]
    [SerializeField] private Texture2D paperTexture;
    [Range(0, 1)] [SerializeField] private float paperTextureStrength = 0.181f;
    [SerializeField] private Vector2 paperTiling = Vector2.one;
    [Min(0.01f)] [SerializeField] private float paperSize = 0.84f;
    [SerializeField] private Vector2 paperOffset;
    [SerializeField] private Texture2D damageMask;
    [Range(0, 1)] [SerializeField] private float damageStrength = 0.055f;
    [SerializeField] private Vector2 damageTiling = new Vector2(2, 2);
    [Min(0.01f)] [SerializeField] private float damageSize = 1f;
    [SerializeField] private Vector2 damageOffset;
    [Header("Poskozeni")]
    [Min(0.1f)] [SerializeField] private float damageNoiseScale = 6f;
    [Range(0, 1)] [SerializeField] private float damageBreakup = 0.7f;
    [Range(0, 0.5f)] [SerializeField] private float damageWarp = 0.12f;
    [SerializeField] private bool randomizeDamagePlacement = true;
    [Header("Threshold")]
    [Range(0, 1)] [SerializeField] private float threshold = 0.65f;
    [Range(0, 0.5f)] [SerializeField] private float thresholdSoftness = 0.174f;
    [Range(0, 1)] [SerializeField] private float thresholdBlend = 0.96f;
    [Header("Nahoda")]
    [SerializeField] private int seed = 158;
    [SerializeField] private bool newSeedEachTime = true;

    private const int Width = 768, Height = 1024;
    private Camera faxCamera;
    private Camera previewCamera;
    private GameObject paper;
    private TextMeshProUGUI heading, from, body;
    private RenderTexture original, result;
    private bool pending = true;
    // Pri vypnuti testu se textura uvolni.
    public RenderTexture Result => result;

    private IEnumerator Start()
    {
        // URP potrebuje nejdriv dokoncit prvni frame.
        yield return null;
        BuildPaper();
        Regenerate();
    }

    private void BuildPaper()
    {
        // Kamera jen na pozadi Game view.
        var preview = new GameObject("Debug pozadi", typeof(Camera));
        preview.transform.SetParent(transform, false);
        previewCamera = preview.GetComponent<Camera>();
        previewCamera.cullingMask = 0;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        paper = new GameObject("Fax text", typeof(RectTransform), typeof(Canvas));
        paper.layer = 5;
        paper.transform.SetParent(transform, false);
        paper.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        paper.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, Height);
        heading = AddText("Heading", 64, 80, 640, 90, 48);
        from = AddText("Sender", 64, 200, 640, 80, 27);
        body = AddText("Message", 64, 320, 640, 640, 30);

        var cameraObject = new GameObject("Fax kamera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -10);
        faxCamera = cameraObject.GetComponent<Camera>();
        faxCamera.enabled = false;
        faxCamera.orthographic = true;
        faxCamera.orthographicSize = Height / 2f;
        faxCamera.aspect = (float)Width / Height;
        faxCamera.nearClipPlane = 0.1f;
        faxCamera.farClipPlane = 20;
        faxCamera.clearFlags = CameraClearFlags.SolidColor;
        faxCamera.backgroundColor = Color.white;
        faxCamera.cullingMask = 1 << 5; // UI vrstva
        faxCamera.allowHDR = false;
        faxCamera.allowMSAA = false;
        var data = faxCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        paper.GetComponent<Canvas>().worldCamera = faxCamera;
    }

    private TextMeshProUGUI AddText(string objectName, float x, float y,
        float width, float height, float size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(paper.transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.black;
        text.richText = false;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = size;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
        text.rectTransform.pivot = new Vector2(0, 1);
        text.rectTransform.anchoredPosition = new Vector2(x, -y);
        text.rectTransform.sizeDelta = new Vector2(width, height);
        return text;
    }

    public void Regenerate()
    {
        if (!Application.isPlaying || faxCamera == null) return;
        pending = false;
        if (!SystemInfo.supportsComputeShaders || inkEffect == null || font == null)
        {
            Debug.LogError("Fax test: chybi font/shader nebo GPU nepodporuje compute.", this);
            return;
        }
        if (original == null) original = CreateTexture("Fax original", false);
        if (result == null) result = CreateTexture("Fax processed", true);
        heading.text = title;
        from.text = sender;
        body.text = message;
        Canvas.ForceUpdateCanvases();
        heading.ForceMeshUpdate();
        from.ForceMeshUpdate();
        body.ForceMeshUpdate();
        RenderPipeline.SubmitRenderRequest(faxCamera,
            new UniversalRenderPipeline.SingleCameraRequest { destination = original });

        ApplyEffect();
    }

    // Nastaveni z Inspectoru poslu shaderu a jednou ho spustim.
    private void ApplyEffect()
    {
        int kernel = inkEffect.FindKernel("ProcessFax");
        inkEffect.SetTexture(kernel, "Source", original);
        inkEffect.SetTexture(kernel, "Result", result);
        inkEffect.SetFloat("InkBleed", inkBleed);
        inkEffect.SetFloat("Noise", noise);
        inkEffect.SetFloat("Dropouts", dropouts);
        inkEffect.SetInt("BleedRadius", bleedRadius);
        inkEffect.SetFloat("UnevenInk", unevenInk);
        inkEffect.SetFloat("Ghosting", ghosting);
        inkEffect.SetVector("GhostOffset", (Vector4)ghostOffset);
        inkEffect.SetFloat("Warp", warp);
        inkEffect.SetFloat("RowJitter", rowJitter);
        inkEffect.SetFloat("BandShift", bandShift);
        inkEffect.SetFloat("PaperMottle", paperMottle);
        inkEffect.SetFloat("EdgeDirt", edgeDirt);
        inkEffect.SetFloat("Streaks", streaks);
        inkEffect.SetTexture(kernel, "PaperTexture", paperTexture != null ? paperTexture : Texture2D.whiteTexture);
        inkEffect.SetTexture(kernel, "DamageMask", damageMask != null ? damageMask : Texture2D.whiteTexture);
        inkEffect.SetFloat("PaperTextureStrength", paperTextureStrength);
        inkEffect.SetFloat("DamageStrength", damageStrength);
        inkEffect.SetVector("PaperTiling", (Vector4)paperTiling / Mathf.Max(0.01f, paperSize));
        inkEffect.SetVector("DamageTiling", (Vector4)damageTiling / Mathf.Max(0.01f, damageSize));
        inkEffect.SetVector("PaperOffset", (Vector4)paperOffset);
        inkEffect.SetVector("DamageOffset", (Vector4)damageOffset);
        inkEffect.SetFloat("DamageNoiseScale", Mathf.Max(0.1f, damageNoiseScale));
        inkEffect.SetFloat("DamageBreakup", damageBreakup);
        inkEffect.SetFloat("DamageWarp", damageWarp);
        inkEffect.SetInt("RandomizeDamagePlacement", randomizeDamagePlacement ? 1 : 0);
        inkEffect.SetFloat("Threshold", threshold);
        inkEffect.SetFloat("ThresholdSoftness", thresholdSoftness);
        inkEffect.SetFloat("ThresholdBlend", thresholdBlend);
        if (newSeedEachTime) seed++;
        inkEffect.SetInt("Seed", seed);
        inkEffect.Dispatch(kernel, (Width + 7) / 8, (Height + 7) / 8, 1);
    }

    private RenderTexture CreateTexture(string textureName, bool writable)
    {
        var texture = new RenderTexture(Width, Height, writable ? 0 : 24,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        texture.name = textureName;
        texture.enableRandomWrite = writable;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Create();
        return texture;
    }

    private void Update()
    {
        // Pocitam jen po kliknuti, ne kazdy frame.
        if (pending && faxCamera != null) Regenerate();
    }

    private void OnGUI()
    {
        float height = Mathf.Max(1, Mathf.Min(Screen.height - 110, (Screen.width - 60) * 0.5f * Height / Width));
        float width = height * Width / Height;
        float left = (Screen.width - width * 2 - 20) / 2;
        GUI.Label(new Rect(left, 12, width, 24), "ORIGINAL");
        GUI.Label(new Rect(left + width + 20, 12, width, 24), "DEBUG FAX");
        if (original != null) GUI.DrawTexture(new Rect(left, 40, width, height), original);
        if (result != null) GUI.DrawTexture(new Rect(left + width + 20, 40, width, height), result);
        if (GUI.Button(new Rect(Screen.width / 2f - 100, height + 55, 200, 32), "Vygenerovat fax"))
            pending = true;
    }

    private void OnEnable() => pending = true;

    private void OnDisable()
    {
        Release(ref original);
        Release(ref result);
    }

    private void OnDestroy()
    {
        if (paper != null) Destroy(paper);
        if (faxCamera != null) Destroy(faxCamera.gameObject);
        if (previewCamera != null) Destroy(previewCamera.gameObject);
    }

    private static void Release(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        Destroy(texture);
        texture = null;
    }
}
