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
    [Tooltip("Maximum health the panda can reach through water alone.")]
    [SerializeField, Range(0f, 100f)]
    private float maximumHealthFromWater = 50f;

    [Tooltip("Maximum health recovered per second when water is at 100%.")]
    [SerializeField]
    private float maximumHealthRecoveryPerSecond = 15f;

    [Tooltip("Higher values make recovery increase more strongly with water.")]
    [SerializeField]
    private float waterRecoveryPower = 2f;

    [Header("Health Damage From Water")]
    [Tooltip("Maximum health lost per second when water is at 0%.")]
    [SerializeField]
    private float maximumHealthLossPerSecond = 10f;

    [Tooltip("Higher values make damage increase more strongly as water gets lower.")]
    [SerializeField]
    private float waterDamagePower = 3f;

    [Header("References")]
    [SerializeField]
    private EnvironmentSystem environment;

    public float Health => health;

    // =========================================================
    // UPDATE
    // =========================================================

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
        // WATER ABOVE HEALTH THRESHOLD
        // =====================================================

        if (water > waterHealthThreshold)
        {
            // Only allow water to recover the panda
            // if health is below the water recovery limit.
            if (health < maximumHealthFromWater)
            {
                // 30% water = 0 recovery
                // 100% water = 1 recovery
                float recoveryAmount =
                    Mathf.InverseLerp(
                        waterHealthThreshold,
                        100f,
                        water
                    );

                // Exponential recovery.
                // More water = increasingly faster recovery.
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

                // Water can NEVER recover health
                // above the maximum water health.
                health =
                    Mathf.Min(
                        health,
                        maximumHealthFromWater
                    );
            }
        }

        // =====================================================
        // WATER BELOW HEALTH THRESHOLD
        // =====================================================

        else if (water < waterHealthThreshold)
        {
            // 30% water = 0 damage
            // 0% water = 1 damage
            float damageAmount =
                1f -
                (water / waterHealthThreshold);

            damageAmount =
                Mathf.Clamp01(
                    damageAmount
                );

            // Exponential damage.
            // Less water = increasingly faster damage.
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

        // =====================================================
        // CLAMP HEALTH
        // =====================================================

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

        // Feeding gives an immediate health increase.
        health += healthPerFeeding;

        // Feeding can take the panda above
        // the water recovery limit.
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