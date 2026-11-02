using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SubtitleLine
{
    [Tooltip("Seconds into the narration clip when this line appears.")]
    [Min(0f)]
    public float startTime;

    [Tooltip("Seconds into the clip when it disappears. 0 = stays until the next line starts (or the clip ends).")]
    [Min(0f)]
    public float endTime;

    [TextArea(1, 3)]
    public string text;
}

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

    [Header("Narration")]
    [Tooltip("Voice-over played after the player picks the choice that triggers " +
             "this event, explaining what the choice means. Empty = no narration.")]
    public AudioClip narrationClip;

    [Tooltip("Seconds after the choice is clicked before the narration starts.")]
    [Min(0f)]
    public float narrationDelay = 0.5f;

    [Tooltip("Subtitles shown during the narration, timed to the clip. " +
             "Put them in order of Start Time.")]
    public List<SubtitleLine> subtitles = new List<SubtitleLine>();
}