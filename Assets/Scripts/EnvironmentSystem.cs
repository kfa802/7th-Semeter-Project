using UnityEngine;

public class EnvironmentSystem : MonoBehaviour
{
    public static EnvironmentSystem Instance { get; private set; }

    // Not serialized on purpose: the values here are the real ones,
    // so an old Inspector value can't override them.
    private float minTemperature = -15f;
    private float maxTemperature = 40f;

    [Header("Temperature")]
    [SerializeField] private float temperature = 20f;

    [Header("Ideal Temperature")]
    [SerializeField] private float idealTemperature = 20f;

    [Header("Temperature Threshold")]
    [SerializeField] private float temperatureThreshold = 3f;

    [Header("Freezing")]
    [SerializeField] private float freezingPoint = 0f;

    // Higher = growth drops off faster when it gets cold
    [SerializeField] private float coldSlowdownPower = 1.5f;

    // Slowest growth speed when freezing (0.2 = 5x slower than normal)
    [SerializeField, Range(0.05f, 0.5f)] private float minGrowthWhenCold = 0.2f;

    [Header("Heat")]
    [SerializeField, Range(0f, 1f)] private float minGrowthWhenHot = 0.2f;

    [Header("Water")]
    [SerializeField] private float water = 100f;
    [SerializeField] private float waterReactionSpeed = 5f;

    [Header("Bamboo")]
    [SerializeField] private float bamboo = 100f;
    [SerializeField] private float bambooGrowthSpeed = 4f;
    [SerializeField] private float bambooDecaySpeed = 4f;

    public float Temperature => temperature;
    public float MinTemperature => minTemperature;
    public float MaxTemperature => maxTemperature;
    public float Water => water;
    public float Bamboo => bamboo;

    // 1 = normal growth, small number = very slow
    public float TemperatureGrowthFactor { get; private set; } = 1f;


    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        water = 100f;
        bamboo = 100f;
    }


    private void Update()
    {
        UpdateWater();
        UpdateBamboo();
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
        float comfortableMin = idealTemperature - temperatureThreshold;
        float comfortableMax = idealTemperature + temperatureThreshold;

        // Cold side
        if (temperature < comfortableMin)
        {
            // 0 at freezing (and below), 1 at the comfortable range
            float t = Mathf.Clamp01(
                Mathf.InverseLerp(
                    freezingPoint,
                    comfortableMin,
                    temperature
                )
            );

            // Gets steadily slower, but never fully stops
            return Mathf.Lerp(
                minGrowthWhenCold,
                1f,
                Mathf.Pow(t, coldSlowdownPower)
            );
        }

        // Hot side: slower, but never fully stops
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
        float temperatureDifference =
            Mathf.Abs(temperature - idealTemperature);

        if (temperatureDifference <= temperatureThreshold)
        {
            water = Mathf.MoveTowards(
                water,
                100f,
                waterReactionSpeed * Time.deltaTime
            );

            return;
        }

        float excessTemperature =
            temperatureDifference - temperatureThreshold;

        float maximumExcess =
            Mathf.Max(
                idealTemperature - temperatureThreshold - minTemperature,
                maxTemperature - idealTemperature - temperatureThreshold
            );

        float severity =
            Mathf.Clamp01(excessTemperature / maximumExcess);

        float exponentialSeverity = severity * severity;

        water -= exponentialSeverity * waterReactionSpeed * Time.deltaTime;

        water = Mathf.Clamp(water, 0f, 100f);
    }


    // =========================================================
    // BAMBOO
    // =========================================================

    private void UpdateBamboo()
    {
        TemperatureGrowthFactor = GetTemperatureGrowthFactor();

        if (water >= 50f)
        {
            float waterQuality =
                Mathf.InverseLerp(50f, 100f, water);

            float growth =
                bambooGrowthSpeed *
                waterQuality *
                TemperatureGrowthFactor *
                Time.deltaTime;

            bamboo += growth;
        }
        else
        {
            float waterStress =
                Mathf.InverseLerp(50f, 0f, water);

            float exponentialStress = waterStress * waterStress;

            float decay =
                bambooDecaySpeed *
                exponentialStress *
                Time.deltaTime;

            bamboo -= decay;
        }

        bamboo = Mathf.Clamp(bamboo, 0f, 100f);
    }
}