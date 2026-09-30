using System.Collections.Generic;
using UnityEngine;

public class BambooGrowthManager : MonoBehaviour
{
    [Header("Growth Spots")]
    [SerializeField] private List<BambooGrowthSpot> growthSpots =
        new List<BambooGrowthSpot>();

    public void PandaPooped()
    {
        List<BambooGrowthSpot> availableSpots =
            new List<BambooGrowthSpot>();

        foreach (BambooGrowthSpot spot in growthSpots)
        {
            if (spot != null && spot.IsAvailable)
            {
                availableSpots.Add(spot);
            }
        }

        if (availableSpots.Count == 0)
        {
            Debug.Log("No available bamboo growth spots.");
            return;
        }

        // Choose a random growth spot
        BambooGrowthSpot selectedSpot =
            availableSpots[
                Random.Range(0, availableSpots.Count)
            ];

        selectedSpot.GrowBamboo();
    }
}