using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Ending", menuName = "Panda/Ending")]
public class Ending : ScriptableObject
{
    [Header("Data label (written to the CSV, same in both languages)")]
    public string endingTitle;

    [Header("Danish")]
    public string titleDA;
    [TextArea(3, 10)] public string descriptionDA;

    [Header("English")]
    public string titleEN;
    [TextArea(3, 10)] public string descriptionEN;

    [Header("Image")]
    public Sprite endingImage;

    [Header("Narration (Danish): played in order")]
    public AudioClip narrationDA1;
    public AudioClip narrationDA2;

    [Header("Narration (English): played in order")]
    public AudioClip narrationEN1;
    public AudioClip narrationEN2;

    public List<AudioClip> GetNarrations(bool english)
    {
        var clips = new List<AudioClip>();
        AudioClip a = english ? narrationEN1 : narrationDA1;
        AudioClip b = english ? narrationEN2 : narrationDA2;
        if (a != null) clips.Add(a);
        if (b != null) clips.Add(b);
        return clips;   // no fallback to the other language on purpose
    }

    // Old single-language field, kept so existing assets still work.
    [HideInInspector] public string endingDescription;

    public string GetTitle(bool english)
    {
        string wanted = english ? titleEN : titleDA;
        if (!string.IsNullOrWhiteSpace(wanted)) return wanted;

        string other = english ? titleDA : titleEN;
        return !string.IsNullOrWhiteSpace(other) ? other : endingTitle;
    }

    public string GetDescription(bool english)
    {
        string wanted = english ? descriptionEN : descriptionDA;
        if (!string.IsNullOrWhiteSpace(wanted)) return wanted;

        string other = english ? descriptionDA : descriptionEN;
        return !string.IsNullOrWhiteSpace(other) ? other : endingDescription;
    }
}