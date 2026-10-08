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

    [Header("Health Recovery From Water")]
    [SerializeField]
    private float maximumHealthRecoveryPerSecond = 15f;

    [SerializeField]
    private float waterRecoveryPower = 2f;

    [Header("Health Damage From Water")]
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

        UpdateHealthFromWater();
    }

    // =========================================================
    // WATER → HEALTH
    // =========================================================

    private void UpdateHealthFromWater()
    {
        float water = environment.Water;

        // =====================================================
        // WATER ABOVE THRESHOLD
        // =====================================================

        if (water > waterHealthThreshold)
        {
            float recoveryAmount =
                Mathf.InverseLerp(
                    waterHealthThreshold,
                    100f,
                    water
                );

            // Exponential recovery:
            // closer to 100% water = increasingly faster recovery
            recoveryAmount =
                Mathf.Pow(
                    recoveryAmount,
                    waterRecoveryPower
                );

            float recoveryPerSecond =
                recoveryAmount *
                maximumHealthRecoveryPerSecond;

            health +=
                recoveryPerSecond *
                Time.deltaTime;
        }

        // =====================================================
        // WATER BELOW THRESHOLD
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

            // Exponential damage:
            // closer to 0% water = increasingly faster damage
            damageAmount =
                Mathf.Pow(
                    damageAmount,
                    waterDamagePower
                );

            float damagePerSecond =
                damageAmount *
                maximumHealthLossPerSecond;

            health -=
                damagePerSecond *
                Time.deltaTime;
        }

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
        float oldHealth = health;

        // Instant health increase.
        health += healthPerFeeding;

        health =
            Mathf.Clamp(
                health,
                0f,
                100f
            );

        Debug.Log(
            "PANDA FED! " +
            oldHealth.ToString("0.0") +
            " -> " +
            health.ToString("0.0")
        );
    }
}