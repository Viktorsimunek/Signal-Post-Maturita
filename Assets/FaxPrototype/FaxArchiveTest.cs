using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

[RequireComponent(typeof(FaxTest))]
public class FaxArchiveTest : MonoBehaviour
{
    public bool IsSaving { get; private set; }
    public string LastFolder { get; private set; }
    public string LastSummary { get; private set; } = "Pripraveno k testu ulozeni.";
    public string LastReportJson { get; private set; }
    private readonly Queue<float> idleFrames = new Queue<float>();
    private readonly List<float> saveFrames = new List<float>();

    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        if (IsSaving) saveFrames.Add(ms);
        else
        {
            idleFrames.Enqueue(ms);
            if (idleFrames.Count > 120) idleFrames.Dequeue();
        }
    }

    public void SaveTestFax()
    {
        if (!Application.isPlaying || IsSaving) return;
        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            LastSummary = "GPU nepodporuje asynchronni readback.";
            Debug.LogError(LastSummary, this);
            return;
        }
        StartCoroutine(SaveRoutine());
    }

    private IEnumerator SaveRoutine()
    {
        IsSaving = true;
        saveFrames.Clear();
        var report = new SaveReport();
        report.baselineFrameCount = idleFrames.Count;
        report.baselineMedianMs = Percentile(idleFrames.ToArray(), 0.5f);
        report.baselineP95Ms = Percentile(idleFrames.ToArray(), 0.95f);
        var total = Stopwatch.StartNew();
        var clock = Stopwatch.StartNew();
        var fax = GetComponent<FaxTest>();
        LastSummary = "Renderuji fax...";
        fax.Regenerate();
        report.renderSubmitMs = clock.Elapsed.TotalMilliseconds;
        if (fax.Result == null) { Fail("Nejdrive spust nahled faxu."); yield break; }
        FaxArchiveRecord record = fax.CreateArchiveRecord();
        int width = fax.Result.width, height = fax.Result.height;
        report.width = width; report.height = height;
        clock.Restart();
        var request = AsyncGPUReadback.Request(fax.Result, 0, TextureFormat.RGBA32);
        report.readbackSubmitMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        LastSummary = "Cekam na GPU bez blokovani hry...";
        while (!request.done) yield return null;
        report.readbackWaitMs = clock.Elapsed.TotalMilliseconds;
        if (request.hasError) { Fail("GPU readback selhal."); yield break; }
        clock.Restart();
        byte[] pixels = request.GetData<byte>().ToArray();
        report.readbackCopyMs = clock.Elapsed.TotalMilliseconds;

        LastFolder = Path.Combine(Application.persistentDataPath, "FaxArchiveTests",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        string folder = LastFolder;
        string metadata = JsonUtility.ToJson(record, true);
        LastSummary = "Komprimuji a ukladam PNG na pracovnim vlakne...";
        Task<WorkerResult> task = Task.Run(() =>
        {
            var workerClock = Stopwatch.StartNew();
            byte[] png = FaxPalettePng.Encode(pixels, width, height);
            double encodeMs = workerClock.Elapsed.TotalMilliseconds;
            workerClock.Restart();
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "fax.png.tmp"), png);
            File.Move(Path.Combine(folder, "fax.png.tmp"), Path.Combine(folder, "fax.png"));
            File.WriteAllText(Path.Combine(folder, "document.json.tmp"), metadata);
            File.Move(Path.Combine(folder, "document.json.tmp"), Path.Combine(folder, "document.json"));
            return new WorkerResult { pngBytes = png.Length, encodeMs = encodeMs, writeMs = workerClock.Elapsed.TotalMilliseconds };
        });
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { Fail(task.Exception.GetBaseException().Message); yield break; }
        report.encodeWorkerMs = task.Result.encodeMs;
        report.writeWorkerMs = task.Result.writeMs;
        report.pngBytes = task.Result.pngBytes;
        // Dalsi frame zachyti i snimek, ve kterem se operace dokoncila.
        yield return null;
        report.totalWallMs = total.Elapsed.TotalMilliseconds;
        report.saveFrameCount = saveFrames.Count;
        report.saveMedianMs = Percentile(saveFrames.ToArray(), 0.5f);
        report.saveP95Ms = Percentile(saveFrames.ToArray(), 0.95f);
        report.saveMaxFrameMs = Percentile(saveFrames.ToArray(), 1f);
        LastReportJson = JsonUtility.ToJson(report, true);
        string reportText = LastReportJson;
        // Ani zapis reportu nepatri na hlavni vlakno.
        var writeReport = Task.Run(() => File.WriteAllText(Path.Combine(folder, "timing.json"), reportText));
        while (!writeReport.IsCompleted) yield return null;
        if (writeReport.IsFaulted) { Fail(writeReport.Exception.GetBaseException().Message); yield break; }
        IsSaving = false;
        LastSummary = "Ulozeno " + (report.pngBytes / 1024f).ToString("F0") + " KiB; render CPU "
            + report.renderSubmitMs.ToString("F2") + " ms; nejdelsi frame " + report.saveMaxFrameMs.ToString("F2") + " ms.";
        Debug.Log("Fax archive test: " + folder + "\n" + LastReportJson, this);
    }

    private void Fail(string message)
    {
        IsSaving = false;
        LastSummary = "Ulozeni selhalo: " + message;
        Debug.LogError(LastSummary, this);
    }

    private static float Percentile(float[] values, float fraction)
    {
        if (values.Length == 0) return 0;
        Array.Sort(values);
        return values[Mathf.Clamp(Mathf.CeilToInt((values.Length - 1) * fraction), 0, values.Length - 1)];
    }

    private void OnGUI()
    {
        bool previous = GUI.enabled;
        GUI.enabled = !IsSaving;
        if (GUI.Button(new Rect(Screen.width - 205, 4, 200, 28), "Ulozit test 4 barev PNG")) SaveTestFax();
        GUI.enabled = previous;
        GUI.Label(new Rect(12, Screen.height - 25, Screen.width - 24, 24), LastSummary);
    }

    private class WorkerResult { public int pngBytes; public double encodeMs, writeMs; }

    [Serializable]
    private class SaveReport
    {
        public int width, height, pngBytes, baselineFrameCount, saveFrameCount;
        public float baselineMedianMs, baselineP95Ms, saveMedianMs, saveP95Ms, saveMaxFrameMs;
        public double renderSubmitMs, readbackSubmitMs, readbackWaitMs, readbackCopyMs;
        public double encodeWorkerMs, writeWorkerMs, totalWallMs;
    }
}

[Serializable]
public class FaxArchiveRecord
{
    public int schemaVersion = 1;
    public string generatorVersion = "fax-prototype-smudge-v1";
    public string documentId, title, sender, body, exportedUtc;
    public float quality;
    public int seed, width, height;
    public bool CH_Logo;
    public string image = "fax.png";
    public string encoding = "indexed-png-2bit-bayer4x4";
}
