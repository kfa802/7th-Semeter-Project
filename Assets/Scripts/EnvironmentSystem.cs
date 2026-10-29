using UnityEngine;

/// <summary>
/// Runs the environment for the zone the panda is currently in.
/// ALL settings (start temperature, water, growth, thresholds...) live on
/// each EcosystemZone. This script only holds the active zone's values at
/// runtime, so there is nothing to tune here except the travel speed.
/// </summary>
public class EnvironmentSystem : MonoBehaviour
{
    public static EnvironmentSystem Instance { get; private set; }

    // =========================================================
    // GLOBAL SETTING
    // =========================================================

    [Header("Travel")]
    [Tooltip("How fast (°C per second) the temperature moves towards " +
             "the destination zone's temperature while travelling.")]
    [SerializeField] private float temperatureChangeSpeed = 2f;

    // =========================================================
    // ACTIVE ZONE
    // =========================================================

    public string ActiveZoneName { get; private set; } = "Travelling";

    private EcosystemZone activeZone;

    public EcosystemZone ActiveZone => activeZone;

    // =========================================================
    // RUNTIME VALUES (copied from the active zone - not tuned here)
    // =========================================================

    // Temperature.
    private float minTemperature = -15f;
    private float maxTemperature = 40f;
    private float temperature = 20f;
    private float idealTemperature = 20f;
    private float temperatureThreshold = 3f;
    private float freezingPoint = 0f;
    private float coldSlowdownPower = 1.5f;
    private float minGrowthWhenCold = 0.2f;
    private float minGrowthWhenHot = 0.2f;

    private float targetTemperature = 20f;
    private bool isTravelling = false;

    // Water.
    private float water = 0f;
    private float waterReactionSpeed = 5f;
    private float waterRecoverySpeed = 1f;
    private float comfortableWaterMinTemperature = 15f;
    private float comfortableWaterMaxTemperature = 24f;
    private float idealWaterTemperature = 20f;
    private float comfortableEdgeRecoveryMultiplier = 0.5f;
    private float coldWaterMinTemperature = 0f;
    private float coldWaterMaxTemperature = 15f;
    private float coldWaterMinRecoveryMultiplier = 0.2f;
    private float hotWaterStartTemperature = 25f;
    private float coldWaterLossStartTemperature = 0f;
    private float coldWaterLossSpeed = 5f;
    private float coldWaterLossPower = 1f;

    // Pollution.
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

    // =========================================================
    // FEEDBACK STATES (read by ResourceFeedback)
    // =========================================================

    public enum WaterTrendType
    {
        None,
        Evaporating,
        Freezing,
        RefillingSlow,
        Refilling
    }

    public enum GrowthConditionType
    {
        Ideal,
        TooCold,
        TooHot
    }

    /// <summary>What is happening to the water right now (set in UpdateWater).</summary>
    public WaterTrendType WaterTrend { get; private set; } = WaterTrendType.None;

    /// <summary>How the temperature affects bamboo growth right now.</summary>
    public GrowthConditionType GrowthCondition
    {
        get
        {
            if (temperature < idealTemperature - temperatureThreshold)
                return GrowthConditionType.TooCold;

            if (temperature > idealTemperature + temperatureThreshold)
                return GrowthConditionType.TooHot;

            return GrowthConditionType.Ideal;
        }
    }

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

        // Already in this zone: do NOT reload the saved values.
        if (zone == activeZone)
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
        comfortableWaterMinTemperature = zone.comfortableWaterMinTemperature;
        comfortableWaterMaxTemperature = zone.comfortableWaterMaxTemperature;
        idealWaterTemperature = zone.idealWaterTemperature;
        comfortableEdgeRecoveryMultiplier = zone.comfortableEdgeRecoveryMultiplier;
        coldWaterMinTemperature = zone.coldWaterMinTemperature;
        coldWaterMaxTemperature = zone.coldWaterMaxTemperature;
        coldWaterMinRecoveryMultiplier = zone.coldWaterMinRecoveryMultiplier;
        hotWaterStartTemperature = zone.hotWaterStartTemperature;
        coldWaterLossStartTemperature = zone.coldWaterLossStartTemperature;
        coldWaterLossSpeed = zone.coldWaterLossSpeed;
        coldWaterLossPower = zone.coldWaterLossPower;

        // Stop temperaturens automatiske overgang ved ankomst.
        isTravelling = false;

        // Zonen bestemmer temperaturen: dens starttemperatur første gang,
        // og derefter den temperatur spilleren efterlod den med.
        temperature = Mathf.Clamp(
            zone.temperature,
            minTemperature,
            maxTemperature
        );

        targetTemperature = temperature;

        TelemetryManager.Instance?.LogTemperature(temperature, "zone");

        // Zonen indlæser sine egne gemte værdier.
        water = Mathf.Clamp(zone.water, 0f, 100f);
        pollution = Mathf.Clamp01(zone.pollution);

        TemperatureGrowthFactor = GetTemperatureGrowthFactor();

        Debug.Log(
            "Active ecosystem: " + ActiveZoneName +
            ". Water: " + water.ToString("F0") +
            ". Temperature: " + temperature.ToString("F1") + "°C"
        );
    }

    public void ExitZone(EcosystemZone zone)
    {
        if (activeZone != zone)
            return;

        // Gem zonens nuværende tilstand, så den er der,
        // når pandaen vender tilbage.
        zone.water = water;
        zone.pollution = pollution;

        // Temperaturen gemmes allerede i BeginTravelToZone, før den
        // begynder at ændre sig. Gem kun her, hvis der ikke rejses.
        if (!isTravelling)
            zone.temperature = temperature;

        activeZone = null;
        ActiveZoneName = "Travelling";

        Debug.Log(
            "Panda left " + zone.zoneName +
            ". Saved water: " + water.ToString("F0") +
            ", temperature: " + zone.temperature.ToString("F1") + "°C"
        );
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
        WaterTrend = WaterTrendType.None;

        // Under rejsen: frys vandværdien i stedet for at tømme den.
        if (activeZone == null)
            return;

        // Frost: det rene vand fryser til, så mængden falder.
        // Jo koldere, jo hurtigere (samme kurve som ved varme).
        if (temperature <= coldWaterLossStartTemperature)
        {
            float coldRange = Mathf.Max(
                0.01f,
                coldWaterLossStartTemperature - minTemperature
            );

            float coldSeverity = Mathf.Clamp01(
                (coldWaterLossStartTemperature - temperature) / coldRange
            );

            water -= Mathf.Pow(coldSeverity, Mathf.Max(0.01f, coldWaterLossPower)) *
                     coldWaterLossSpeed *
                     Time.deltaTime;

            if (water > 0f)
                WaterTrend = WaterTrendType.Freezing;
        }
        // Behagelig temperatur: vandet genopfyldes, hurtigst ved
        // den ideelle vandtemperatur og langsommere mod kanterne.
        else if (temperature >= comfortableWaterMinTemperature &&
                 temperature <= comfortableWaterMaxTemperature)
        {
            float ideal = Mathf.Clamp(
                idealWaterTemperature,
                comfortableWaterMinTemperature,
                comfortableWaterMaxTemperature
            );

            // 0 at the edge of the range, 1 at the ideal temperature.
            float closeness = temperature <= ideal
                ? Mathf.InverseLerp(comfortableWaterMinTemperature, ideal, temperature)
                : Mathf.InverseLerp(comfortableWaterMaxTemperature, ideal, temperature);

            float recoveryMultiplier = Mathf.Lerp(
                comfortableEdgeRecoveryMultiplier,
                1f,
                closeness
            );

            water += waterRecoverySpeed *
                     recoveryMultiplier *
                     Time.deltaTime;

            if (water < 100f)
                WaterTrend = WaterTrendType.Refilling;
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

            // Continues smoothly from the comfortable range's edge speed.
            float recoveryMultiplier = Mathf.Lerp(
                comfortableEdgeRecoveryMultiplier,
                coldWaterMinRecoveryMultiplier,
                coldDistance
            );

            water += waterRecoverySpeed *
                     recoveryMultiplier *
                     Time.deltaTime;

            if (water < 100f)
                WaterTrend = WaterTrendType.RefillingSlow;
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

            if (water > 0f)
                WaterTrend = WaterTrendType.Evaporating;
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
                // Gem den temperatur spilleren efterlod zonen med,
                // før overgangen begynder at ændre den.
                if (activeZone != null)
                    activeZone.temperature = temperature;

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