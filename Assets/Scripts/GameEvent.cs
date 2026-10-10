
using UnityEngine;

[CreateAssetMenu(fileName = "New Game Event", menuName = "Panda/Game Event")]
public class GameEvent : ScriptableObject
{
    [Header("Environment Changes")]
    public float temperatureChange;
    public float pollutionChange;

    [Header("Travel")]
    public bool requestTravel;
    public string targetZoneName;
}