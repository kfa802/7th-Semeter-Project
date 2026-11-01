using UnityEngine;
using UnityEngine.Audio;

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

    [Header("Rain Sound")]
    [Tooltip("A looping rain sound. Plays while it rains.")]
    [SerializeField] private AudioClip rainLoop;

    [SerializeField, Range(0f, 1f)] private float rainVolume = 0.7f;

    [Tooltip("Seconds for the rain sound to fade in when the rain starts.")]
    [SerializeField, Min(0.01f)] private float rainFadeIn = 3f;

    [Tooltip("Seconds for the rain sound to fade out when the rain stops.")]
    [SerializeField, Min(0.01f)] private float rainFadeOut = 4f;

    [Tooltip("Optional: thunder sounds played now and then while it rains.")]
    [SerializeField] private AudioClip[] thunderSounds;

    [SerializeField, Range(0f, 1f)] private float thunderVolume = 0.6f;

    [Tooltip("Random seconds between thunder (min and max).")]
    [SerializeField] private Vector2 thunderInterval = new Vector2(8f, 20f);

    [Tooltip("Optional: send the rain sounds through a group in your Audio Mixer.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

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

    private AudioSource rainSource;
    private AudioSource thunderSource;
    private float thunderTimer;

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
        CreateSoundSources();

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
        UpdateRainSound();

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

        if (rainSource != null && !rainSource.isPlaying)
        {
            // Start at a random point so it doesn't always sound the same.
            rainSource.time = Random.Range(0f, rainLoop.length * 0.9f);
            rainSource.Play();
        }

        thunderTimer = Random.Range(thunderInterval.x, thunderInterval.y);

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

    // =========================================================
    // SOUND
    // =========================================================

    private void CreateSoundSources()
    {
        if (rainLoop != null)
        {
            rainSource = gameObject.AddComponent<AudioSource>();
            rainSource.clip = rainLoop;
            rainSource.loop = true;
            rainSource.playOnAwake = false;
            rainSource.spatialBlend = 0f;   // all around you
            rainSource.volume = 0f;
            rainSource.outputAudioMixerGroup = mixerGroup;
        }

        if (thunderSounds != null && thunderSounds.Length > 0)
        {
            thunderSource = gameObject.AddComponent<AudioSource>();
            thunderSource.playOnAwake = false;
            thunderSource.spatialBlend = 0f;
            thunderSource.outputAudioMixerGroup = mixerGroup;
        }
    }

    private void UpdateRainSound()
    {
        if (rainSource != null)
        {
            // Fade in while raining, fade out when it stops (also when leaving the zone).
            float target = isRaining ? rainVolume : 0f;
            float fadeTime = isRaining ? rainFadeIn : rainFadeOut;

            rainSource.volume = Mathf.MoveTowards(
                rainSource.volume,
                target,
                Mathf.Max(rainVolume, 0.01f) / fadeTime * Time.deltaTime
            );

            if (!isRaining && rainSource.volume <= 0.001f && rainSource.isPlaying)
                rainSource.Stop();
        }

        // Thunder now and then while it rains.
        if (isRaining && thunderSource != null)
        {
            thunderTimer -= Time.deltaTime;

            if (thunderTimer <= 0f)
            {
                AudioClip clip = thunderSounds[Random.Range(0, thunderSounds.Length)];

                if (clip != null)
                {
                    thunderSource.pitch = Random.Range(0.9f, 1.1f);
                    thunderSource.PlayOneShot(clip, thunderVolume);
                }

                thunderTimer = Random.Range(thunderInterval.x, Mathf.Max(thunderInterval.x, thunderInterval.y));
            }
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

        if (rainSource != null)
        {
            rainSource.Stop();
            rainSource.volume = 0f;
        }

        wasActiveZone = false;
    }
}