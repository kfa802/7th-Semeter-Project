using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EndingUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Ending Content")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image endingImage;

    [Header("Continue button (temporary until narration exists)")]
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private string continueTextDA = "Fortsæt";
    [SerializeField] private string continueTextEN = "Continue";

    private bool English => PlayerPrefs.GetString("language", "da") == "en";

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void ShowEnding(Ending ending)
    {
        if (ending == null)
        {
            Debug.LogError("EndingUI: No ending was provided.");
            return;
        }

        bool english = English;

        if (titleText != null)
            titleText.text = ending.GetTitle(english);

        if (descriptionText != null)
            descriptionText.text = ending.GetDescription(english);

        if (continueLabel != null)
            continueLabel.text = english ? continueTextEN : continueTextDA;

        if (endingImage != null)
        {
            endingImage.sprite = ending.endingImage;
            endingImage.gameObject.SetActive(ending.endingImage != null);
        }

        if (panel != null)
            panel.SetActive(true);
    }
}