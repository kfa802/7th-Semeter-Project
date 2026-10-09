
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EcosystemZone : MonoBehaviour
{
    [Header("Zone")]
    public string zoneName = "Mountain";

    [Header("Panda Detection")]
    [SerializeField] private string pandaTag = "Panda";

    // =========================================================
    // BAMBOO
    // =========================================================

    [Header("Bamboo Settings")]
    [Min(0)]
    [SerializeField] private int maximumBamboo = 10;

    [Header("Bamboo Statistics - Runtime")]
    [SerializeField] private int startingBamboo;
    [SerializeField] private int currentBamboo;
    [SerializeField] private int remainingCapacity;

    public int MaximumBamboo => maximumBamboo;
    public int StartingBamboo => startingBamboo;
    public int CurrentBamboo => currentBamboo;
    public int RemainingCapacity => remainingCapacity;

    public int GetStartingBambooCount()
    {
        return startingBamboo;
    }

    public bool CanAddBamboo(int amount = 1)
    {
        return currentBamboo + amount <= maximumBamboo;
    }

    public void SetBambooStatistics(
        int starting,
        int current)
    {
        startingBamboo = starting;
        currentBamboo = current;
        remainingCapacity =
            Mathf.Max(0, maximumBamboo - currentBamboo);
    }

    // =========================================================
    // TEMPERATURE
    // =========================================================

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

    // =========================================================
    // WATER
    // =========================================================

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

    // =========================================================
    // POLLUTION
    // =========================================================

    [Header("Pollution")]
    [Range(0f, 1f)]
    public float pollution = 0f;

    // =========================================================
    // UNITY
    // =========================================================

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

    private void Start()
    {
        // Capture the starting amount after the other
        // objects have had an opportunity to register.
        if (BambooManager.Instance != null)
        {
            BambooManager.Instance.RefreshZoneStatistics();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag(pandaTag))
            {
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