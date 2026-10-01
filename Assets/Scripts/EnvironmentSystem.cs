using UnityEngine;

public class EnvironmentSystem : MonoBehaviour
{
    public static EnvironmentSystem Instance { get; private set; }


    // =========================================================
    // TEMPERATURE
    // =========================================================

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


    // Higher = growth drops off faster when cold
    [SerializeField] private float coldSlowdownPower = 1.5f;


    // Slowest growth speed when freezing
    [SerializeField, Range(0.05f, 0.5f)]
    private float minGrowthWhenCold = 0.2f;


    [Header("Heat")]
    [SerializeField, Range(0f, 1f)]
    private float minGrowthWhenHot = 0.2f;


    // =========================================================
    // WATER
    // =========================================================

    [Header("Water")]
    [SerializeField] private float water = 100f;

    [SerializeField] private float waterReactionSpeed = 5f;


    // =========================================================
    // POLLUTION
    // =========================================================

    [Header("Pollution")]
    [Tooltip("0 = no pollution, 1 = maximum pollution.")]
    [SerializeField, Range(0f, 1f)]
    private float pollution = 0f;


    // =========================================================
    // PUBLIC VALUES
    // =========================================================

    public float Temperature =>
        temperature;


    public float MinTemperature =>
        minTemperature;


    public float MaxTemperature =>
        maxTemperature;


    public float Water =>
        water;


    // Bamboo is now based on the actual bamboo
    // existing in the scene.
    public float Bamboo
    {
        get
        {
            if (BambooManager.Instance == null)
                return 0f;

            return BambooManager.Instance.Percent;
        }
    }


    public float Pollution =>
        pollution;


    // 1 = normal growth
    // smaller number = slower growth
    public float TemperatureGrowthFactor
    {
        get;
        private set;
    } = 1f;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        water = 100f;
    }


    private void Update()
    {
        UpdateWater();

        TemperatureGrowthFactor =
            GetTemperatureGrowthFactor();
    }


    // =========================================================
    // TEMPERATURE
    // =========================================================

    public void SetTemperature(
        float newTemperature
    )
    {
        temperature =
            Mathf.Clamp(
                newTemperature,
                minTemperature,
                maxTemperature
            );
    }


    public void ChangeTemperature(
        float amount
    )
    {
        SetTemperature(
            temperature + amount
        );
    }


    private float GetTemperatureGrowthFactor()
    {
        float comfortableMin =
            idealTemperature -
            temperatureThreshold;


        float comfortableMax =
            idealTemperature +
            temperatureThreshold;


        // -------------------------------------------------
        // COLD SIDE
        // -------------------------------------------------

        if (temperature < comfortableMin)
        {
            float t =
                Mathf.Clamp01(
                    Mathf.InverseLerp(
                        freezingPoint,
                        comfortableMin,
                        temperature
                    )
                );


            return Mathf.Lerp(
                minGrowthWhenCold,
                1f,
                Mathf.Pow(
                    t,
                    coldSlowdownPower
                )
            );
        }


        // -------------------------------------------------
        // HOT SIDE
        // -------------------------------------------------

        if (temperature > comfortableMax)
        {
            float t =
                Mathf.InverseLerp(
                    comfortableMax,
                    maxTemperature,
                    temperature
                );


            return Mathf.Lerp(
                1f,
                minGrowthWhenHot,
                t
            );
        }


        return 1f;
    }


    // =========================================================
    // WATER
    // =========================================================

    private void UpdateWater()
    {
        float temperatureDifference =
            Mathf.Abs(
                temperature -
                idealTemperature
            );


        if (temperatureDifference <=
            temperatureThreshold)
        {
            water =
                Mathf.MoveTowards(
                    water,
                    100f,
                    waterReactionSpeed *
                    Time.deltaTime
                );

            return;
        }


        float excessTemperature =
            temperatureDifference -
            temperatureThreshold;


        float maximumExcess =
            Mathf.Max(
                idealTemperature -
                temperatureThreshold -
                minTemperature,

                maxTemperature -
                idealTemperature -
                temperatureThreshold
            );


        float severity =
            Mathf.Clamp01(
                excessTemperature /
                maximumExcess
            );


        float exponentialSeverity =
            severity * severity;


        water -=
            exponentialSeverity *
            waterReactionSpeed *
            Time.deltaTime;


        water =
            Mathf.Clamp(
                water,
                0f,
                100f
            );
    }


    // =========================================================
    // POLLUTION
    // =========================================================

    // This is mainly here so other systems can change
    // pollution later if needed.
    public void SetPollution(
        float value
    )
    {
        pollution =
            Mathf.Clamp01(value);
    }
}