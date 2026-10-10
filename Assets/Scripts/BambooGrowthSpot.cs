
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BambooGrowthSpot : MonoBehaviour
{
    // =========================================================
    // POOP
    // =========================================================

    [Header("Poop")]
    [SerializeField] private Transform poopPoint;
    [SerializeField] private GameObject poopPrefab;


    // =========================================================
    // BAMBOO
    // =========================================================

    [Header("Bamboo")]
    [SerializeField] private GameObject bambooPrefab;
    [SerializeField] private float minSize = 1.5f;
    [SerializeField] private float maxSize = 2.5f;


    // =========================================================
    // TIMING
    // =========================================================

    [Header("Timing")]
    [SerializeField] private float poopToBambooDelay = 5f;
    [SerializeField] private float growTime = 10f;


    // =========================================================
    // STARTING BAMBOO
    // =========================================================

    [Header("Bamboo Already Here At The Start")]
    [SerializeField] private GameObject existingBamboo;
    [SerializeField] private float autoDetectRadius = 1.5f;


    // =========================================================
    // ECOSYSTEM ZONE
    // =========================================================

    [Header("Ecosystem Zone")]
    [SerializeField] private EcosystemZone ecosystemZone;

    public EcosystemZone Zone => ecosystemZone;

    public GameObject ExistingBamboo => existingBamboo;


    // =========================================================
    // STATE
    // =========================================================

    private bool occupied;
    private bool isGrowing;

    private Coroutine growthCoroutine;

    private static readonly HashSet<GameObject> claimed =
        new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claimed.Clear();
    }


    // =========================================================
    // AVAILABILITY
    // =========================================================

    public bool IsAvailable
    {
        get
        {
            // Do not release a spot while bamboo is growing.
            if (occupied &&
                existingBamboo == null &&
                !isGrowing)
            {
                occupied = false;
            }

            return !occupied && !isGrowing;
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Find starting bamboo if none was assigned manually.
        if (existingBamboo == null &&
            autoDetectRadius > 0f)
        {
            existingBamboo = FindNearbyBamboo();
        }

        // Register starting bamboo with this specific spot.
        if (existingBamboo != null)
        {
            occupied = true;
            claimed.Add(existingBamboo);

            if (BambooManager.Instance != null)
            {
                BambooManager.Instance.RegisterBamboo(
                    existingBamboo,
                    this
                );
            }
        }

        // Warn if this spot has no zone.
        if (ecosystemZone == null)
        {
            Debug.LogWarning(
                "BambooGrowthSpot '" + gameObject.name +
                "' has no Ecosystem Zone assigned.",
                this
            );
        }
    }


    // =========================================================
    // FIND STARTING BAMBOO
    // =========================================================

    private GameObject FindNearbyBamboo()
    {
        List<GameObject> candidates =
            new List<GameObject>();

        // Starting bamboo explicitly listed in BambooManager.
        if (BambooManager.Instance != null)
        {
            foreach (
                GameObject bamboo
                in BambooManager.Instance.StartingBamboo)
            {
                if (bamboo != null &&
                    !candidates.Contains(bamboo))
                {
                    candidates.Add(bamboo);
                }
            }
        }

        // BambooPlant objects already in the scene.
        BambooPlant[] plants =
            FindObjectsOfType<BambooPlant>();

        foreach (BambooPlant plant in plants)
        {
            if (plant == null)
                continue;

            GameObject bamboo = plant.gameObject;

            if (!candidates.Contains(bamboo))
            {
                candidates.Add(bamboo);
            }
        }

        GameObject best = null;

        float bestDistance = autoDetectRadius;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
                continue;

            // Do not allow two spots to claim the same plant.
            if (claimed.Contains(candidate))
                continue;

            float distance = Vector3.Distance(
                candidate.transform.position,
                transform.position
            );

            if (distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }


    // =========================================================
    // GROW BAMBOO
    // =========================================================

    public void GrowBamboo()
    {
        if (!IsAvailable)
            return;

        // Every spot must belong to a zone.
        if (ecosystemZone == null)
        {
            Debug.LogWarning(
                "Cannot grow bamboo: no zone assigned to " +
                gameObject.name,
                this
            );

            return;
        }


        // Check the zone's maximum bamboo capacity.
        if (BambooManager.Instance != null &&
            !BambooManager.Instance.CanGrowInZone(
                ecosystemZone))
        {
            Debug.Log(
                "Maximum bamboo reached in " +
                ecosystemZone.zoneName
            );

            return;
        }

        // Reserve the spot immediately.
        occupied = true;
        isGrowing = true;

        growthCoroutine = StartCoroutine(GrowthSequence());
    }


    // =========================================================
    // FREE SPOT
    // =========================================================

    public void Free()
    {
        if (existingBamboo != null)
        {
            claimed.Remove(existingBamboo);
        }

        existingBamboo = null;
        occupied = false;

        // Do not cancel a growth coroutine here.
        // Free() is also called when bamboo is removed.
    }


    // =========================================================
    // GROWTH SEQUENCE
    // =========================================================

    private IEnumerator GrowthSequence()
    {
        GameObject poop = null;

        if (poopPrefab != null &&
            poopPoint != null)
        {
            poop = Instantiate(
                poopPrefab,
                poopPoint.position,
                poopPoint.rotation
            );
        }

        // Wait before creating bamboo.
        float waited = 0f;

while (waited < poopToBambooDelay)
{
    float waterMultiplier =
        BambooManager.Instance != null
            ? BambooManager.Instance.GetWaterGrowthMultiplier()
            : 0f;

    float temperatureMultiplier =
        EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.TemperatureGrowthFactor
            : 1f;

    // Only progress when water is available.
    if (waterMultiplier > 0f)
    {
        waited += Time.deltaTime
                  * temperatureMultiplier
                  * waterMultiplier;
    }

    yield return null;
}

        if (poop != null)
        {
            Destroy(poop);
        }

        // Safety check.
        if (bambooPrefab == null)
        {
            occupied = false;
            isGrowing = false;
            growthCoroutine = null;
            yield break;
        }

        // Create the bamboo holder.
        float size = Random.Range(minSize, maxSize);

        GameObject holder = new GameObject("BambooHolder");

        holder.transform.SetPositionAndRotation(
            transform.position,
            bambooPrefab.transform.rotation
        );

        holder.transform.localScale = Vector3.one * size;

        // Create the visible bamboo as a child.
        GameObject bamboo = Instantiate(
            bambooPrefab,
            holder.transform
        );

        bamboo.transform.localPosition = Vector3.zero;
        bamboo.transform.localRotation = Quaternion.identity;

        // BambooManager tracks the holder.
        BambooPlant plant = holder.AddComponent<BambooPlant>();
        plant.Init(growTime);

        // Assign the new bamboo to this spot.
        existingBamboo = holder;

        occupied = true;
        isGrowing = false;
        growthCoroutine = null;

        claimed.Add(holder);

        if (BambooManager.Instance != null)
        {
            BambooManager.Instance.RegisterBamboo(
                holder,
                this
            );

            BambooManager.Instance.RefreshZoneStatistics();
        }
    }


    // =========================================================
    // DEBUG GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (autoDetectRadius <= 0f)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            transform.position,
            autoDetectRadius
        );
    }
}