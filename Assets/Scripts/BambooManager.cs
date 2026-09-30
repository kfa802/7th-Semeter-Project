using System.Collections.Generic;
using UnityEngine;

public class BambooManager : MonoBehaviour
{
    public static BambooManager Instance;

    private static readonly HashSet<BambooPlant> bambooPlants =
        new HashSet<BambooPlant>();


    private void Awake()
    {
        Instance = this;
    }


    public static void RegisterBamboo(BambooPlant bamboo)
    {
        bambooPlants.Add(bamboo);
    }


    public static void UnregisterBamboo(BambooPlant bamboo)
    {
        bambooPlants.Remove(bamboo);
    }


    public int BambooCount
    {
        get
        {
            return bambooPlants.Count;
        }
    }
}