using UnityEngine;

public class EnvironmentSystem : MonoBehaviour
{
    public static EnvironmentSystem Instance { get; private set; }

    // =========================================================
    // ACTIVE ZONE
    // =========================================================

    public string ActiveZoneName { get; private set; } = "Travelling";

    private EcosystemZone activeZone;

    public EcosystemZone ActiveZone => activeZone;

    // =========================================================
    // TEMPERATURE
    // =========================================================

    private float minTemperature = -15f;
    private float maxTemperature = 40f;

    [Header("Temperature")]
    [SerializeField] private float temperature = 20f;
    [SerializeField] private float temperatureChangeSpeed = 2f;
    [SerializeField] private float idealTemperature = 20f;
    [SerializeField] private float temperatureThreshold = 3f;
    [SerializeField] private float freezingPoint = 0f;
    [SerializeField] private float coldSlowdownPower = 1.5f;

    [SerializeField, Range(0.05f, 0.5f)]
    private float minGrowthWhenCold = 0.2f;

    private float targetTemperature = 20f;
    private bool isTravelling = false;

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

    public float TemperatureGrowthFactor { get; private set; } = 1f;

    public float Bamboo
    {
        get
        {
            if (activeZone == null)
                return 0f;

            if (BambooManager.Instance == null)
                return 0f;

            return BambooManager.Instance.GetPercentForZone(activeZone);
        }
    }

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
        {
            ApplyZoneSettings(activeZone);
        }
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

        targetTemperature = temperature;
    }

    private void Update()
    {
        // Temperaturen bevæger sig automatisk mod destinationens
        // temperatur, mens pandaen rejser.
        if (isTravelling)
        {
            float before = temperature;

            temperature = Mathf.MoveTowards(
                temperature,
                targetTemperature,
                temperatureChangeSpeed * Time.deltaTime
            );

            if (!Mathf.Approximately(before, temperature))
            {
                TelemetryManager.Instance?.LogTemperature(
                    temperature,
                    "travel"
                );
            }
        }

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

        activeZone = zone;
        ActiveZoneName = zone.zoneName;

        // Temperaturindstillinger.
        minTemperature = zone.minTemperature;
        maxTemperature = zone.maxTemperature;
        idealTemperature = zone.idealTemperature;
        temperatureThreshold = zone.temperatureThreshold;
        freezingPoint = zone.freezingPoint;
        coldSlowdownPower = zone.coldSlowdownPower;
        minGrowthWhenCold = zone.minGrowthWhenCold;
        minGrowthWhenHot = zone.minGrowthWhenHot;

        // Vandindstillinger.
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

        // Stop temperaturens automatiske overgang ved ankomst.
        isTravelling = false;

        // Behold den aktuelle temperatur, så spilleren kan justere den.
        targetTemperature = temperature;

        // Den nye zone starter med sine egne værdier.
        water = Mathf.Clamp(zone.water, 0f, 100f);
        pollution = Mathf.Clamp01(zone.pollution);

        TemperatureGrowthFactor = GetTemperatureGrowthFactor();

        Debug.Log(
            "Active ecosystem: " + ActiveZoneName +
            ". Temperature target: " + targetTemperature + "°C"
        );
    }

    public void ExitZone(EcosystemZone zone)
    {
        if (activeZone != zone)
            return;

        activeZone = null;
        ActiveZoneName = "Travelling";

        // Ingen tilgængelig vandressource under rejsen.
        water = 0f;

        Debug.Log("Panda is travelling between zones. Water: 0");
    }

    // =========================================================
    // TEMPERATURE
    // =========================================================

    public void SetTemperature(
        float newTemperature,
        string source = "player"
    )
    {
        temperature = Mathf.Clamp(
            newTemperature,
            minTemperature,
            maxTemperature
        );

        targetTemperature = temperature;

        TemperatureGrowthFactor = GetTemperatureGrowthFactor();

        TelemetryManager.Instance?.LogTemperature(
            temperature,
            source
        );
    }

    public void ChangeTemperature(
        float amount,
        string source = "player"
    )
    {
        SetTemperature(temperature + amount, source);
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
        if (activeZone == null)
        {
            water = 0f;
            return;
        }

        // Frost ændrer vandets tilstand, men bevarer vandmængden.
        if (temperature <= freezingPoint)
        {
            water = Mathf.Clamp(water, 0f, 100f);
            return;
        }

        // Behagelig temperatur: vandet genopfyldes.
        if (temperature >= comfortableWaterMinTemperature &&
            temperature <= comfortableWaterMaxTemperature)
        {
            water += waterRecoverySpeed * Time.deltaTime;
        }
        // Køligt: langsommere genopfyldning.
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
        // Varmt: vandet fordamper.
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

        water = Mathf.Clamp(water, 0f, 100f);
    }

    // =========================================================
    // WATER ACCESS
    // =========================================================

    public void SetWater(float value)
    {
        // Lås også vandtilførsel fra eksempelvis regn under frost.
        if (temperature <= freezingPoint)
            return;

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

    // =========================================================
    // TRAVEL
    // =========================================================

    public void BeginTravelToZone(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName))
            return;

        EcosystemZone[] zones =
            FindObjectsByType<EcosystemZone>(
                FindObjectsSortMode.None
            );

        foreach (EcosystemZone zone in zones)
        {
            if (zone == null)
                continue;

            if (string.Equals(
                zone.zoneName?.Trim(),
                zoneName.Trim(),
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                isTravelling = true;

                targetTemperature = Mathf.Clamp(
                    zone.temperature,
                    zone.minTemperature,
                    zone.maxTemperature
                );

                Debug.Log(
                    "EnvironmentSystem: Temperature transition started. " +
                    "Current = " + temperature.ToString("F1") +
                    "°C, destination = " +
                    targetTemperature.ToString("F1") + "°C."
                );

                return;
            }
        }

        Debug.LogWarning(
            "EnvironmentSystem: Could not find destination zone '" +
            zoneName + "' for the temperature transition."
        );
    }
}