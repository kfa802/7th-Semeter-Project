
using UnityEngine;

public class PollutionParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private EcosystemZone thisZone;
    [SerializeField] private ParticleSystem pollutionParticles;

    [Header("Pollution Settings")]
    [SerializeField, Range(0f, 1f)]
    private float pollutionThreshold = 0.01f;

    [SerializeField, Min(0f)]
    private float maximumEmission = 80f;

    [Header("Fade Settings")]
    [SerializeField, Min(0.1f)]
    private float fadeInDuration = 2f;

    [SerializeField, Min(0.1f)]
    private float fadeOutDuration = 2f;

    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.MainModule main;

    private float currentAlpha = 0f;
    private float targetAlpha = 0f;

    private void Awake()
    {
        if (pollutionParticles == null)
            pollutionParticles = GetComponent<ParticleSystem>();

        if (pollutionParticles == null)
            return;

        emission = pollutionParticles.emission;
        main = pollutionParticles.main;

        emission.rateOverTime = 0f;

        // Start with transparent particles.
        Color startColor = main.startColor.color;
        startColor.a = 0f;
        main.startColor = startColor;

        pollutionParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private void Update()
    {
        if (environment == null ||
            thisZone == null ||
            pollutionParticles == null)
            return;

        bool isActiveZone = environment.ActiveZone == thisZone;

        float pollution = isActiveZone
            ? Mathf.Clamp01(environment.Pollution)
            : 0f;

        bool shouldEmit = isActiveZone &&
                          pollution > pollutionThreshold;

        targetAlpha = shouldEmit ? 1f : 0f;

        // Fade in and out smoothly.
        float duration = targetAlpha > currentAlpha
            ? fadeInDuration
            : fadeOutDuration;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            Time.deltaTime / duration
        );

        // Control the number of new particles.
        float intensity = Mathf.InverseLerp(
            pollutionThreshold,
            1f,
            pollution
        );

        emission.rateOverTime = shouldEmit
            ? maximumEmission * intensity
            : 0f;

        // Fade the particles' colour.
        Color particleColor = main.startColor.color;
        particleColor.a = currentAlpha;
        main.startColor = particleColor;

        if (shouldEmit && !pollutionParticles.isPlaying)
            pollutionParticles.Play(true);

        // Let existing particles finish fading before clearing them.
        if (!shouldEmit &&
            currentAlpha <= 0f &&
            pollutionParticles.isPlaying)
        {
            pollutionParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }
}