using UnityEngine;

public class EnvironmentSystem : MonoBehaviour
{
    [Header("Temperature")]
    [SerializeField] private float temperature = 20f;
    [SerializeField] private float minTemperature = 5f;
    [SerializeField] private float maxTemperature = 40f;

    [Header("Ideal Temperature")]
    [SerializeField] private float idealTemperature = 20f;

    [Header("Temperature Threshold")]
    [SerializeField] private float temperatureThreshold = 3f;

    [Header("Water")]
    [SerializeField] private float water = 100f;

    [SerializeField] private float waterReactionSpeed = 5f;

    [Header("Bamboo")]
    [SerializeField] private float bamboo = 100f;

    [SerializeField] private float bambooGrowthSpeed = 4f;
    [SerializeField] private float bambooDecaySpeed = 4f;

    public float Temperature => temperature;
    public float Water => water;
    public float Bamboo => bamboo;


    private void Start()
    {
        // Start everything at full.
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


    // =========================================================
    // WATER
    // =========================================================

    private void UpdateWater()
    {
        float temperatureDifference =
            Mathf.Abs(temperature - idealTemperature);


        // -----------------------------------------------------
        // INSIDE THE SAFE TEMPERATURE RANGE
        // -----------------------------------------------------

        if (temperatureDifference <= temperatureThreshold)
        {
            // Temperature is still safe.
            // Slowly allow water to recover toward 100%.

            water = Mathf.MoveTowards(
                water,
                100f,
                waterReactionSpeed * Time.deltaTime
            );

            return;
        }


        // -----------------------------------------------------
        // TEMPERATURE IS TOO HIGH OR TOO LOW
        // -----------------------------------------------------

        float excessTemperature =
            temperatureDifference - temperatureThreshold;


        float maximumExcess =
            Mathf.Max(
                idealTemperature - temperatureThreshold - minTemperature,
                maxTemperature - idealTemperature - temperatureThreshold
            );


        float severity =
            Mathf.Clamp01(
                excessTemperature / maximumExcess
            );


        /*
         * Exponential-style curve.
         *
         * Small temperature changes have little effect.
         * Larger temperature changes become increasingly severe.
         */

        float exponentialSeverity =
            severity * severity;


        float waterLoss =
            exponentialSeverity *
            waterReactionSpeed *
            Time.deltaTime;


        water -= waterLoss;

        water = Mathf.Clamp(
            water,
            0f,
            100f
        );
    }


    // =========================================================
    // BAMBOO
    // =========================================================

    private void UpdateBamboo()
    {
        /*
         * Bamboo does NOT directly care about temperature.
         *
         * It cares about water.
         *
         * This means:
         *
         * Temperature
         *      ↓
         *    Water
         *      ↓
         *   Bamboo
         */

        if (water >= 50f)
        {
            // Good amount of water.
            // Bamboo grows.

            float waterQuality =
                Mathf.InverseLerp(
                    50f,
                    100f,
                    water
                );


            float growth =
                bambooGrowthSpeed *
                waterQuality *
                Time.deltaTime;


            bamboo += growth;
        }
        else
        {
            // Too little water.
            // Bamboo starts dying.

            float waterStress =
                Mathf.InverseLerp(
                    50f,
                    0f,
                    water
                );


            /*
             * Square the value so that mild water shortage
             * isn't immediately devastating.
             */

            float exponentialStress =
                waterStress * waterStress;


            float decay =
                bambooDecaySpeed *
                exponentialStress *
                Time.deltaTime;


            bamboo -= decay;
        }


        bamboo = Mathf.Clamp(
            bamboo,
            0f,
            100f
        );
    }
}