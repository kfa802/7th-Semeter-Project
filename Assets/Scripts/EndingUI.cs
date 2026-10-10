
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

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void ShowEnding(Ending ending)
    {
        if (ending == null)
        {
            Debug.LogError("EndingUI: No ending was provided.");
            return;
        }

        if (titleText != null)
        {
            titleText.text = ending.endingTitle;
        }

        if (descriptionText != null)
        {
            descriptionText.text = ending.endingDescription;
        }

        if (endingImage != null)
        {
            endingImage.sprite = ending.endingImage;
            endingImage.gameObject.SetActive(
                ending.endingImage != null
            );
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }
}