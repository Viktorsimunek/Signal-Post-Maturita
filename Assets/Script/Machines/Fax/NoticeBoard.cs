using System.Collections.Generic;
using UnityEngine;

public class NoticeBoard : MonoBehaviour
{
    [SerializeField] private GameObject pinnedPaperPrefab;
    [SerializeField] private Transform[] slots;
    [SerializeField] private Vector3 paperRotation = new Vector3(0f, 270f, 0f);
    [SerializeField] private Vector3 lookPointOffset = new Vector3(0f, 0f, -0.6f);
    [SerializeField] private TextAsset scheduleJson;
    [SerializeField] private string resourcesJsonName = "FaxSchedule";
    [SerializeField] private string textureFolder = "Faxes/";

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pinSound;
    [SerializeField] private float pinVolume = 0.8f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    private readonly List<FaxMessage> faxes = new List<FaxMessage>();
    private readonly List<string> pinnedFaxIds = new List<string>();

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        RestorePinnedFaxes();
    }

    private void OnValidate()
    {
        pinVolume = Mathf.Max(0f, pinVolume);
        pitchRange = Sort(pitchRange);
    }

    public bool AddFax(FaxMessage message)
    {
        if (message == null || slots == null || faxes.Count >= slots.Length)
        {
            return false;
        }

        if (pinnedPaperPrefab == null)
        {
            Debug.LogWarning("NoticeBoard needs pinnedPaperPrefab assigned.", this);
            return false;
        }

        Transform slot = slots[faxes.Count];
        if (slot == null)
        {
            return false;
        }

        faxes.Add(message);
        string saveId = GetFaxSaveId(message, faxes.Count - 1);
        if (!pinnedFaxIds.Contains(saveId))
        {
            pinnedFaxIds.Add(saveId);
            GameProgress.SaveBoardFaxIds(pinnedFaxIds);
        }

        GameObject paper = CreatePaper(slot, message);

        FaxPaper faxPaper = paper.GetComponent<FaxPaper>();
        if (faxPaper != null)
        {
            faxPaper.enabled = false;
        }

        PaperLook paperLook = paper.GetComponent<PaperLook>();
        if (paperLook == null)
        {
            paperLook = paper.AddComponent<PaperLook>();
        }

        paperLook.SetFocusPointOffset(lookPointOffset);
        PlayPinSound();
        return true;
    }

    private GameObject CreatePaper(Transform slot, FaxMessage message)
    {
        GameObject paper = Instantiate(pinnedPaperPrefab, slot.position, Quaternion.Euler(paperRotation));
        paper.SetActive(true);
        paper.name = string.IsNullOrWhiteSpace(message.title) ? "Pinned Fax" : message.title;
        ApplyTexture(paper, message.texture);
        return paper;
    }

    private void ApplyTexture(GameObject paper, Texture2D texture)
    {
        Renderer renderer = paper.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            return;
        }

        if (texture != null)
        {
            renderer.material.mainTexture = texture;
            renderer.material.SetTexture("_BaseMap", texture);
        }
    }

    private void PlayPinSound()
    {
        if (pinSound == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(pinSound, pinVolume);
    }

    private static Vector2 Sort(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }

    private void RestorePinnedFaxes()
    {
        List<string> savedIds = GameProgress.LoadBoardFaxIds();
        if (savedIds.Count == 0 || pinnedPaperPrefab == null || slots == null || slots.Length == 0)
        {
            return;
        }

        Dictionary<string, FaxMessage> knownFaxes = LoadKnownFaxes();
        for (int i = 0; i < savedIds.Count && faxes.Count < slots.Length; i++)
        {
            string id = savedIds[i];
            if (string.IsNullOrWhiteSpace(id) || pinnedFaxIds.Contains(id) || !knownFaxes.TryGetValue(id, out FaxMessage message))
            {
                continue;
            }

            pinnedFaxIds.Add(id);
            faxes.Add(message);
            Transform slot = slots[faxes.Count - 1];
            if (slot == null)
            {
                continue;
            }

            GameObject paper = CreatePaper(slot, message);
            FaxPaper faxPaper = paper.GetComponent<FaxPaper>();
            if (faxPaper != null)
            {
                faxPaper.enabled = false;
            }

            PaperLook paperLook = paper.GetComponent<PaperLook>();
            if (paperLook == null)
            {
                paperLook = paper.AddComponent<PaperLook>();
            }

            paperLook.SetFocusPointOffset(lookPointOffset);
        }
    }

    private Dictionary<string, FaxMessage> LoadKnownFaxes()
    {
        Dictionary<string, FaxMessage> knownFaxes = new Dictionary<string, FaxMessage>();
        TextAsset json = scheduleJson != null ? scheduleJson : Resources.Load<TextAsset>(resourcesJsonName);
        if (json == null)
        {
            return knownFaxes;
        }

        FaxSchedule schedule = JsonUtility.FromJson<FaxSchedule>(json.text);
        FaxMessage[] scheduledFaxes = schedule != null && schedule.faxes != null ? schedule.faxes : new FaxMessage[0];
        for (int i = 0; i < scheduledFaxes.Length; i++)
        {
            FaxMessage fax = scheduledFaxes[i];
            string id = GetFaxSaveId(fax, i);
            if (string.IsNullOrWhiteSpace(id) || knownFaxes.ContainsKey(id))
            {
                continue;
            }

            if (fax.texture == null && !string.IsNullOrWhiteSpace(fax.textureName))
            {
                fax.texture = Resources.Load<Texture2D>(textureFolder + fax.textureName);
            }

            knownFaxes.Add(id, fax);
        }

        return knownFaxes;
    }

    private static string GetFaxSaveId(FaxMessage message, int index)
    {
        if (message != null && !string.IsNullOrWhiteSpace(message.id))
        {
            return message.id;
        }

        return "fax_index_" + index;
    }
}
