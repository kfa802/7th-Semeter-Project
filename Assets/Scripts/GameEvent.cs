
using UnityEngine;

[CreateAssetMenu(fileName = "New Game Event", menuName = "Panda/Game Event")]
public class GameEvent : ScriptableObject
{
    [Header("Environment Changes")]
    public float temperatureChange;
    public float pollutionChange;

    [Header("Travel")]
    [Tooltip("Enable this if the event should move the panda.")]
    public bool requestTravel;

    [Tooltip("Must match a Zone Name in PandaPathMover.")]
    public string targetZoneName;
}