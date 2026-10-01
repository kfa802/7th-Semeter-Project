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
    [SerializeField] private float minSize = 1.5f;   // 1 = prefab's normal size
    [SerializeField] private float maxSize = 2.5f;

    [Header("Timing")]
    [SerializeField] private float poopToBambooDelay = 5f;
    [SerializeField] private float growTime = 10f;   // length of your grow animation (seconds)

    [Header("Bamboo already here at the start")]
    // Drag the bamboo from the scene that stands on this spot (optional)
    [SerializeField] private GameObject existingBamboo;
    // If the slot above is empty, use the nearest bamboo within this distance (0 = off)
    [SerializeField] private float autoDetectRadius = 1.5f;

    private bool occupied;

    // Bamboo that another spot already took, so two spots don't share one
    private static readonly HashSet<GameObject> claimed = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claimed.Clear();
    }

    public bool IsAvailable => !occupied;


    private void Start()
    {
        if (existingBamboo == null && autoDetectRadius > 0f)
            existingBamboo = FindNearbyBamboo();

        // A bamboo already stands here, so the spot is taken until it disappears
        if (existingBamboo != null)
        {
            occupied = true;
            claimed.Add(existingBamboo);

            if (BambooManager.Instance != null)
                BambooManager.Instance.RegisterBamboo(existingBamboo, this);
        }
    }

    private GameObject FindNearbyBamboo()
    {
        List<GameObject> candidates = new List<GameObject>();

        if (BambooManager.Instance != null)
            candidates.AddRange(BambooManager.Instance.StartingBamboo);

        foreach (BambooPlant plant in FindObjectsOfType<BambooPlant>())
            candidates.Add(plant.gameObject);

        GameObject best = null;
        float bestDistance = autoDetectRadius;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || claimed.Contains(candidate))
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


    public void GrowBamboo()
    {
        if (occupied)
            return;

        StartCoroutine(GrowthSequence());
    }

    // Called by BambooManager when this spot's bamboo is gone (rotted, removed, destroyed)
    public void Free()
    {
        if (existingBamboo != null)
            claimed.Remove(existingBamboo);

        existingBamboo = null;
        occupied = false;
    }

    private IEnumerator GrowthSequence()
    {
        occupied = true;

        GameObject poop = null;

        if (poopPrefab != null && poopPoint != null)
            poop = Instantiate(poopPrefab, poopPoint.position, poopPoint.rotation);

        // The wait also runs slower when it is cold
        float waited = 0f;

        while (waited < poopToBambooDelay)
        {
            float factor = EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.TemperatureGrowthFactor
                : 1f;

            waited += Time.deltaTime * factor;
            yield return null;
        }

        if (poop != null)
            Destroy(poop);

        if (bambooPrefab == null)
        {
            // Nothing to grow, so don't block the spot forever
            occupied = false;
            yield break;
        }

        float size = Random.Range(minSize, maxSize);

        GameObject holder = new GameObject("BambooHolder");
        holder.transform.SetPositionAndRotation(
            transform.position,
            bambooPrefab.transform.rotation
        );
        holder.transform.localScale = Vector3.one * size;

        GameObject bamboo = Instantiate(bambooPrefab, holder.transform);
        bamboo.transform.localPosition = Vector3.zero;
        bamboo.transform.localRotation = Quaternion.identity;

        holder.AddComponent<BambooPlant>().Init(growTime);

        // Remember it, so Free() can release it later
        existingBamboo = holder;
        claimed.Add(holder);

        if (BambooManager.Instance != null)
            BambooManager.Instance.RegisterBamboo(holder, this);
    }

    // Shows the detection radius when you select the spot
    private void OnDrawGizmosSelected()
    {
        if (autoDetectRadius <= 0f)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, autoDetectRadius);
    }
}