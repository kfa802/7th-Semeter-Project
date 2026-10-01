using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BambooGrowthSpot : MonoBehaviour
{
    [Header("Poop")]
    [SerializeField] private Transform poopPoint;
    [SerializeField] private GameObject poopPrefab;

    [Header("Bamboo")]
    [SerializeField] private GameObject bambooPrefab;
    [SerializeField] private float minSize = 1.5f;
    [SerializeField] private float maxSize = 2.5f;

    [Header("Timing")]
    [SerializeField] private float poopToBambooDelay = 5f;
    [SerializeField] private float growTime = 10f;

    [Header("Bamboo already here at the start")]
    [SerializeField] private GameObject existingBamboo;
    [SerializeField] private float autoDetectRadius = 1.5f;

    private bool occupied;

    private static readonly HashSet<GameObject> claimed =
        new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claimed.Clear();
    }

    public bool IsAvailable
    {
        get
        {
            // If the bamboo assigned to this spot has been destroyed,
            // the spot is available again.
            if (occupied && existingBamboo == null)
            {
                occupied = false;
            }

            return !occupied;
        }
    }

    private void Start()
    {
        // -----------------------------------------------------
        // FIND EXISTING BAMBOO
        // -----------------------------------------------------

        if (existingBamboo == null &&
            autoDetectRadius > 0f)
        {
            existingBamboo = FindNearbyBamboo();
        }

        // -----------------------------------------------------
        // REGISTER EXISTING BAMBOO
        // -----------------------------------------------------

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
    }

    private GameObject FindNearbyBamboo()
    {
        List<GameObject> candidates =
            new List<GameObject>();

        // Starting bamboo from BambooManager
        if (BambooManager.Instance != null)
        {
            foreach (
                GameObject bamboo
                in BambooManager.Instance.StartingBamboo
            )
            {
                if (bamboo != null)
                {
                    candidates.Add(bamboo);
                }
            }
        }

        // BambooPlant objects already in the scene
        BambooPlant[] plants =
            FindObjectsOfType<BambooPlant>();

        foreach (BambooPlant plant in plants)
        {
            if (plant == null)
                continue;

            if (!candidates.Contains(plant.gameObject))
            {
                candidates.Add(plant.gameObject);
            }
        }

        GameObject best = null;

        float bestDistance =
            autoDetectRadius;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
                continue;

            // Don't let two growth spots claim the same bamboo.
            if (claimed.Contains(candidate))
                continue;

            float distance =
                Vector3.Distance(
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

        StartCoroutine(GrowthSequence());
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
    }

    // =========================================================
    // GROWTH SEQUENCE
    // =========================================================

    private IEnumerator GrowthSequence()
    {
        // Reserve the spot immediately.
        occupied = true;

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

        float waited = 0f;

        while (waited < poopToBambooDelay)
        {
            float factor =
                EnvironmentSystem.Instance != null
                    ? EnvironmentSystem.Instance.TemperatureGrowthFactor
                    : 1f;

            waited +=
                Time.deltaTime * factor;

            yield return null;
        }

        // Remove poop
        if (poop != null)
        {
            Destroy(poop);
        }

        // -----------------------------------------------------
        // SAFETY CHECK
        // -----------------------------------------------------

        if (bambooPrefab == null)
        {
            occupied = false;
            yield break;
        }

        // -----------------------------------------------------
        // CREATE BAMBOO
        // -----------------------------------------------------

        float size =
            Random.Range(
                minSize,
                maxSize
            );

        GameObject holder =
            new GameObject(
                "BambooHolder"
            );

        holder.transform.SetPositionAndRotation(
            transform.position,
            bambooPrefab.transform.rotation
        );

        holder.transform.localScale =
            Vector3.one * size;

        GameObject bamboo =
            Instantiate(
                bambooPrefab,
                holder.transform
            );

        bamboo.transform.localPosition =
            Vector3.zero;

        bamboo.transform.localRotation =
            Quaternion.identity;

        // Add BambooPlant to the HOLDER,
        // because the holder is what BambooManager tracks.
        BambooPlant plant =
            holder.AddComponent<BambooPlant>();

        plant.Init(growTime);

        // -----------------------------------------------------
        // REGISTER THE NEW BAMBOO
        // -----------------------------------------------------

        existingBamboo = holder;

        claimed.Add(holder);

        if (BambooManager.Instance != null)
        {
            BambooManager.Instance.RegisterBamboo(
                holder,
                this
            );
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