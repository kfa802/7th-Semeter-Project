
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EcosystemZone : MonoBehaviour
{
    [Header("Zone")]
    public string zoneName = "Mountain";

    [Header("Panda Detection")]
    [SerializeField] private string pandaTag = "Panda";

    [Header("Temperature")]
    public float minTemperature = -15f;
    public float maxTemperature = 40f;
    public float temperature = 5f;
    public float idealTemperature = 20f;
    public float temperatureThreshold = 3f;
    public float freezingPoint = 0f;
    public float coldSlowdownPower = 1.5f;

    [Range(0.05f, 0.5f)]
    public float minGrowthWhenCold = 0.2f;

    [Range(0f, 1f)]
    public float minGrowthWhenHot = 0.2f;

    [Header("Water")]
    [Range(0f, 100f)]
    public float water = 100f;
    public float waterReactionSpeed = 5f;
    public float waterRecoverySpeed = 1f;

    [Header("Water Temperature Ranges")]
    public float comfortableWaterMinTemperature = 15f;
    public float comfortableWaterMaxTemperature = 24f;
    public float coldWaterMinTemperature = 0f;
    public float coldWaterMaxTemperature = 15f;

    [Range(0f, 1f)]
    public float coldWaterMinRecoveryMultiplier = 0.2f;

    public float hotWaterStartTemperature = 25f;

    [Header("Pollution")]
    [Range(0f, 1f)]
    public float pollution = 0f;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;

        Debug.Log(
            "Zone ready: " + zoneName +
            " | Collider: " + zoneCollider.GetType().Name +
            " | Is Trigger: " + zoneCollider.isTrigger,
            this
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            "Something entered " + zoneName +
            ": " + other.name +
            " | Tag: " + other.tag,
            other
        );

        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag(pandaTag))
            {
                Debug.Log(
                    "PANDA DETECTED IN ZONE: " + zoneName,
                    this
                );

                if (EnvironmentSystem.Instance == null)
                {
                    Debug.LogError(
                        "No EnvironmentSystem found in the scene!"
                    );
                    return;
                }

                EnvironmentSystem.Instance.ApplyZoneSettings(this);
                return;
            }

            current = current.parent;
        }

        Debug.Log(
            "Entering object was not tagged Panda: " + other.name,
            this
        );
    }

    private void OnTriggerExit(Collider other)
{
    Transform current = other.transform;

    while (current != null)
    {
        if (current.CompareTag(pandaTag))
        {
            if (EnvironmentSystem.Instance != null)
            {
                EnvironmentSystem.Instance.ExitZone(this);
            }

            return;
        }

        current = current.parent;
    }
}
}