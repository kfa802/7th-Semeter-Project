
using UnityEngine;

[CreateAssetMenu(fileName = "New Ending", menuName = "Panda/Ending")]
public class Ending : ScriptableObject
{
    [Header("Ending Details")]
    public string endingTitle;

    [TextArea(3, 8)]
    public string endingDescription;

    public Sprite endingImage;
}