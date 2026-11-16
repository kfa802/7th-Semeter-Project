using UnityEngine;
using TMPro;

/// <summary>
/// For fixed UI text (buttons, labels, headings). Put it on the object with
/// the TextMeshPro text and write the English and Danish version.
/// The chosen language is shown when the game starts.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(1, 4)] [SerializeField] private string english;
    [TextArea(1, 4)] [SerializeField] private string danish;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        TMP_Text text = GetComponent<TMP_Text>();

        // Empty English field: keep what is already written on the text.
        if (string.IsNullOrWhiteSpace(english))
            english = text.text;

        text.text = Language.Pick(english, danish);
    }
}