using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class PandaSystem : MonoBehaviour
{
    // =========================================================
    // EVENTS (used by ResourceFeedback)
    // =========================================================

    /// <summary>Normal feeding. Passes the health gained.</summary>
    public event Action<float> Fed;

    // OVERFEEDING DISABLED
    // /// <summary>Overfeeding. Passes the health lost.</summary>
    // public event Action<float> Overfed;

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
    // OVERFEEDING (DISABLED)
    // =========================================================

    // [Header("Overfeeding")]
    // [Tooltip("Number of recent feedings that makes the panda overfed.")]
    // [SerializeField]
    // private int maximumFeedingsInWindow = 3;
    //
    // [Tooltip("How long feeding history is remembered.")]
    // [SerializeField]
    // private float overfeedingWindow = 30f;
    //
    // [Tooltip("How many seconds the panda remains overfed after a feeding that causes overfeeding.")]
    // [SerializeField]
    // private float overfedDuration = 5f;
    //
    // [Tooltip("Health lost when trying to feed an already overfed panda.")]
    // [SerializeField]
    // private float overfeedingDamage = 10f;
    //
    // private List<float> feedingTimes = new List<float>();
    //
    // private bool isOverfed = false;
    // private float overfedTimer = 0f;


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
    // DRINKING SOUND
    // =========================================================

    [Header("Drinking Sound")]
    [Tooltip("Played when the panda goes from thirsty to not thirsty. One is picked at random.")]
    [SerializeField] private AudioClip[] drinkingSounds;

    [SerializeField, Range(0f, 1f)]
    private float drinkingVolume = 0.8f;

    [Tooltip("Random pitch change so it doesn't sound identical every time. 0.08 = ±8%.")]
    [SerializeField, Range(0f, 0.5f)]
    private float drinkingPitchVariation = 0.08f;

    [Tooltip("0 = same volume everywhere (2D). 1 = fully 3D (quieter further from the camera).")]
    [SerializeField, Range(0f, 1f)]
    private float drinkingSpatialBlend = 0.3f;

    [Tooltip("Minimum seconds between drinking sounds, so it doesn't repeat " +
             "if the water hovers around the thirst threshold.")]
    [SerializeField, Min(0f)]
    private float drinkingSoundCooldown = 15f;

    [Tooltip("Off: no sound when thirst ends only because the panda arrived " +
             "in a zone with more water. On: also play it then.")]
    [SerializeField] private bool playWhenArrivingInZone = false;

    [Tooltip("Optional: send the sound through a group in your Audio Mixer.")]
    [SerializeField] private AudioMixerGroup drinkingMixerGroup;

    private bool wasThirsty;
    private EcosystemZone lastZone;
    private float lastDrinkSoundTime = -999f;
    private AudioSource drinkingAudio;


    // =========================================================
    // HEARTBEAT SOUND
    // =========================================================

    [Header("Heartbeat Sound")]
    [Tooltip("A looping heartbeat. Gets louder (and faster) the lower the health.")]
    [SerializeField] private AudioClip heartbeatLoop;

    [Tooltip("The heartbeat starts (very quietly) when health drops below this.")]
    [SerializeField, Range(0f, 100f)]
    private float heartbeatStartBelowHealth = 40f;

    [Tooltip("At or below this health the heartbeat is at its loudest and fastest.")]
    [SerializeField, Range(0f, 100f)]
    private float heartbeatFullAtHealth = 5f;

    [SerializeField, Range(0f, 1f)]
    private float heartbeatMaxVolume = 0.9f;

    [Tooltip("1 = volume rises evenly. Higher = stays quiet longer, then rises steeply near the end.")]
    [SerializeField, Range(0.5f, 3f)]
    private float heartbeatVolumeCurve = 1.5f;

    [Tooltip("Make the heartbeat faster as health drops (this also raises its pitch slightly).")]
    [SerializeField] private bool heartbeatSpeedUp = true;

    [Tooltip("Playback speed at 'Full At Health' (1.4 = 40% faster).")]
    [SerializeField, Range(1f, 2f)]
    private float heartbeatMaxSpeed = 1.4f;

    [Tooltip("How fast the heartbeat follows the health. Lower = smoother.")]
    [SerializeField, Min(0.01f)]
    private float heartbeatSmoothSpeed = 1.5f;

    [Tooltip("Optional: send the sound through a group in your Audio Mixer.")]
    [SerializeField] private AudioMixerGroup heartbeatMixerGroup;

    private AudioSource heartbeatAudio;
    private float heartbeatIntensity;


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

    // OVERFEEDING DISABLED: always false. Kept so other scripts that
    // still check IsOverfed (e.g. status/mood UI) keep compiling.
    public bool IsOverfed => false;

    // public bool IsOverfed
    // {
    //     get
    //     {
    //         return isOverfed;
    //     }
    // }

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

    /// <summary>True while extreme heat is damaging the panda.</summary>
    public bool IsInExtremeHeat
    {
        get
        {
            if (environment == null)
                return false;

            return environment.Temperature > extremeHeatTemperature;
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

    private void Start()
    {
        // Remember the starting state, so no sound plays when the game starts.
        wasThirsty = IsThirsty;
        lastZone = environment != null ? environment.ActiveZone : null;
    }

    private void Update()
    {
        UpdateHunger();
        UpdateDrinkingSound();
        UpdateHeartbeat();

        // OVERFEEDING DISABLED
        // UpdateFeedingHistory();
        // UpdateOverfedTimer();

        if (environment == null)
            return;

        UpdateHealthFromWater();
        UpdateHealthFromHunger();
        UpdateHealthFromExtremeHeat();

        ClampHealth();
    }


    // =========================================================
    // DRINKING SOUND
    // =========================================================

    private void UpdateDrinkingSound()
    {
        if (environment == null)
            return;

        EcosystemZone zone = environment.ActiveZone;
        bool thirsty = IsThirsty;

        bool zoneChanged = zone != lastZone;
        bool stoppedBeingThirsty = wasThirsty && !thirsty;

        // Only while the panda is in a zone (not travelling), and
        // optionally not when the change came from switching zones.
        bool allowed =
            zone != null &&
            (playWhenArrivingInZone || !zoneChanged) &&
            Time.time - lastDrinkSoundTime >= drinkingSoundCooldown;

        if (stoppedBeingThirsty && allowed)
        {
            PlayDrinkingSound();
            lastDrinkSoundTime = Time.time;
        }

        wasThirsty = thirsty;
        lastZone = zone;
    }

    private void PlayDrinkingSound()
    {
        if (drinkingSounds == null || drinkingSounds.Length == 0)
            return;

        AudioClip clip = drinkingSounds[UnityEngine.Random.Range(0, drinkingSounds.Length)];

        if (clip == null)
            return;

        if (drinkingAudio == null)
        {
            // A sound object on the panda, so the sound comes from the panda.
            GameObject soundObject = new GameObject("DrinkingSound");
            soundObject.transform.SetParent(transform, false);

            drinkingAudio = soundObject.AddComponent<AudioSource>();
            drinkingAudio.playOnAwake = false;
            drinkingAudio.rolloffMode = AudioRolloffMode.Linear;
            drinkingAudio.minDistance = 1f;
            drinkingAudio.maxDistance = 60f;
            drinkingAudio.dopplerLevel = 0f;
        }

        drinkingAudio.clip = clip;
        drinkingAudio.volume = drinkingVolume;
        drinkingAudio.pitch = 1f + UnityEngine.Random.Range(-drinkingPitchVariation, drinkingPitchVariation);
        drinkingAudio.spatialBlend = drinkingSpatialBlend;
        drinkingAudio.outputAudioMixerGroup = drinkingMixerGroup;
        drinkingAudio.Play();
    }


    // =========================================================
    // HEARTBEAT SOUND
    // =========================================================

    private void UpdateHeartbeat()
    {
        if (heartbeatLoop == null)
            return;

        // 0 = health fine, 1 = health at (or below) 'Full At Health'.
        float target = Mathf.InverseLerp(
            heartbeatStartBelowHealth,
            Mathf.Min(heartbeatFullAtHealth, heartbeatStartBelowHealth - 0.01f),
            health
        );

        heartbeatIntensity = Mathf.MoveTowards(
            heartbeatIntensity,
            target,
            heartbeatSmoothSpeed * Time.deltaTime
        );

        float volume = Mathf.Pow(heartbeatIntensity, heartbeatVolumeCurve) * heartbeatMaxVolume;

        if (heartbeatAudio == null)
        {
            if (volume <= 0.001f)
                return;

            GameObject soundObject = new GameObject("HeartbeatSound");
            soundObject.transform.SetParent(transform, false);

            heartbeatAudio = soundObject.AddComponent<AudioSource>();
            heartbeatAudio.loop = true;
            heartbeatAudio.playOnAwake = false;
            heartbeatAudio.spatialBlend = 0f;   // heard the same everywhere
        }

        heartbeatAudio.clip = heartbeatLoop;
        heartbeatAudio.outputAudioMixerGroup = heartbeatMixerGroup;
        heartbeatAudio.volume = volume;
        heartbeatAudio.pitch = heartbeatSpeedUp
            ? Mathf.Lerp(1f, heartbeatMaxSpeed, heartbeatIntensity)
            : 1f;

        // Only play while it can be heard.
        if (volume > 0.001f && !heartbeatAudio.isPlaying)
            heartbeatAudio.Play();
        else if (volume <= 0.001f && heartbeatAudio.isPlaying)
            heartbeatAudio.Stop();
    }


    // =========================================================
    // HUNGER TIMER
    // =========================================================

    private void UpdateHunger()
    {
        hungerTimer += Time.deltaTime;
    }


    // =========================================================
    // FEEDING HISTORY (DISABLED - only used for overfeeding)
    // =========================================================

    // private void UpdateFeedingHistory()
    // {
    //     float currentTime = Time.time;
    //
    //     for (int i = feedingTimes.Count - 1; i >= 0; i--)
    //     {
    //         if (currentTime - feedingTimes[i] > overfeedingWindow)
    //         {
    //             feedingTimes.RemoveAt(i);
    //         }
    //     }
    // }


    // =========================================================
    // OVERFED TIMER (DISABLED)
    // =========================================================

    // private void UpdateOverfedTimer()
    // {
    //     if (!isOverfed)
    //         return;
    //
    //     overfedTimer -= Time.deltaTime;
    //
    //     if (overfedTimer <= 0f)
    //     {
    //         isOverfed = false;
    //         overfedTimer = 0f;
    //
    //         // Clear old feeding history so the panda can
    //         // return to normal feeding after recovering.
    //         feedingTimes.Clear();
    //
    //         Debug.Log("Panda is no longer overfed.");
    //     }
    // }


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
        // OVERFEEDING DISABLED
        // // Update feeding history before checking overfeeding.
        // UpdateFeedingHistory();
        //
        // // If the panda is already overfed, apply damage
        // // and restart the overfeeding timer.
        // if (isOverfed)
        // {
        //     health -= overfeedingDamage;
        //
        //     overfedTimer = overfedDuration;
        //
        //     ClampHealth();
        //
        //     Overfed?.Invoke(overfeedingDamage);
        //
        //     Debug.Log(
        //         "Panda is overfed! Health lost: "
        //         + overfeedingDamage
        //         + ". Overfeeding timer restarted."
        //     );
        //
        //     return;
        // }
        //
        // // Check whether this feeding exceeds the normal limit.
        // if (feedingTimes.Count >= maximumFeedingsInWindow)
        // {
        //     isOverfed = true;
        //     overfedTimer = overfedDuration;
        //
        //     health -= overfeedingDamage;
        //
        //     ClampHealth();
        //
        //     Overfed?.Invoke(overfeedingDamage);
        //
        //     Debug.Log(
        //         "Panda became overfed! Health lost: "
        //         + overfeedingDamage
        //     );
        //
        //     return;
        // }

        // Normal feeding.
        hungerTimer = 0f;

        // OVERFEEDING DISABLED
        // // Record the feeding.
        // feedingTimes.Add(Time.time);

        // Restore health.
        health += healthPerFeeding;

        ClampHealth();

        Fed?.Invoke(healthPerFeeding);

        Debug.Log("Panda fed successfully.");
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