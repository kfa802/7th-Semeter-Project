using System.Collections.Generic;
using UnityEngine;

public enum QuizCondition
{
    Everyone,
    DilemmaChoice,       // dilemma resolved with a specific choice
    DilemmaReached,      // dilemma was resolved (any choice)
    EndingReached,       // the player got a specific ending
    GameEventTriggered   // a specific GameEvent has been executed
}

[System.Serializable]
public class QuizOption
{
    [TextArea(1, 3)] public string textDA;
    [TextArea(1, 3)] public string textEN;

    [Tooltip("Part of the correct answer. Leave all unticked for opinion questions.")]
    public bool correct;

    [Tooltip("Choosing this clears all other options (e.g. 'Ved ikke').")]
    public bool exclusive;

    [Tooltip("Optional picture, e.g. a smiley.")]
    public Sprite icon;
}

[CreateAssetMenu(fileName = "New Quiz Question", menuName = "Panda/Quiz Question")]
public class QuizQuestion : ScriptableObject
{
    [Header("Question")]
    [Tooltip("Used as the column name, e.g. Q1, Q4a.")]
    public string id = "Q1";

    [TextArea(2, 5)] public string textDA;
    [TextArea(2, 5)] public string textEN;

    [Header("Answer type")]
    public bool multiSelect;
    public bool showLetters = true;
    [Tooltip("Place the options in a row (for the smiley scale).")]
    public bool horizontalLayout;
    [Tooltip("Log 1, 2, 3... instead of A, B, C... (for scales).")]
    public bool logAsNumber;

    public List<QuizOption> options = new();

    [Header("When is it shown?")]
    public QuizCondition condition = QuizCondition.Everyone;

    [Tooltip("Used by DilemmaChoice and DilemmaReached.")]
    public Dilemma dilemma;

    [Tooltip("Used by DilemmaChoice: which choice must have been made.")]
    public PrerequisiteChoice requiredChoice = PrerequisiteChoice.Either;

    [Tooltip("Used by EndingReached.")]
    public Ending ending;

    [Tooltip("Used by GameEventTriggered.")]
    public GameEvent gameEvent;
}