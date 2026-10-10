
using UnityEngine;

public enum DilemmaTriggerType
{
    ZoneDelay,
    ZoneEntered,
    WaterBelow,
    PollutionAbove
}

[CreateAssetMenu(fileName = "New Dilemma", menuName = "Panda/Dilemma")]
public class Dilemma : ScriptableObject
{
    [Header("Dilemma Text")]
    public string title;

    [TextArea(3, 6)]
    public string description;

    public string choiceA;
    public string choiceB;

    [Header("Trigger")]
    public DilemmaTriggerType triggerType;
    public string requiredZoneName;

    [Tooltip("Used for ZoneDelay, in seconds.")]
    public float delaySeconds = 30f;

    [Tooltip("Used for WaterBelow or PollutionAbove.")]
    [Range(0f, 100f)]
    public float threshold = 30f;

    [Header("Choice Consequences")]
    public GameEvent choiceAEvent;
    public GameEvent choiceBEvent;
}