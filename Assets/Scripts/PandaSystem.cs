
using System.Collections.Generic;
using UnityEngine;

public class PandaSystem : MonoBehaviour
{
    // =========================================================
    // HEALTH
    // =========================================================

    [Header("Panda Health")]
    [SerializeField, Range(0f, 100f)]
    private float health = 100f;


    // =========================================================
    // FEEDING / HUNGER
    // =========================================================

    [Header("Feeding")]
    [Tooltip("Health gained from a normal feeding.")]
    [SerializeField]
    private float healthPerFeeding = 10f;

    [Tooltip("Seconds without food before the panda becomes hungry.")]
    [SerializeField]
    private float timeUntilHungry = 30f;

    [Tooltip("Seconds without food before the panda becomes very hungry.")]
    [SerializeField]
    private float timeUntilVeryHungry = 60f;

    [Tooltip("Health lost per second while hungry.")]
    [SerializeField]
    private float hungryHealthLossPerSecond = 3f;

    [Tooltip("Health lost per second while very hungry.")]
    [SerializeField]
    private float veryHungryHealthLossPerSecond = 6f;

    private float hungerTimer;


    // =========================================================
    // OVERFEEDING
    // =========================================================

    [Header("Overfeeding")]
    [Tooltip("Number of recent feedings that makes the panda overfed.")]
    [SerializeField]
    private int maximumFeedingsInWindow = 3;

    [Tooltip("How long feeding history is remembered.")]
    [SerializeField]
    private float overfeedingWindow = 30f;

    [Tooltip("Health lost when trying to feed an already overfed panda.")]
    [SerializeField]
    private float overfeedingDamage = 10f;

    private List<float> feedingTimes = new List<float>();


    // =========================================================
    // WATER
    // =========================================================

    [Header("Water Threshold")]
    [SerializeField, Range(0f, 100f)]
    private float waterHealthThreshold = 30f;

    [Header("Health Damage From Water")]
    [Tooltip("Maximum health lost per second when water is at 0%.")]
    [SerializeField]
    private float maximumHealthLossPerSecond = 10f;

    [Tooltip("Higher values make damage increase more strongly as water gets lower.")]
    [SerializeField]
    private float waterDamagePower = 3f;


    // =========================================================
    // TEMPERATURE
    // =========================================================

    [Header("Temperature")]
    [Tooltip("Temperature at which the panda becomes hot.")]
    [SerializeField]
    private float hotTemperature = 25f;

    [Tooltip("Temperature at or below which the panda becomes cold.")]
    [SerializeField]
    private float coldTemperature = 0f;


    // =========================================================
    // STRESS
    // =========================================================

    [Header("Stress")]
    [SerializeField, Range(0f, 100f)]
    private float stress = 0f;

    [SerializeField, Range(0f, 100f)]
    private float stressedThreshold = 50f;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private EnvironmentSystem environment;

    [Header("Extreme Heat Damage")]
    [SerializeField]
    private float extremeHeatTemperature = 35f;

    [SerializeField]
    private float maximumExtremeHeatDamagePerSecond = 2f;


    // =========================================================
    // PUBLIC VALUES
    // =========================================================

    public float Health => health;

    public float Stress => stress;

    public float HungerTimer => hungerTimer;


    // =========================================================
    // CONDITIONS
    // =========================================================

    public bool IsHungry
    {
        get
        {
            return hungerTimer >= timeUntilHungry;
        }
    }

    public bool IsVeryHungry
    {
        get
        {
            return hungerTimer >= timeUntilVeryHungry;
        }
    }

    public bool IsOverfed
    {
        get
        {
            return feedingTimes.Count >= maximumFeedingsInWindow;
        }
    }

    public bool IsThirsty
    {
        get
        {
            if (environment == null)
                return false;

            return environment.Water < waterHealthThreshold;
        }
    }

    public bool IsHot
    {
        get
        {
            if (environment == null)
                return false;

            return environment.Temperature >= hotTemperature;
        }
    }

    public bool IsCold
    {
        get
        {
            if (environment == null)
                return false;

            return environment.Temperature <= coldTemperature;
        }
    }

    public bool IsStressed
    {
        get
        {
            return stress >= stressedThreshold;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (environment == null)
            return;

        UpdateHunger();
        UpdateFeedingHistory();

        UpdateHealthFromWater();
        UpdateHealthFromHunger();
        UpdateHealthFromExtremeHeat();

        ClampHealth();
    }


    // =========================================================
    // HUNGER TIMER
    // =========================================================

    private void UpdateHunger()
    {
        hungerTimer += Time.deltaTime;
    }


    // =========================================================
    // FEEDING HISTORY
    // =========================================================

    private void UpdateFeedingHistory()
    {
        float currentTime = Time.time;

        for (int i = feedingTimes.Count - 1; i >= 0; i--)
        {
            if (currentTime - feedingTimes[i] > overfeedingWindow)
            {
                feedingTimes.RemoveAt(i);
            }
        }
    }


    // =========================================================
    // WATER HEALTH
    // =========================================================

    private void UpdateHealthFromWater()
    {
        float water = environment.Water;

        // WATER BELOW THRESHOLD: DAMAGE HEALTH
        if (water < waterHealthThreshold)
        {
            float damageAmount =
                1f - (water / waterHealthThreshold);

            damageAmount = Mathf.Clamp01(damageAmount);

            damageAmount = Mathf.Pow(
                damageAmount,
                waterDamagePower
            );

            float damagePerSecond =
                damageAmount * maximumHealthLossPerSecond;

            health -= damagePerSecond * Time.deltaTime;
        }

        // Water above the threshold does not restore health.
    }


    // =========================================================
    // HUNGER HEALTH
    // =========================================================

    private void UpdateHealthFromHunger()
    {
        if (IsVeryHungry)
        {
            health -= veryHungryHealthLossPerSecond * Time.deltaTime;
        }
        else if (IsHungry)
        {
            health -= hungryHealthLossPerSecond * Time.deltaTime;
        }
    }


    // =========================================================
    // FEED PANDA
    // =========================================================

    public void FeedPanda()
    {
        // Remove feedings outside the time window.
        UpdateFeedingHistory();

        // OVERFED: Reject feeding and apply damage.
        if (IsOverfed)
        {
            // Record the attempt so repeated attempts
            // keep extending the overfeeding window.
            feedingTimes.Add(Time.time);

            health -= overfeedingDamage;
            ClampHealth();

            Debug.Log("PANDA OVERFED! Stop feeding and wait.");

            return;
        }

        // NORMAL FEEDING
        hungerTimer = 0f;

        feedingTimes.Add(Time.time);

        health += healthPerFeeding;
        ClampHealth();

        if (IsOverfed)
        {
            Debug.Log("PANDA IS NOW OVERFED! Stop feeding.");
        }
    }


    // =========================================================
    // STRESS
    // =========================================================

    public void SetStress(float value)
    {
        stress = Mathf.Clamp(
            value,
            0f,
            100f
        );
    }

    public void ChangeStress(float amount)
    {
        SetStress(stress + amount);
    }


    // =========================================================
    // EXTREME HEAT DAMAGE
    // =========================================================

    private void UpdateHealthFromExtremeHeat()
    {
        float temperature = environment.Temperature;

        if (temperature <= extremeHeatTemperature)
            return;

        float heatSeverity = Mathf.InverseLerp(
            extremeHeatTemperature,
            environment.MaxTemperature,
            temperature
        );

        // Damage increases gradually as temperature rises.
        float damageMultiplier = Mathf.Pow(
            heatSeverity,
            2f
        );

        float damagePerSecond =
            damageMultiplier * maximumExtremeHeatDamagePerSecond;

        health -= damagePerSecond * Time.deltaTime;
    }


    // =========================================================
    // HEALTH CLAMP
    // =========================================================

    private void ClampHealth()
    {
        health = Mathf.Clamp(
            health,
            0f,
            100f
        );
    }
}