using UnityEngine;

public class PandaSystem : MonoBehaviour
{
    [Header("Panda")]
    [SerializeField] private float health = 100f;

    [Header("Health Reaction")]
    [SerializeField] private float healthReactionSpeed = 2f;

    [Header("Water Health")]
    [SerializeField, Range(0f, 100f)]
    private float waterHealthThreshold = 30f;

    [SerializeField]
    private float waterDamagePower = 2f;

    [Header("Resource Importance")]
    [SerializeField]
    private float waterImportance = 1f;

    [Header("References")]
    [SerializeField]
    private EnvironmentSystem environment;

    public float Health => health;


    private void Start()
    {
        // Panda always starts with full health
        health = 100f;
    }


    private void Update()
    {
        if (environment == null)
            return;


        float water = environment.Water;


        // -----------------------------------------------------
        // WATER HEALTH
        // -----------------------------------------------------

        float waterHealth;


        if (water >= waterHealthThreshold)
        {
            // Above 30% water:
            // Panda stays completely healthy.

            waterHealth = 1f;
        }
        else
        {
            // Convert the dangerous water range into 0–1.
            //
            // 30% water = 0 stress
            // 0% water  = 1 stress

            float waterStress =
                1f -
                (water / waterHealthThreshold);

            waterStress =
                Mathf.Clamp01(waterStress);


            // Make the damage increasingly stronger
            // as the water gets lower.

            float exponentialStress =
                Mathf.Pow(
                    waterStress,
                    waterDamagePower
                );


            // Convert stress into a health value.
            //
            // 30% water = 100% health
            // 0% water  = 0% health

            waterHealth =
                1f -
                exponentialStress;
        }


        // -----------------------------------------------------
        // TARGET HEALTH
        // -----------------------------------------------------

        float targetHealth =
            waterHealth * 100f;


        // -----------------------------------------------------
        // HEALTH MOVES TOWARD TARGET
        // -----------------------------------------------------

        health = Mathf.MoveTowards(
            health,
            targetHealth,
            healthReactionSpeed * Time.deltaTime
        );


        // Keep health between 0 and 100.

        health = Mathf.Clamp(
            health,
            0f,
            100f
        );
    }
}