
using UnityEngine;

public class EnvironmentSystem : MonoBehaviour
{
    public static EnvironmentSystem Instance { get; private set; }

    // =========================================================
    // ACTIVE ZONE
    // =========================================================

public string ActiveZoneName { get; private set; } = "Travelling";

private EcosystemZone activeZone;
    // =========================================================
    // TEMPERATURE
    // =========================================================

    private float minTemperature = -15f;
    private float maxTemperature = 40f;

    [Header("Temperature")]
    [SerializeField] private float temperature = 20f;
    [SerializeField] private float idealTemperature = 20f;
    [SerializeField] private float temperatureThreshold = 3f;
    [SerializeField] private float freezingPoint = 0f;
    [SerializeField] private float coldSlowdownPower = 1.5f;

    [SerializeField, Range(0.05f, 0.5f)]
    private float minGrowthWhenCold = 0.2f;

    // =========================================================
    // HEAT
    // =========================================================

    [Header("Heat")]
    [SerializeField, Range(0f, 1f)]
    private float minGrowthWhenHot = 0.2f;

    // =========================================================
    // WATER
    // =========================================================

    [Header("Water")]
    [SerializeField, Range(0f, 100f)]
    private float water = 100f;

    [Header("Water Rates")]
    [SerializeField] private float waterReactionSpeed = 5f;
    [SerializeField] private float waterRecoverySpeed = 1f;

    [Header("Water Temperature Ranges")]
    [SerializeField] private float comfortableWaterMinTemperature = 15f;
    [SerializeField] private float comfortableWaterMaxTemperature = 24f;
    [SerializeField] private float coldWaterMinTemperature = 0f;
    [SerializeField] private float coldWaterMaxTemperature = 15f;
    [SerializeField, Range(0f, 1f)]
    private float coldWaterMinRecoveryMultiplier = 0.2f;
    [SerializeField] private float hotWaterStartTemperature = 25f;

    // =========================================================
    // POLLUTION
    // =========================================================

    [Header("Pollution")]
    [SerializeField, Range(0f, 1f)]
    private float pollution = 0f;

    // =========================================================
    // PUBLIC VALUES
    // =========================================================

    public float Temperature => temperature;
    public float MinTemperature => minTemperature;
    public float MaxTemperature => maxTemperature;
    public float IdealTemperature => idealTemperature;
    public float Water => water;
    public float Pollution => pollution;

    public float Bamboo
    {
        get
        {
            if (BambooManager.Instance == null)
                return 0f;

            return BambooManager.Instance.Percent;
        }
    }

    public float TemperatureGrowthFactor { get; private set; } = 1f;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one EnvironmentSystem exists!");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

private void Start()
{
    if (activeZone != null)
        ApplyZoneSettings(activeZone);
    else
    {
        water = Mathf.Clamp(water, 0f, 100f);
        pollution = Mathf.Clamp01(pollution);
        temperature = Mathf.Clamp(
            temperature,
            minTemperature,
            maxTemperature
        );
    }
}

    private void Update()
    {
        UpdateWater();
        TemperatureGrowthFactor = GetTemperatureGrowthFactor();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // APPLY ZONE SETTINGS
    // =========================================================

    public void ApplyZoneSettings(EcosystemZone zone)
{
    if (zone == null)
        return;

    // The active zone becomes the source of truth.
    activeZone = zone;
    ActiveZoneName = zone.zoneName;

    minTemperature = zone.minTemperature;
    maxTemperature = zone.maxTemperature;

    idealTemperature = zone.idealTemperature;
    temperatureThreshold = zone.temperatureThreshold;
    freezingPoint = zone.freezingPoint;
    coldSlowdownPower = zone.coldSlowdownPower;
    minGrowthWhenCold = zone.minGrowthWhenCold;
    minGrowthWhenHot = zone.minGrowthWhenHot;

    waterReactionSpeed = zone.waterReactionSpeed;
    waterRecoverySpeed = zone.waterRecoverySpeed;

    comfortableWaterMinTemperature =
        zone.comfortableWaterMinTemperature;
    comfortableWaterMaxTemperature =
        zone.comfortableWaterMaxTemperature;

    coldWaterMinTemperature =
        zone.coldWaterMinTemperature;
    coldWaterMaxTemperature =
        zone.coldWaterMaxTemperature;

    coldWaterMinRecoveryMultiplier =
        zone.coldWaterMinRecoveryMultiplier;

    hotWaterStartTemperature =
        zone.hotWaterStartTemperature;

    // Apply the zone's values.
    temperature = Mathf.Clamp(
        zone.temperature,
        minTemperature,
        maxTemperature
    );

    water = Mathf.Clamp(zone.water, 0f, 100f);
    pollution = Mathf.Clamp01(zone.pollution);

    TemperatureGrowthFactor = GetTemperatureGrowthFactor();

    Debug.Log("Active ecosystem: " + ActiveZoneName);
}

public void ExitZone(EcosystemZone zone)
{
    // Only leave the zone if it is still the active zone.
    if (activeZone != zone)
        return;

    activeZone = null;
    ActiveZoneName = "Travelling";

    Debug.Log("Panda is travelling between zones.");
}

    // =========================================================
    // TEMPERATURE
    // =========================================================

    public void SetTemperature(float newTemperature)
    {
        temperature = Mathf.Clamp(
            newTemperature,
            minTemperature,
            maxTemperature
        );
    }

    public void ChangeTemperature(float amount)
    {
        SetTemperature(temperature + amount);
    }

    private float GetTemperatureGrowthFactor()
    {
        float comfortableMin =
            idealTemperature - temperatureThreshold;

        float comfortableMax =
            idealTemperature + temperatureThreshold;

        if (temperature < comfortableMin)
        {
            float t = Mathf.Clamp01(
                Mathf.InverseLerp(
                    freezingPoint,
                    comfortableMin,
                    temperature
                )
            );

            return Mathf.Lerp(
                minGrowthWhenCold,
                1f,
                Mathf.Pow(t, coldSlowdownPower)
            );
        }

        if (temperature > comfortableMax)
        {
            float t = Mathf.InverseLerp(
                comfortableMax,
                maxTemperature,
                temperature
            );

            return Mathf.Lerp(1f, minGrowthWhenHot, t);
        }

        return 1f;
    }

    // =========================================================
    // WATER
    // =========================================================

    private void UpdateWater()
    {
        // Comfortable temperatures: water recovers quickly.
        if (temperature >= comfortableWaterMinTemperature &&
            temperature <= comfortableWaterMaxTemperature)
        {
            water += waterRecoverySpeed * Time.deltaTime;
        }
        // Slightly cold: water recovers more slowly.
        else if (temperature > coldWaterMinTemperature &&
                 temperature < coldWaterMaxTemperature)
        {
            float coldDistance = Mathf.InverseLerp(
                coldWaterMaxTemperature,
                coldWaterMinTemperature,
                temperature
            );

            float recoveryMultiplier = Mathf.Lerp(
                1f,
                coldWaterMinRecoveryMultiplier,
                coldDistance
            );

            water += waterRecoverySpeed *
                     recoveryMultiplier *
                     Time.deltaTime;
        }
        // Hot: water decreases increasingly quickly.
        else if (temperature >= hotWaterStartTemperature)
        {
            float hotRange = Mathf.Max(
                0.01f,
                maxTemperature - hotWaterStartTemperature
            );

            float hotSeverity = Mathf.Clamp01(
                (temperature - hotWaterStartTemperature) / hotRange
            );

            water -= Mathf.Pow(hotSeverity, 2f) *
                     waterReactionSpeed *
                     Time.deltaTime;
        }
        // Freezing: water decreases increasingly quickly.
        else if (temperature <= freezingPoint)
        {
            float coldRange = Mathf.Max(
                0.01f,
                freezingPoint - minTemperature
            );

            float coldSeverity = Mathf.Clamp01(
                (freezingPoint - temperature) / coldRange
            );

            water -= Mathf.Pow(coldSeverity, 2f) *
                     waterReactionSpeed *
                     Time.deltaTime;
        }

        water = Mathf.Clamp(water, 0f, 100f);
    }

    // =========================================================
    // WATER ACCESS
    // =========================================================

    public void SetWater(float value)
    {
        water = Mathf.Clamp(value, 0f, 100f);
    }

    public void ChangeWater(float amount)
    {
        SetWater(water + amount);
    }

    // =========================================================
    // POLLUTION
    // =========================================================

    public void SetPollution(float value)
    {
        pollution = Mathf.Clamp01(value);
    }

    public void ChangePollution(float amount)
    {
        SetPollution(pollution + amount);
    }
}