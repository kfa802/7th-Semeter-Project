using UnityEngine;

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