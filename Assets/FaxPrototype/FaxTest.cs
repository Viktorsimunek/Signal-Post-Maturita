using System.Collections;
using UnityEngine;
public class FaxTest : FaxRenderer
{
    private IEnumerator Start()
    {
        // Kamera jen na pozadi Game view.
        var preview = new GameObject("Debug pozadi", typeof(Camera));
        preview.transform.SetParent(transform, false);
        previewCamera = preview.GetComponent<Camera>();
        previewCamera.cullingMask = 0;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        // URP potrebuje nejdriv dokoncit prvni frame.
        yield return null;
        LoadDocument();
        BuildPaper();
        Regenerate();
    }

    private void Update()
    {
        if (pending && faxCamera != null) Regenerate();
    }

    private void OnGUI()
    {
        float height = Mathf.Max(1, Mathf.Min(Screen.height - 145, (Screen.width - 60) * 0.5f * Height / Width));
        float width = height * Width / Height;
        float left = (Screen.width - width * 2 - 20) / 2;
        GUI.Label(new Rect(left, 12, width, 24), "ORIGINAL");
        GUI.Label(new Rect(left + width + 20, 12, width, 24), "DEBUG FAX");
        if (original != null) GUI.DrawTexture(new Rect(left, 40, width, height), original);
        if (result != null) GUI.DrawTexture(new Rect(left + width + 20, 40, width, height), result);
        float center = Screen.width / 2f;
        GUI.Label(new Rect(center - 235, height + 50, 130, 24), "Kvalita: " + quality.ToString("0.00"));
        float nextQuality = GUI.HorizontalSlider(new Rect(center - 100, height + 58, 230, 20), quality, 0, 1);
        if (!Mathf.Approximately(nextQuality, quality))
        {
            quality = nextQuality;
            useQuality = true;
            pending = true;
        }
        if (GUI.Button(new Rect(center - 235, height + 82, 150, 30), "Nacist JSON"))
            LoadDocument();
        if (GUI.Button(new Rect(center - 75, height + 82, 150, 30), "Vygenerovat fax"))
            pending = true;
        if (GUI.Button(new Rect(center + 85, height + 82, 150, 30), "Jine poskozeni"))
        {
            seed++;
            pending = true;
        }
    }
    private void OnValidate() => pending = true;
    private void OnEnable() => pending = true;
}
