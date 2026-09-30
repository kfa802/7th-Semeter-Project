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

    private bool occupied;

    public bool IsAvailable => !occupied;

    // Call GrowBamboo() for a random size, or GrowBamboo(2f) for an exact one
    public void GrowBamboo(float? size = null)
    {
        if (occupied)
            return;

        float finalSize = size ?? Random.Range(minSize, maxSize);
        StartCoroutine(GrowthSequence(finalSize));
    }

    private IEnumerator GrowthSequence(float size)
    {
        occupied = true;

        // Spawn poop
        GameObject poop = null;

        if (poopPrefab != null && poopPoint != null)
        {
            poop = Instantiate(
                poopPrefab,
                poopPoint.position,
                poopPoint.rotation
            );
        }

        // Wait
        yield return new WaitForSeconds(poopToBambooDelay);

        // Remove poop
        if (poop != null)
        {
            Destroy(poop);
        }

        // Spawn bamboo
        if (bambooPrefab != null)
        {
            // Empty parent that carries the size (the Animator can't override this)
            GameObject holder = new GameObject("BambooHolder");
            holder.transform.SetPositionAndRotation(
                transform.position,
                bambooPrefab.transform.rotation
            );
            holder.transform.localScale = Vector3.one * size;

            // Bamboo keeps its own prefab scale, and the Animator plays as normal
            GameObject bamboo = Instantiate(bambooPrefab, holder.transform);
            bamboo.transform.localPosition = Vector3.zero;
            bamboo.transform.localRotation = Quaternion.identity;
        }
    }
}