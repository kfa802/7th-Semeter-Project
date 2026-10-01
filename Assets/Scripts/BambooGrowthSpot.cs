using System.Collections;
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

    private bool occupied;

    public bool IsAvailable => !occupied;

    public void GrowBamboo()
    {
        if (occupied)
            return;

        StartCoroutine(GrowthSequence());
    }

    // Called by BambooManager when this spot's bamboo is removed (rotted, etc.)
    public void Free()
    {
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

        // Tell the manager this bamboo exists (the holder, so removing it removes everything)
        if (BambooManager.Instance != null)
            BambooManager.Instance.RegisterBamboo(holder, this);
    }
}