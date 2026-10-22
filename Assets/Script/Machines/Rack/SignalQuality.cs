using UnityEngine;

public class SignalQuality : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;

    [Header("Signal")]
    [SerializeField, Range(0f, 1f)] private float quality = 1f;
    [SerializeField, Range(0f, 1f)] private float goodSignalFrom = 0.7f;
    [SerializeField] private Transform signalPlate;
    [SerializeField] private float bestAngle = 0f;
    [SerializeField] private float driftSpeed = 0.18f;
    [SerializeField] private Vector2 retuneTime = new Vector2(180f, 300f);
    [SerializeField] private Vector2 newAngleDistance = new Vector2(60f, 130f);
    [SerializeField] private float goodAngleSize = 12f;
    [SerializeField] private float middleAngleSize = 70f;
    [SerializeField] private float middleQuality = 0.5f;
    [SerializeField, Range(0f, 0.4f)] private float noiseAmount = 0.08f;
    [SerializeField] private float noiseSpeed = 2f;

    [Header("Kontrolka")]
    [SerializeField] private Transform needle;
    [SerializeField] private Vector3 badRotation;
    [SerializeField] private Vector3 goodRotation;
    [SerializeField] private float needleSpeed = 6f;

    [Header("Ledky")]
    [SerializeField] private Renderer[] leds;
    [SerializeField] private Color offColor = Color.black;
    [SerializeField] private Color badColor = Color.red;
    [SerializeField] private Color middleColor = Color.yellow;
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private float emissionPower = 2f;

    private Material[] ledMaterials;
    private float plateAngle;
    private float targetAngle;
    private float nextRetuneTime;
    private float noiseOffset;

    public float Quality
    {
        get { return quality; }
    }

    public bool HasGoodSignal
    {
        get { return HasPower && quality >= goodSignalFrom; }
    }

    public bool CanSend
    {
        get { return HasPower && HasGoodSignal; }
    }

    public bool HasPower
    {
        get { return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered; }
    }

    private void Reset()
    {
        leds = GetComponentsInChildren<Renderer>(true);
    }

    private void Awake()
    {
        noiseOffset = Random.Range(0f, 1000f);

        if (signalPlate != null)
        {
            plateAngle = signalPlate.eulerAngles.y;
            bestAngle = plateAngle;
        }

        if (leds == null)
        {
            leds = new Renderer[0];
        }

        ledMaterials = new Material[leds.Length];

        for (int i = 0; i < leds.Length; i++)
        {
            if (leds[i] == null)
            {
                continue;
            }

            ledMaterials[i] = leds[i].material;
            ledMaterials[i].EnableKeyword("_EMISSION");
        }

        quality = Mathf.Clamp01(quality);
        bestAngle = NormalizeAngle(bestAngle);
        PickNewTargetAngle();
        SetNextRetuneTime();
        UpdateVisuals(true);
    }

    private void OnValidate()
    {
        quality = Mathf.Clamp01(quality);
        goodSignalFrom = Mathf.Clamp01(goodSignalFrom);
        driftSpeed = Mathf.Max(0f, driftSpeed);
        retuneTime = Sort(retuneTime);
        retuneTime.x = Mathf.Max(1f, retuneTime.x);
        retuneTime.y = Mathf.Max(retuneTime.x, retuneTime.y);
        newAngleDistance = Sort(newAngleDistance);
        newAngleDistance.x = Mathf.Clamp(newAngleDistance.x, 1f, 180f);
        newAngleDistance.y = Mathf.Clamp(newAngleDistance.y, newAngleDistance.x, 180f);
        goodAngleSize = Mathf.Max(1f, goodAngleSize);
        middleAngleSize = Mathf.Max(goodAngleSize + 1f, middleAngleSize);
        middleQuality = Mathf.Clamp01(middleQuality);
        noiseSpeed = Mathf.Max(0f, noiseSpeed);
        bestAngle = NormalizeAngle(bestAngle);

        if (!Application.isPlaying)
        {
            UpdateVisuals(true);
        }
    }

    private void Update()
    {
        if (!HasPower)
        {
            quality = 0f;
            UpdateVisuals(false);
            return;
        }

        MoveBestAngle();

        if (signalPlate != null)
        {
            SetPlateAngle(signalPlate.eulerAngles.y);
        }
        else
        {
            CountQuality();
        }

        UpdateVisuals(false);
    }

    public void SetQuality(float value)
    {
        if (!HasPower)
        {
            quality = 0f;
            UpdateVisuals(false);
            return;
        }

        quality = Mathf.Clamp01(value);
        UpdateVisuals(false);
    }

    public void AddQuality(float value)
    {
        SetQuality(quality + value);
    }

    public void SetPlateAngle(float value)
    {
        if (!HasPower)
        {
            quality = 0f;
            UpdateVisuals(false);
            return;
        }

        plateAngle = NormalizeAngle(value);
        CountQuality();
    }

    private void MoveBestAngle()
    {
        nextRetuneTime -= Time.deltaTime;

        if (nextRetuneTime <= 0f)
        {
            PickNewTargetAngle();
            SetNextRetuneTime();
        }

        bestAngle = Mathf.MoveTowardsAngle(bestAngle, targetAngle, driftSpeed * Time.deltaTime);
        bestAngle = NormalizeAngle(bestAngle);
    }

    private void PickNewTargetAngle()
    {
        float distance = Random.Range(newAngleDistance.x, newAngleDistance.y);
        float direction = Random.value < 0.5f ? -1f : 1f;
        targetAngle = NormalizeAngle(bestAngle + distance * direction);
    }

    private void SetNextRetuneTime()
    {
        nextRetuneTime = Random.Range(retuneTime.x, retuneTime.y);
    }

    private void CountQuality()
    {
        float distance = Mathf.Abs(Mathf.DeltaAngle(plateAngle, bestAngle));
        float baseQuality;

        if (distance <= goodAngleSize)
        {
            baseQuality = 1f;
            quality = AddNoise(baseQuality);
            return;
        }

        if (distance <= middleAngleSize)
        {
            baseQuality = Mathf.Lerp(1f, middleQuality, Mathf.InverseLerp(goodAngleSize, middleAngleSize, distance));
            quality = AddNoise(baseQuality);
            return;
        }

        baseQuality = Mathf.Lerp(middleQuality, 0f, Mathf.InverseLerp(middleAngleSize, 180f, distance));
        quality = AddNoise(baseQuality);
    }

    private float AddNoise(float value)
    {
        float noise = Mathf.PerlinNoise(noiseOffset, Time.time * noiseSpeed);
        float multiplier = Mathf.Lerp(1f - noiseAmount, 1f, noise);
        return Mathf.Clamp01(value * multiplier);
    }

    private void UpdateVisuals(bool instant)
    {
        Color color = GetSignalColor();
        UpdateNeedle(instant);
        UpdateLeds(color);
    }

    private Color GetSignalColor()
    {
        if (!HasPower)
        {
            return offColor;
        }

        if (quality <= middleQuality)
        {
            float t = middleQuality <= 0f ? 1f : quality / middleQuality;
            return Color.Lerp(badColor, middleColor, t);
        }

        float greenPart = Mathf.InverseLerp(middleQuality, 1f, quality);
        return Color.Lerp(middleColor, goodColor, greenPart);
    }

    private void UpdateNeedle(bool instant)
    {
        if (needle == null)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.Euler(Vector3.Lerp(badRotation, goodRotation, quality));

        if (instant)
        {
            needle.localRotation = targetRotation;
            return;
        }

        needle.localRotation = Quaternion.Slerp(needle.localRotation, targetRotation, needleSpeed * Time.deltaTime);
    }

    private void UpdateLeds(Color color)
    {
        if (ledMaterials == null)
        {
            return;
        }

        for (int i = 0; i < ledMaterials.Length; i++)
        {
            if (ledMaterials[i] == null)
            {
                continue;
            }

            if (ledMaterials[i].HasProperty(BaseColorId))
            {
                ledMaterials[i].SetColor(BaseColorId, color);
            }
            else if (ledMaterials[i].HasProperty(ColorId))
            {
                ledMaterials[i].SetColor(ColorId, color);
            }

            if (ledMaterials[i].HasProperty(EmissionColorId))
            {
                ledMaterials[i].SetColor(EmissionColorId, color * emissionPower);
            }
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private static Vector2 Sort(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }
}
