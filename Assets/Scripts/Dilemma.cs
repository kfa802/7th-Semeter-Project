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
    TemperatureTooHigh,
    TemperatureModerate,

    // Checked ONCE, 'Delay Seconds' after entering the zone: which temperature
    // did the player keep it at the LONGEST? Only one of these three appears per zone.
    MostTimeHot,     // Mostly hot: the panda gets stressed by the heat.
    MostTimeMild,    // Mostly mild: forgot to feed / too little bamboo on the mountain.
    MostTimeCold     // Mostly cold: the bamboo can't grow.
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

    [Header("Danish Text (shown when Danish is chosen; empty = English)")]
    public string titleDA;

    [TextArea(3, 6)]
    public string descriptionDA;

    public string choiceADA;
    public string choiceBDA;
    public string choiceCDA;

    [Header("Narration")]
    [Tooltip("Voice-over played when this dilemma appears. " +
             "The choice buttons unlock when it has finished. Empty = no narration.")]
    public AudioClip narrationClip;

    [Tooltip("Danish voice-over. Empty = the English one is used.")]
    public AudioClip narrationClipDA;

    [Header("Choice Buttons Appear (seconds into the narration)")]
    [Tooltip("When choice A pops up, in seconds after the narration clip starts. " +
             "0 = visible right away.")]
    [Min(0f)] public float choiceAAppearAt;
    [Min(0f)] public float choiceBAppearAt;
    [Min(0f)] public float choiceCAppearAt;

    [Tooltip("Same, for the DANISH narration clip. 0 = use the English time.")]
    [Min(0f)] public float choiceAAppearAtDA;
    [Min(0f)] public float choiceBAppearAtDA;
    [Min(0f)] public float choiceCAppearAtDA;

    /// <summary>Seconds into the narration when choice 0/1/2 appears (chosen language).</summary>
    public float ChoiceAppearAt(int choice)
    {
        float en = choice == 0 ? choiceAAppearAt : choice == 1 ? choiceBAppearAt : choiceCAppearAt;
        float da = choice == 0 ? choiceAAppearAtDA : choice == 1 ? choiceBAppearAtDA : choiceCAppearAtDA;
        return Language.IsDanish && da > 0f ? da : en;
    }

    // The text/clip in the chosen language.
    public string Title => Language.Pick(title, titleDA);
    public string Description => Language.Pick(description, descriptionDA);
    public string ChoiceA => Language.Pick(choiceA, choiceADA);
    public string ChoiceB => Language.Pick(choiceB, choiceBDA);
    public string ChoiceC => Language.Pick(choiceC, choiceCDA);
    public AudioClip NarrationClip => Language.Pick(narrationClip, narrationClipDA);

    [Header("Trigger")]
    public DilemmaTriggerType triggerType;
    public string requiredZoneName;

    [Tooltip("Delay in seconds for ZoneDelay and 'Most Time...' triggers, " +
             "and for scheduled follow-up dilemmas.")]
    public float delaySeconds = 30f;

    [Tooltip("Threshold percentage for WaterBelow, PollutionAbove, or BambooBelow.")]
    [Range(0f, 100f)]
    public float threshold = 30f;

    [Header("Temperature Trigger")]
    [Tooltip("Temperature threshold in degrees Celsius for this specific dilemma.")]
    public float temperatureThreshold = 30f;

    [Tooltip("Upper temperature boundary for a moderate-temperature dilemma.")]
    public float temperatureHighThreshold = 25f;

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