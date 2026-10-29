using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One ecosystem (Mountain, Midway, Valley...). Every environment setting
/// lives here, so each zone can be tuned on its own. When the panda enters,
/// EnvironmentSystem copies these values; when it leaves, the zone's
/// current water, pollution and temperature are saved back here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EcosystemZone : MonoBehaviour
{
    [Header("Zone")]
    public string zoneName = "Mountain";

    [Header("Panda Detection")]
    [SerializeField] private string pandaTag = "Panda";

    // Every panda collider (body parts, held bamboo, etc.)
    // currently inside this zone. The zone is only entered when
    // the first one arrives and only left when the last one leaves,
    // so extra colliders on the panda can't re-trigger the zone.
    private readonly HashSet<Collider> pandaCollidersInside =
        new HashSet<Collider>();

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
    [Tooltip("Lowest temperature the slider can reach in this zone.")]
    public float minTemperature = -15f;

    [Tooltip("Highest temperature the slider can reach in this zone.")]
    public float maxTemperature = 40f;

    [Tooltip("Start temperature of this zone. During play it is updated " +
             "with the temperature the player leaves the zone at.")]
    public float temperature = 5f;

    [Header("Bamboo Growth")]
    [Tooltip("Bamboo grows fastest at this temperature.")]
    public float idealTemperature = 20f;

    [Tooltip("± degrees around the ideal that still count as perfect.")]
    public float temperatureThreshold = 3f;

    [Tooltip("At this temperature cold growth reaches its minimum.")]
    public float freezingPoint = 0f;

    [Tooltip("Shape of the cold slowdown. 1 = even drop. Above 1 = growth drops " +
             "quickly as soon as it is below the perfect range. Below 1 = growth " +
             "stays fast longer and drops sharply near freezing.")]
    public float coldSlowdownPower = 1.5f;

    [Tooltip("Growth speed at the freezing point (0.2 = 20%).")]
    [Range(0.05f, 0.5f)]
    public float minGrowthWhenCold = 0.2f;

    [Tooltip("Growth speed at the max temperature (0.2 = 20%).")]
    [Range(0f, 1f)]
    public float minGrowthWhenHot = 0.2f;

    // =========================================================
    // WATER
    // =========================================================

    [Header("Water")]
    [Tooltip("Start water of this zone. During play it is updated " +
             "with the water the zone has when the panda leaves.")]
    [Range(0f, 100f)]
    public float water = 100f;

    [Tooltip("Water lost per second (evaporation) at the max temperature.")]
    public float waterReactionSpeed = 5f;

    [Tooltip("Water gained per second in the comfortable range.")]
    public float waterRecoverySpeed = 1f;

    [Header("Water Temperature Ranges")]
    [Tooltip("Full-speed refill from this temperature...")]
    public float comfortableWaterMinTemperature = 15f;

    [Tooltip("...up to this temperature.")]
    public float comfortableWaterMaxTemperature = 24f;

    [Tooltip("Water refills fastest (full Water Recovery Speed) at this " +
             "temperature. Should be inside the comfortable range.")]
    public float idealWaterTemperature = 20f;

    [Tooltip("Refill speed at the edges of the comfortable range " +
             "(0.5 = half speed). It rises smoothly to full speed at " +
             "the Ideal Water Temperature.")]
    [Range(0f, 1f)]
    public float comfortableEdgeRecoveryMultiplier = 0.5f;

    [Tooltip("Slow refill from this temperature...")]
    public float coldWaterMinTemperature = 0f;

    [Tooltip("...up to this one (should equal Comfortable Water Min).")]
    public float coldWaterMaxTemperature = 15f;

    [Tooltip("Refill speed at the cold end of the slow range (0.2 = 20%).")]
    [Range(0f, 1f)]
    public float coldWaterMinRecoveryMultiplier = 0.2f;

    [Tooltip("Evaporation starts at this temperature.")]
    public float hotWaterStartTemperature = 25f;

    [Header("Cold Water Loss")]
    [Tooltip("At or below this temperature the clean water starts to drop.")]
    public float coldWaterLossStartTemperature = 0f;

    [Tooltip("Water lost per second at this zone's minimum temperature.")]
    public float coldWaterLossSpeed = 5f;

    [Tooltip("Shape of the freezing curve. 2 = slow just below the start " +
             "temperature, fast near the minimum. 1 = even increase. " +
             "Below 1 = freezes fast as soon as it is below the start temperature.")]
    [Range(0.25f, 3f)]
    public float coldWaterLossPower = 1f;

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
        if (!BelongsToPanda(other))
            return;

        // Forget colliders that were destroyed or disabled while
        // inside (Unity does not send OnTriggerExit for those).
        RemoveStaleColliders();

        bool wasEmpty = pandaCollidersInside.Count == 0;
        pandaCollidersInside.Add(other);

        // Only the first panda collider actually enters the zone.
        if (!wasEmpty)
            return;

        if (EnvironmentSystem.Instance == null)
        {
            Debug.LogError(
                "No EnvironmentSystem found in the scene!"
            );
            return;
        }

        EnvironmentSystem.Instance.ApplyZoneSettings(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!BelongsToPanda(other))
            return;

        pandaCollidersInside.Remove(other);
        RemoveStaleColliders();

        // Only leave the zone when no part of the panda is inside.
        if (pandaCollidersInside.Count > 0)
            return;

        if (EnvironmentSystem.Instance != null)
        {
            EnvironmentSystem.Instance.ExitZone(this);
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private bool BelongsToPanda(Collider other)
    {
        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag(pandaTag))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void RemoveStaleColliders()
    {
        pandaCollidersInside.RemoveWhere(
            c => c == null ||
                 !c.enabled ||
                 !c.gameObject.activeInHierarchy
        );
    }
}