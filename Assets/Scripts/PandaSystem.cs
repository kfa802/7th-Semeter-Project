using UnityEngine;

public class PandaSystem : MonoBehaviour
{
    [Header("Panda")]
    [SerializeField] private float health = 100f;

    [Header("Health Reaction")]
    [SerializeField] private float healthReactionSpeed = 2f;

    [Header("Resource Importance")]
    [SerializeField] private float waterImportance = 0.5f;
    [SerializeField] private float bambooImportance = 0.5f;

    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;

    public float Health => health;


    private void Start()
    {
        health = 100f;
    }


    private void Update()
    {
        if (environment == null)
            return;


        float water = environment.Water;
        float bamboo = environment.Bamboo;


        // -----------------------------------------------------
        // CALCULATE HOW HEALTHY THE ENVIRONMENT IS
        // -----------------------------------------------------

        float waterHealth =
            water / 100f;

        float bambooHealth =
            bamboo / 100f;


        // Combine water and bamboo.
        float environmentHealth =
            (waterHealth * waterImportance) +
            (bambooHealth * bambooImportance);


        // Convert back to 0-100.
        float targetHealth =
            environmentHealth * 100f;


        // -----------------------------------------------------
        // HEALTH MOVES TOWARD TARGET
        // -----------------------------------------------------

        health = Mathf.MoveTowards(
            health,
            targetHealth,
            healthReactionSpeed * Time.deltaTime
        );


        health = Mathf.Clamp(
            health,
            0f,
            100f
        );
    }
}