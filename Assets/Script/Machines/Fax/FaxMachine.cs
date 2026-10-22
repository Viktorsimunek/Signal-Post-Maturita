using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FaxMachine : MonoBehaviour
{
    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;

    [Header("Paper")]
    [SerializeField] private GameObject paperPrefab;
    [SerializeField] private NoticeBoard noticeBoard;
    [SerializeField] private Transform paperSpawnPoint;
    [SerializeField] private Transform paperEndPoint;
    [SerializeField] private float printTime = 3f;
    [SerializeField] private float paperStayTime;
    [SerializeField] private bool destroyOldPaper;

    [Header("Message")]
    [TextArea(3, 8)]
    [SerializeField] private string defaultMessage = "NO SIGNAL";

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip faxSound;
    [SerializeField] private float faxVolume = 0.8f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    private readonly Queue<FaxMessage> messages = new Queue<FaxMessage>();
    private readonly List<GameObject> printedPapers = new List<GameObject>();
    private Coroutine printRoutine;
    private bool isPrinting;

    public bool IsPrinting => isPrinting;
    public bool HasPower => (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
    public event Action<FaxMessage> FaxReadyToTake;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (noticeBoard == null)
        {
            noticeBoard = FindFirstObjectByType<NoticeBoard>();
        }
    }

    private void OnValidate()
    {
        printTime = Mathf.Max(0.1f, printTime);
        paperStayTime = Mathf.Max(0f, paperStayTime);
        faxVolume = Mathf.Max(0f, faxVolume);
        pitchRange = Sort(pitchRange);
    }

    private void OnDisable()
    {
        StopPrintingNow();
    }

    private void Update()
    {
        if (!HasPower)
        {
            if (isPrinting || printRoutine != null)
            {
                StopPrintingNow();
            }

            return;
        }

        if (messages.Count > 0 && printRoutine == null)
        {
            printRoutine = StartCoroutine(PrintQueue());
        }
    }

    public void ReceiveMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = defaultMessage;
        }

        FaxMessage fax = new FaxMessage
        {
            id = "manual",
            title = message
        };

        ReceiveFax(fax);
    }

    public void ReceiveFax(FaxMessage fax)
    {
        if (fax == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(fax.title))
        {
            fax.title = defaultMessage;
        }

        messages.Enqueue(fax);

        if (HasPower && printRoutine == null)
        {
            printRoutine = StartCoroutine(PrintQueue());
        }
    }

    public void ReceiveDefaultMessage()
    {
        ReceiveMessage(defaultMessage);
    }

    [ContextMenu("Test Print")]
    private void TestPrint()
    {
        ReceiveDefaultMessage();
    }

    private IEnumerator PrintQueue()
    {
        while (HasPower && messages.Count > 0)
        {
            yield return PrintPaper(messages.Dequeue());
        }

        printRoutine = null;
    }

    private IEnumerator PrintPaper(FaxMessage fax)
    {
        if (!HasPower)
        {
            yield break;
        }

        if (paperPrefab == null)
        {
            Debug.LogWarning("FaxMachine needs paperPrefab assigned.", this);
            yield break;
        }

        isPrinting = true;

        Transform startPoint = paperSpawnPoint != null ? paperSpawnPoint : transform;
        Transform endPoint = paperEndPoint != null ? paperEndPoint : startPoint;
        GameObject paper = CreatePaper(startPoint);
        printedPapers.Add(paper);
        FaxPaper faxPaper = SetupPaper(paper, fax);

        PlayFaxSound();

        float timer = 0f;
        while (timer < printTime)
        {
            if (!HasPower)
            {
                StopFaxSound();
                isPrinting = false;
                yield break;
            }

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / printTime);
            float printT = GetPrintProgress(t);
            paper.transform.position = Vector3.Lerp(startPoint.position, endPoint.position, printT);
            paper.transform.rotation = Quaternion.Slerp(startPoint.rotation, endPoint.rotation, printT);
            yield return null;
        }

        paper.transform.SetPositionAndRotation(endPoint.position, endPoint.rotation);
        faxPaper.SetCanTake(true);
        FaxReadyToTake?.Invoke(fax);
        StopFaxSound();

        if (paperStayTime > 0f)
        {
            Destroy(paper, paperStayTime);
        }

        if (destroyOldPaper)
        {
            ClearMissingPapers();
            while (printedPapers.Count > 1)
            {
                GameObject oldPaper = printedPapers[0];
                printedPapers.RemoveAt(0);

                if (oldPaper != null)
                {
                    Destroy(oldPaper);
                }
            }
        }

        isPrinting = false;
    }

    private void StopPrintingNow()
    {
        if (printRoutine != null)
        {
            StopCoroutine(printRoutine);
            printRoutine = null;
        }

        StopFaxSound();
        isPrinting = false;
    }

    private FaxPaper SetupPaper(GameObject paper, FaxMessage fax)
    {
        ApplyTexture(paper, fax.texture);

        FaxPaper faxPaper = paper.GetComponent<FaxPaper>();
        if (faxPaper == null)
        {
            faxPaper = paper.AddComponent<FaxPaper>();
        }

        faxPaper.Setup(fax, noticeBoard);
        faxPaper.SetCanTake(false);

        if (paper.GetComponent<Collider>() == null)
        {
            paper.AddComponent<BoxCollider>();
        }

        return faxPaper;
    }

    private GameObject CreatePaper(Transform startPoint)
    {
        GameObject paper = Instantiate(paperPrefab, startPoint.position, startPoint.rotation);
        paper.SetActive(true);
        return paper;
    }

    private float GetPrintProgress(float t)
    {
        return t * t * (3f - 2f * t);
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

    private void PlayFaxSound()
    {
        if (audioSource == null || faxSound == null)
        {
            return;
        }

        audioSource.clip = faxSound;
        audioSource.loop = true;
        audioSource.volume = faxVolume;
        audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        audioSource.Play();
    }

    private void StopFaxSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    private void ClearMissingPapers()
    {
        for (int i = printedPapers.Count - 1; i >= 0; i--)
        {
            if (printedPapers[i] == null)
            {
                printedPapers.RemoveAt(i);
            }
        }
    }

    private static Vector2 Sort(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }
}
