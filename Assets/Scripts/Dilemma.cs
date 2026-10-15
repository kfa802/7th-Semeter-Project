
using System.Collections.Generic;
using UnityEngine;

public enum DilemmaTriggerType
{
    ZoneDelay,
    ZoneEntered,
    WaterBelow,
    PollutionAbove,
    BambooBelow,
    TemperatureTooLow,
    TemperatureTooHigh
}

public enum PrerequisiteChoice
{
    Either,
    ChoiceA,
    ChoiceB,
    ChoiceC
}

[System.Serializable]
public class DilemmaPrerequisite
{
    [Tooltip("This dilemma can unlock the follow-up.")]
    public Dilemma dilemma;

    [Tooltip("Which choice must have been selected.")]
    public PrerequisiteChoice requiredChoice = PrerequisiteChoice.Either;
}

[System.Serializable]
public class DilemmaBlockRule
{
    [Tooltip("Resolving this dilemma with the selected choice can block this dilemma.")]
    public Dilemma dilemma;

    [Tooltip("Which choice blocks this dilemma. Either means any choice.")]
    public PrerequisiteChoice requiredChoice = PrerequisiteChoice.Either;
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
    public string choiceC;

    [Header("Trigger")]
    public DilemmaTriggerType triggerType;
    public string requiredZoneName;

    [Tooltip("Delay in seconds for ZoneDelay triggers and scheduled follow-up dilemmas.")]
    public float delaySeconds = 30f;

    [Tooltip("Threshold percentage for WaterBelow, PollutionAbove, or BambooBelow.")]
    [Range(0f, 100f)]
    public float threshold = 30f;

    [Header("Temperature Trigger")]
    [Tooltip("Temperature threshold in degrees Celsius for this specific dilemma.")]
    public float temperatureThreshold = 30f;

    [Tooltip("How long the temperature condition must persist, in seconds.")]
    [Min(0f)]
    public float temperatureDurationSeconds = 20f;

    [Header("Prerequisites")]
    [Tooltip("The dilemma can be unlocked by ANY ONE of these prerequisites.")]
    public List<DilemmaPrerequisite> prerequisiteDilemmas =
        new List<DilemmaPrerequisite>();

    [Header("Blocked By Dilemmas")]
    [Tooltip("If ANY listed dilemma is resolved with its matching choice, this dilemma becomes permanently unavailable for this playthrough.")]
    public List<DilemmaBlockRule> blockedByDilemmas =
        new List<DilemmaBlockRule>();

    // Legacy fields retained for existing assets.
    [HideInInspector]
    public Dilemma prerequisiteDilemma;

    [HideInInspector]
    public PrerequisiteChoice prerequisiteChoice =
        PrerequisiteChoice.Either;

    [Header("Choice Consequences")]
    public GameEvent choiceAEvent;
    public GameEvent choiceBEvent;
    public GameEvent choiceCEvent;

    [Header("Choice Endings")]
    public Ending choiceAEnding;
    public Ending choiceBEnding;
    public Ending choiceCEnding;
}
