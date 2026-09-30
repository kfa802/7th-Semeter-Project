using System.Collections;
using UnityEngine;

public class BambooGrowthSpot : MonoBehaviour
{
    [Header("Poop")]
    [SerializeField] private Transform poopPoint;
    [SerializeField] private GameObject poopPrefab;

    [Header("Bamboo")]
    [SerializeField] private GameObject bambooPrefab;

    [Header("Timing")]
    [SerializeField] private float poopToBambooDelay = 5f;

    private bool occupied;

    public bool IsAvailable => !occupied;

    public void GrowBamboo()
    {
        if (occupied)
            return;

        StartCoroutine(GrowthSequence());
    }

    private IEnumerator GrowthSequence()
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
            GameObject bamboo = Instantiate(
                bambooPrefab,
                transform.position,
                bambooPrefab.transform.rotation
            );

            // Explicitly copy the prefab's scale
            bamboo.transform.localScale =
                bambooPrefab.transform.localScale;

            // Start growth
            BambooPlant bambooPlant =
                bamboo.GetComponent<BambooPlant>();

            if (bambooPlant != null)
            {
                bambooPlant.Grow();
            }
        }
    }
}