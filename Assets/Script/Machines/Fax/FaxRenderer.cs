using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Spolecne vykresleni dokumentu pro hru i testovaci nahled.
public class FaxRenderer : MonoBehaviour
{
    [SerializeField] protected TMP_FontAsset font;
    [SerializeField] protected ComputeShader inkEffect;
    [Range(1, 2)] [SerializeField] protected int renderScale = 2;
    [Header("Dokument JSON")]
    [SerializeField] protected TextAsset documentJson;
    [Header("Kvalita faxu")]
    [SerializeField] protected bool useQuality = true;
    [Range(0, 1)] [SerializeField] protected float quality = 0.75f;
    [Header("Hlavicka")]
    [SerializeField] protected bool CH_Logo = true;
    [SerializeField] protected Texture2D chLogo;
    [SerializeField] protected Vector2 logoSize = new Vector2(128, 115);
    [SerializeField] protected Vector2 logoInset = new Vector2(64, 56);
    [Header("Text faxu")]
    [SerializeField] protected string title = "INCOMING FAX";
    [SerializeField] protected string sender = "FROM: SIGNAL CONTROL / 07";
    [TextArea(6, 16)]
    [SerializeField] protected string message = "Transmission received.\n\nCheck the main power supply.\nKeep the receiver connected.\nWait for further instructions.\n\nEND OF MESSAGE";
    [Header("Tisk")]
    [Range(0, 0.5f)] [SerializeField] protected float inkBleed = 0.5f;
    [Range(0, 0.08f)] [SerializeField] protected float noise = 0.08f;
    [Range(0, 0.05f)] [SerializeField] protected float dropouts = 0.05f;
    [Range(1, 3)] [SerializeField] protected int bleedRadius = 3;
    [Range(0, 0.7f)] [SerializeField] protected float unevenInk = 0.469f;
    [Range(0, 0.5f)] [SerializeField] protected float ghosting = 0.272f;
    [SerializeField] protected Vector2 ghostOffset = new Vector2(2, 1);
    [Header("Smudge pred thresholdem (bila = rozmazani)")]
    [SerializeField] protected Texture2D smudgeMap;
    [Range(0, 1)] [SerializeField] protected float smudgeStrength = 0.45f;
    [Range(0, 4)] [SerializeField] protected float smudgeRadius = 1.4f;
    [Range(0.5f, 12)] [SerializeField] protected float smudgeScale = 5f;
    [Header("Zkresleni (pixely)")]
    [Range(0, 8)] [SerializeField] protected float warp = 2.21f;
    [Range(0, 4)] [SerializeField] protected float rowJitter = 0.83f;
    [Range(0, 12)] [SerializeField] protected float bandShift = 3.83f;
    [Header("Papir")]
    [Range(0, 0.4f)] [SerializeField] protected float paperMottle = 0.05f;
    [Range(0, 0.4f)] [SerializeField] protected float edgeDirt = 0.139f;
    [Range(0, 0.3f)] [SerializeField] protected float streaks = 0.2095f;
    [Header("Textury (bila = ciste misto)")]
    [SerializeField] protected Texture2D paperTexture;
    [Range(0, 1)] [SerializeField] protected float paperTextureStrength = 0.181f;
    [SerializeField] protected Vector2 paperTiling = Vector2.one;
    [Min(0.01f)] [SerializeField] protected float paperSize = 0.84f;
    [SerializeField] protected Vector2 paperOffset;
    [SerializeField] protected Texture2D damageMask;
    [Range(0, 1)] [SerializeField] protected float damageStrength = 0.055f;
    [SerializeField] protected Vector2 damageTiling = new Vector2(2, 2);
    [Min(0.01f)] [SerializeField] protected float damageSize = 1f;
    [SerializeField] protected Vector2 damageOffset;
    [Header("Poskozeni")]
    [Min(0.1f)] [SerializeField] protected float damageNoiseScale = 6f;
    [Range(0, 1)] [SerializeField] protected float damageBreakup = 0.7f;
    [Range(0, 0.5f)] [SerializeField] protected float damageWarp = 0.12f;
    [SerializeField] protected bool randomizeDamagePlacement = true;
    [Header("Threshold")]
    [Range(0, 1)] [SerializeField] protected float threshold = 0.65f;
    [Range(0, 0.5f)] [SerializeField] protected float thresholdSoftness = 0.174f;
    [Range(0, 1)] [SerializeField] protected float thresholdBlend = 0.96f;
    [Header("Nahoda")]
    [SerializeField] protected int seed = 158;
    [SerializeField] protected bool newSeedEachTime = false;

    protected const int Width = 768, Height = 1024;
    protected Camera faxCamera;
    protected Camera previewCamera;
    protected GameObject paper;
    protected TextMeshProUGUI heading, from, body;
    protected RawImage logo;
    protected RenderTexture original, result;
    protected bool pending = true;
    private readonly Dictionary<string, RenderTexture> generatedFaxes = new Dictionary<string, RenderTexture>();
    private readonly List<RenderTexture> generatedTextures = new List<RenderTexture>();

    public int GeneratedCount => generatedTextures.Count;

    public Texture Generate(FaxMessage fax)
    {
        if (fax == null || string.IsNullOrWhiteSpace(fax.body)) return null;
        if (fax.texture != null) return fax.texture;
        if (!string.IsNullOrEmpty(fax.id) && generatedFaxes.TryGetValue(fax.id, out RenderTexture saved))
        {
            fax.texture = saved;
            return saved;
        }

        title = fax.title;
        sender = fax.sender;
        message = fax.body;
        quality = Mathf.Clamp01(fax.quality);
        CH_Logo = fax.CH_Logo;
        seed = fax.seed;
        newSeedEachTime = false;
        useQuality = true;
        Regenerate();
        if (result == null) return null;

        // Kazdy papir potrebuje vlastni obraz. Pracovni textura se dale prepisuje.
        var texture = new RenderTexture(result.descriptor);
        texture.name = "Fax " + fax.id;
        texture.Create();
        Graphics.CopyTexture(result, texture);
        generatedTextures.Add(texture);
        if (!string.IsNullOrEmpty(fax.id)) generatedFaxes[fax.id] = texture;
        fax.texture = texture;
        return texture;
    }
    public RenderTexture Result => result;

    protected void BuildPaper()
    {
        if (paper != null) return;
        paper = new GameObject("Fax text", typeof(RectTransform), typeof(Canvas));
        paper.layer = 31;
        paper.transform.SetParent(transform, false);
        paper.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        paper.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, Height);
        heading = AddText("Heading", 64, 80, 640, 90, 48);
        from = AddText("Sender", 64, 200, 640, 80, 27);
        body = AddText("Message", 64, 280, 640, 680, 30);

        var logoObject = new GameObject("CH logo", typeof(RectTransform), typeof(RawImage));
        logoObject.layer = paper.layer;
        logoObject.transform.SetParent(paper.transform, false);
        logo = logoObject.GetComponent<RawImage>();
        logo.raycastTarget = false;
        logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(1, 1);
        logo.rectTransform.pivot = new Vector2(1, 1);

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
        faxCamera.cullingMask = 1 << 31; // Jen nase dokumenty, bez herniho HUDu.
        faxCamera.allowHDR = false;
        faxCamera.allowMSAA = false;
        var data = faxCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        paper.GetComponent<Canvas>().worldCamera = faxCamera;
    }

    protected TextMeshProUGUI AddText(string objectName, float x, float y,
        float width, float height, float size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 31;
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
        if (!Application.isPlaying) return;
        BuildPaper();
        pending = false;
        if (!SystemInfo.supportsComputeShaders || inkEffect == null || font == null)
        {
            Debug.LogError("Fax test: chybi font/shader nebo GPU nepodporuje compute.", this);
            return;
        }
        int renderWidth = Width * Mathf.Clamp(renderScale, 1, 2);
        if (original != null && original.width != renderWidth)
        {
            Release(ref original);
            Release(ref result);
        }
        if (original == null) original = CreateTexture("Fax original", false);
        if (result == null) result = CreateTexture("Fax processed", true);
        heading.text = ResolveText(title);
        from.text = ResolveText(sender);
        body.text = ResolveText(message);
        bool showLogo = CH_Logo && chLogo != null;
        logo.gameObject.SetActive(showLogo);
        logo.texture = chLogo;
        // Logo si ponecha pomer stran i pri zmene velikosti v Inspectoru.
        Vector2 size = new Vector2(Mathf.Max(1, logoSize.x), Mathf.Max(1, logoSize.y));
        if (chLogo != null)
        {
            float scale = Mathf.Min(size.x / chLogo.width, size.y / chLogo.height);
            size = new Vector2(chLogo.width, chLogo.height) * scale;
        }
        logo.rectTransform.sizeDelta = size;
        logo.rectTransform.anchoredPosition = new Vector2(-logoInset.x, -logoInset.y);
        float headingWidth = showLogo ? Mathf.Max(160, Width - logoInset.x - size.x - 88) : 640;
        heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, headingWidth);
        from.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            showLogo && logoInset.y + size.y > 200 ? headingWidth : 640);
        Canvas.ForceUpdateCanvases();
        heading.ForceMeshUpdate();
        from.ForceMeshUpdate();
        body.ForceMeshUpdate();
        if (body.isTextOverflowing)
            Debug.LogWarning("Fax: text se nevejde na papir. Zkrat zpravu nebo uprav rozlozeni.", this);
        if (CH_Logo && chLogo == null)
            Debug.LogWarning("Fax: CH_Logo je zapnute, ale chybi textura loga.", this);
        RenderPipeline.SubmitRenderRequest(faxCamera,
            new UniversalRenderPipeline.SingleCameraRequest { destination = original });

        ApplyEffect();
    }

    // Nastaveni z Inspectoru poslu shaderu a jednou ho spustim.
    protected void ApplyEffect()
    {
        int kernel = inkEffect.FindKernel("ProcessFax");
        inkEffect.SetTexture(kernel, "Source", original);
        inkEffect.SetTexture(kernel, "Result", result);
        inkEffect.SetInt("UseQuality", useQuality ? 1 : 0);
        inkEffect.SetFloat("Quality", Mathf.Clamp01(quality));
        inkEffect.SetFloat("InkBleed", inkBleed);
        inkEffect.SetFloat("Noise", noise);
        inkEffect.SetFloat("Dropouts", dropouts);
        inkEffect.SetInt("BleedRadius", bleedRadius);
        inkEffect.SetFloat("UnevenInk", unevenInk);
        inkEffect.SetFloat("Ghosting", ghosting);
        inkEffect.SetTexture(kernel, "SmudgeMap", smudgeMap != null ? smudgeMap : Texture2D.whiteTexture);
        inkEffect.SetInt("UseSmudgeMap", smudgeMap != null ? 1 : 0);
        inkEffect.SetFloat("SmudgeStrength", Mathf.Clamp01(smudgeStrength));
        inkEffect.SetFloat("SmudgeRadius", Mathf.Max(0, smudgeRadius));
        inkEffect.SetFloat("SmudgeScale", Mathf.Max(0.5f, smudgeScale));
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
        inkEffect.Dispatch(kernel, (result.width + 7) / 8, (result.height + 7) / 8, 1);
    }

    protected RenderTexture CreateTexture(string textureName, bool writable)
    {
        int scale = Mathf.Clamp(renderScale, 1, 2);
        var texture = new RenderTexture(Width * scale, Height * scale, writable ? 0 : 24,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        texture.name = textureName;
        texture.enableRandomWrite = writable;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Create();
        return texture;
    }

    public bool LoadDocument()
    {
        if (documentJson == null) return false;
        try
        {
            FaxDocument document = JsonUtility.FromJson<FaxDocument>(documentJson.text);
            if (document == null || document.body == null || float.IsNaN(document.quality)
                || float.IsInfinity(document.quality) || document.quality < 0 || document.quality > 1)
                throw new ArgumentException("Je potreba body a quality v rozsahu 0 az 1.");
            title = document.title;
            sender = document.sender;
            message = document.body;
            quality = document.quality;
            CH_Logo = document.CH_Logo;
            seed = document.seed;
            newSeedEachTime = false;
            useQuality = true;
            pending = true;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Fax JSON '" + documentJson.name + "': " + exception.Message, this);
            return false;
        }
    }

    public FaxArchiveRecord CreateArchiveRecord()
    {
        string id = documentJson != null ? documentJson.name : "manual_fax";
        if (documentJson != null)
        {
            var document = JsonUtility.FromJson<FaxDocument>(documentJson.text);
            if (document != null && !string.IsNullOrWhiteSpace(document.id)) id = document.id;
        }
        return new FaxArchiveRecord
        {
            documentId = id, title = ResolveText(title), sender = ResolveText(sender),
            body = ResolveText(message), quality = quality, seed = seed, CH_Logo = CH_Logo,
            width = result.width, height = result.height, exportedUtc = DateTime.UtcNow.ToString("O")
        };
    }

    public static string ResolveText(string text)
    {
        return (text ?? string.Empty).Replace("{PLAYER_NAME}", PlayerProfile.PlayerName)
            .Replace("\\n", "\n").Replace("**", string.Empty);
    }

    protected void OnDisable()
    {
        Release(ref original);
        Release(ref result);
    }

    protected void OnDestroy()
    {
        foreach (var texture in generatedTextures)
        {
            if (texture == null) continue;
            texture.Release();
            Destroy(texture);
        }
        generatedTextures.Clear();
        generatedFaxes.Clear();
        if (paper != null) Destroy(paper);
        if (faxCamera != null) Destroy(faxCamera.gameObject);
        if (previewCamera != null) Destroy(previewCamera.gameObject);
    }

    protected static void Release(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        Destroy(texture);
        texture = null;
    }
}
