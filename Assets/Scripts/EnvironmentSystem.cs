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
    [SerializeField] private float water = 100f;

    [Header("Water Loss")]
    [SerializeField] private float waterReactionSpeed = 5f;

    [Header("Water Recovery")]
    [SerializeField] private float waterRecoverySpeed = 1f;


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


    // Bamboo is based on the actual bamboo
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

    public void SetTemperature(float newTemperature)
    {
        temperature =
            Mathf.Clamp(
                newTemperature,
                minTemperature,
                maxTemperature
            );
    }

    public void ChangeTemperature(float amount)
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
    // =========================================================
    // MODERATE / COMFORTABLE TEMPERATURE
    // 15–24°C
    // Water increases quickly
    // =========================================================

    if (temperature >= 15f && temperature <= 24f)
    {
        water +=
            waterRecoverySpeed *
            Time.deltaTime;

        water = Mathf.Clamp(
            water,
            0f,
            100f
        );

        return;
    }


    // =========================================================
    // SLIGHTLY COLD
    // 0–14°C
    // Water still increases, but slower the colder it gets
    // =========================================================

    if (temperature > 0f && temperature < 15f)
    {
        float coldDistance =
            Mathf.InverseLerp(
                15f,
                0f,
                temperature
            );

        // Starts at 100% recovery at 15°C
        // Drops towards 20% recovery at 0°C
        float recoveryMultiplier =
            Mathf.Lerp(
                1f,
                0.2f,
                coldDistance
            );

        water +=
            waterRecoverySpeed *
            recoveryMultiplier *
            Time.deltaTime;

        water = Mathf.Clamp(
            water,
            0f,
            100f
        );

        return;
    }


    // =========================================================
    // SLIGHTLY HOT
    // 25–40°C
    // Water decreases, increasingly faster
    // =========================================================

    if (temperature >= 25f)
    {
        float hotSeverity =
            Mathf.InverseLerp(
                25f,
                maxTemperature,
                temperature
            );

        // Make the loss accelerate exponentially
        float exponentialSeverity =
            Mathf.Pow(
                hotSeverity,
                2f
            );

        water -=
            exponentialSeverity *
            waterReactionSpeed *
            Time.deltaTime;

        water = Mathf.Clamp(
            water,
            0f,
            100f
        );

        return;
    }


    // =========================================================
    // VERY COLD / FREEZING
    // -15 to 0°C
    // Water decreases increasingly faster
    // =========================================================

    if (temperature <= 0f)
    {
        float coldSeverity =
            Mathf.InverseLerp(
                0f,
                minTemperature,
                temperature
            );

        // Make freezing accelerate exponentially
        float exponentialSeverity =
            Mathf.Pow(
                coldSeverity,
                2f
            );

        water -=
            exponentialSeverity *
            waterReactionSpeed *
            Time.deltaTime;

        water = Mathf.Clamp(
            water,
            0f,
            100f
        );
    }
}


    // =========================================================
    // POLLUTION
    // =========================================================

    public void SetPollution(float value)
    {
        pollution =
            Mathf.Clamp01(value);
    }

    public void ChangePollution(float amount)
    {
        SetPollution(
            pollution + amount
        );
    }
}