using System.Collections;
using UnityEngine;

public class DilemmaManager : MonoBehaviour
{
    [Header("Environment")]
    [SerializeField] private EnvironmentSystem environment;

    [Header("Pollution Particles")]
    [SerializeField] private ParticleSystem pollutionParticles;
    [SerializeField] private float maxEmission = 100f;

    [SerializeField] private float pollutionFadeDuration = 2f;

    [Header("First Dilemma")]
    [SerializeField] private float delay = 10f;

    [SerializeField, Range(0f, 1f)]
    private float pollutionAfterDilemma = 0.5f;

    private bool dilemmaTriggered = false;
    private float timer = 0f;


    private void Update()
    {
        if (dilemmaTriggered)
            return;

        timer += Time.deltaTime;

        if (timer >= delay)
        {
            TriggerFirstDilemma();
        }
    }


    private void TriggerFirstDilemma()
    {
        dilemmaTriggered = true;

        if (environment == null)
            return;

        // Set pollution to 50%
        environment.SetPollution(
            pollutionAfterDilemma
        );

        StartCoroutine(
            FadePollutionParticles()
        );
    }


    private IEnumerator FadePollutionParticles()
    {
        if (pollutionParticles == null)
            yield break;

        var emission =
            pollutionParticles.emission;

        float targetEmission =
            environment.Pollution *
            maxEmission;

        float startEmission =
            0f;

        emission.rateOverTime =
            startEmission;

        if (!pollutionParticles.isPlaying)
        {
            pollutionParticles.Play();
        }

        float elapsed = 0f;

        while (elapsed < pollutionFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    pollutionFadeDuration
                );

            // Smooth fade
            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            emission.rateOverTime =
                Mathf.Lerp(
                    startEmission,
                    targetEmission,
                    t
                );

            yield return null;
        }

        emission.rateOverTime =
            targetEmission;
    }


    // =========================================================
    // EXISTING DILEMMA CHOICES
    // =========================================================

    public void ChooseA(Dilemma dilemma)
    {
        if (environment == null)
            return;

        environment.ChangeTemperature(
            dilemma.choiceA_Temperature
        );

        environment.ChangePollution(
            dilemma.choiceA_Pollution
        );

        StartCoroutine(
            FadePollutionParticles()
        );
    }


    public void ChooseB(Dilemma dilemma)
    {
        if (environment == null)
            return;

        environment.ChangeTemperature(
            dilemma.choiceB_Temperature
        );

        environment.ChangePollution(
            dilemma.choiceB_Pollution
        );

        StartCoroutine(
            FadePollutionParticles()
        );
    }
}