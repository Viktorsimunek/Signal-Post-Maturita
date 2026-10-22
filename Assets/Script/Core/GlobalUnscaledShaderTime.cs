using UnityEngine;

public sealed class GlobalUnscaledShaderTime : MonoBehaviour
{
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");
    private static GlobalUnscaledShaderTime instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("Global Unscaled Shader Time");
        host.hideFlags = HideFlags.HideAndDontSave;
        instance = host.AddComponent<GlobalUnscaledShaderTime>();
        DontDestroyOnLoad(host);
        UpdateGlobalTime();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        UpdateGlobalTime();
    }

    private void Update()
    {
        UpdateGlobalTime();
    }

    private static void UpdateGlobalTime()
    {
        Shader.SetGlobalFloat(UnscaledTimeId, Time.unscaledTime);
    }
}
