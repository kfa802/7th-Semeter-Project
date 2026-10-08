using UnityEngine;

public class PandaSystem : MonoBehaviour
{
    [Header("Panda Health")]
    [SerializeField, Range(0f, 100f)]
    private float health = 100f;

    [Header("Feeding")]
    [SerializeField]
    private float healthPerFeeding = 10f;

    [Header("Water Threshold")]
    [SerializeField, Range(0f, 100f)]
    private float waterHealthThreshold = 30f;

    [Header("Health Recovery")]
    [SerializeField]
    private float maximumHealthRecoveryPerSecond = 15f;

    [SerializeField]
    private float waterRecoveryPower = 2f;

    [Header("Health Damage")]
    [SerializeField]
    private float maximumHealthLossPerSecond = 10f;

    [SerializeField]
    private float waterDamagePower = 3f;

    [Header("References")]
    [SerializeField]
    private EnvironmentSystem environment;

    public float Health => health;

    private void Update()
    {
        if (environment == null)
            return;

        float water = environment.Water;

        float healthChangePerSecond = 0f;

        // =====================================================
        // WATER ABOVE 30%
        // =====================================================

        if (water > waterHealthThreshold)
        {
            float recoveryAmount =
                Mathf.InverseLerp(
                    waterHealthThreshold,
                    100f,
                    water
                );

            recoveryAmount =
                Mathf.Pow(
                    recoveryAmount,
                    waterRecoveryPower
                );

            healthChangePerSecond =
                recoveryAmount *
                maximumHealthRecoveryPerSecond;
        }

        // =====================================================
        // WATER BELOW 30%
        // =====================================================

        else if (water < waterHealthThreshold)
        {
            float damageAmount =
                1f -
                (water / waterHealthThreshold);

            damageAmount =
                Mathf.Clamp01(
                    damageAmount
                );

            damageAmount =
                Mathf.Pow(
                    damageAmount,
                    waterDamagePower
                );

            healthChangePerSecond =
                -damageAmount *
                maximumHealthLossPerSecond;
        }

        // =====================================================
        // APPLY HEALTH CHANGE
        // =====================================================

        health +=
            healthChangePerSecond *
            Time.deltaTime;

        health =
            Mathf.Clamp(
                health,
                0f,
                100f
            );
    }

    // =========================================================
    // FEEDING
    // =========================================================

    public void FeedPanda()
    {
        health += healthPerFeeding;

        health =
            Mathf.Clamp(
                health,
                0f,
                100f
            );

        Debug.Log(
            "Panda fed. Health = " +
            health.ToString("0.00")
        );
    }
}