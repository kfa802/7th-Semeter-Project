
using UnityEngine;

public enum DilemmaTriggerType
{
    ZoneDelay,
    ZoneEntered,
    WaterBelow,
    PollutionAbove
}

public enum PrerequisiteChoice
{
    Either,
    ChoiceA,
    ChoiceB
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

    [Tooltip("Delay in seconds for ZoneDelay triggers.")]
    public float delaySeconds = 30f;

    [Tooltip("Threshold for WaterBelow or PollutionAbove.")]
    [Range(0f, 100f)]
    public float threshold = 30f;

    [Header("Prerequisites")]
    public Dilemma prerequisiteDilemma;

    public PrerequisiteChoice prerequisiteChoice =
        PrerequisiteChoice.Either;

    [Header("Choice Consequences")]
    public GameEvent choiceAEvent;
    public GameEvent choiceBEvent;

    [Header("Choice Endings")]
    public Ending choiceAEnding;
    public Ending choiceBEnding;
}