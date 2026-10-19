using UnityEngine;

public class RainZoneController : MonoBehaviour
{
    [Header("Rain Settings")]
    [SerializeField] private bool canRain = false;

    [Tooltip("Minimum time between rain events, in seconds.")]
    [SerializeField] private float minimumRainInterval = 60f;

    [Tooltip("Maximum time between rain events, in seconds.")]
    [SerializeField] private float maximumRainInterval = 120f;

    [Tooltip("How long the rain lasts, in seconds.")]
    [SerializeField] private float rainDuration = 20f;

    [Header("Rain Particle System")]
    [SerializeField] private ParticleSystem rainParticles;

    [Header("Water Settings")]
    [Tooltip("Extra water added per second while it rains.")]
    [SerializeField] private float extraWaterPerSecond = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("Print the remaining rain timer every second.")]
    [SerializeField] private bool showTimerLogs = true;

    private EcosystemZone zone;
    private float rainTimer;
    private float rainDurationTimer;
    private float nextTimerLogTime;

    private bool isRaining;
    private bool wasActiveZone;

    private void Awake()
    {
        zone = GetComponent<EcosystemZone>();

        if (zone == null)
        {
            Debug.LogError(
                $"RainZoneController on '{gameObject.name}' " +
                "needs an EcosystemZone component on the same GameObject.",
                this
            );

            enabled = false;
            return;
        }

        if (minimumRainInterval < 0f)
            minimumRainInterval = 0f;

        if (maximumRainInterval < minimumRainInterval)
        {
            Debug.LogWarning(
                $"RainZoneController on '{gameObject.name}': " +
                "Maximum interval was less than the minimum. " +
                "Using the minimum as the maximum.",
                this
            );

            maximumRainInterval = minimumRainInterval;
        }

        if (rainDuration < 0f)
            rainDuration = 0f;

        if (rainParticles != null)
        {
            rainParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
        else
        {
            Debug.LogWarning(
                $"RainZoneController on '{gameObject.name}' has no " +
                "Rain Particles reference assigned.",
                this
            );
        }

        ResetRainTimer();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[Rain] Controller ready | Zone: '{zone.zoneName}'" +
                $" | Can rain: {canRain}" +
                $" | Initial timer: {rainTimer:F1}s" +
                $" | Particle System assigned: {rainParticles != null}",
                this
            );
        }
    }

    private void Update()
    {
        if (EnvironmentSystem.Instance == null)
        {
            return;
        }

        EcosystemZone activeZone =
            EnvironmentSystem.Instance.ActiveZone;

        // Check whether THIS exact zone is currently active.
        bool isThisZoneActive = activeZone == zone;

        // The panda has just entered this zone.
        if (isThisZoneActive && !wasActiveZone)
        {
            ResetRainTimer();
            nextTimerLogTime = Time.time;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[Rain] Entered zone '{zone.zoneName}'" +
                    $" | Can rain: {canRain}" +
                    $" | Timer reset to: {rainTimer:F1}s",
                    this
                );
            }
        }

        // The panda has left this zone.
        if (!isThisZoneActive && wasActiveZone)
        {
            StopRain();

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[Rain] Left zone '{zone.zoneName}'. Rain stopped.",
                    this
                );
            }
        }

        wasActiveZone = isThisZoneActive;

        // Never rain in an inactive or disallowed zone.
        if (!canRain || !isThisZoneActive)
        {
            return;
        }

        if (rainParticles == null)
        {
            if (showDebugLogs && !isRaining)
            {
                Debug.LogWarning(
                    $"[Rain] Cannot start rain in '{zone.zoneName}': " +
                    "Rain Particles is not assigned in the Inspector.",
                    this
                );
            }

            return;
        }

        if (isRaining)
        {
            rainDurationTimer -= Time.deltaTime;

            // Add water only while this zone is active and raining.
            EnvironmentSystem.Instance.ChangeWater(
                extraWaterPerSecond * Time.deltaTime
            );

            if (rainDurationTimer <= 0f)
            {
                StopRain();
                ResetRainTimer();

                if (showDebugLogs)
                {
                    Debug.Log(
                        $"[Rain] Rain ended in '{zone.zoneName}'." +
                        $" Next rain in approximately {rainTimer:F1}s.",
                        this
                    );
                }
            }
        }
        else
        {
            rainTimer -= Time.deltaTime;

            // Print the countdown approximately once per second.
            if (showDebugLogs &&
                showTimerLogs &&
                Time.time >= nextTimerLogTime)
            {
                Debug.Log(
                    $"[Rain] Zone: '{zone.zoneName}'" +
                    $" | Timer remaining: {Mathf.Max(0f, rainTimer):F1}s" +
                    $" | Active: {isThisZoneActive}" +
                    $" | Can rain: {canRain}",
                    this
                );

                nextTimerLogTime = Time.time + 1f;
            }

            if (rainTimer <= 0f)
            {
                StartRain();
            }
        }
    }

    private void StartRain()
    {
        if (!canRain)
        {
            Debug.LogWarning(
                $"[Rain] Start prevented in '{zone.zoneName}': " +
                "Can Rain is disabled.",
                this
            );
            return;
        }

        if (rainParticles == null)
        {
            Debug.LogError(
                $"[Rain] Start prevented in '{zone.zoneName}': " +
                "Rain Particles is not assigned.",
                this
            );
            return;
        }

        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone != zone)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    $"[Rain] Start prevented in '{zone.zoneName}': " +
                    "This zone is not the active zone.",
                    this
                );
            }

            return;
        }

        isRaining = true;
        rainDurationTimer = rainDuration;

        rainParticles.Play(true);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[Rain] RAIN STARTED in '{zone.zoneName}'!" +
                $" | Duration: {rainDuration:F1}s" +
                $" | Particle System playing: {rainParticles.isPlaying}" +
                $" | Particle count: {rainParticles.particleCount}",
                this
            );
        }
    }

    private void StopRain()
    {
        isRaining = false;

        if (rainParticles != null)
        {
            rainParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void ResetRainTimer()
    {
        rainTimer = Random.Range(
            minimumRainInterval,
            maximumRainInterval
        );
    }

    private void OnDisable()
    {
        StopRain();
        wasActiveZone = false;
    }
}