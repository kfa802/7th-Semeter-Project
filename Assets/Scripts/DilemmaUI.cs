using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DilemmaUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text choiceAText;
    [SerializeField] private TMP_Text choiceBText;
    [SerializeField] private TMP_Text choiceCText;

    [Header("Buttons")]
    [SerializeField] private Button choiceAButton;
    [SerializeField] private Button choiceBButton;
    [SerializeField] private Button choiceCButton;

    private DilemmaManager manager;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        choiceAButton.onClick.AddListener(ChooseA);
        choiceBButton.onClick.AddListener(ChooseB);

        if (choiceCButton != null)
            choiceCButton.onClick.AddListener(ChooseC);
    }

    public void ShowDilemma(
        Dilemma dilemma,
        DilemmaManager dilemmaManager)
    {
        manager = dilemmaManager;

        titleText.text = dilemma.title;
        descriptionText.text = dilemma.description;
        choiceAText.text = dilemma.choiceA;
        choiceBText.text = dilemma.choiceB;

        bool hasChoiceC = !string.IsNullOrWhiteSpace(dilemma.choiceC);

        if (choiceCButton != null)
            choiceCButton.gameObject.SetActive(hasChoiceC);

        if (hasChoiceC && choiceCText != null)
            choiceCText.text = dilemma.choiceC;

        panel.SetActive(true);
    }

    public void HideDilemma()
    {
        panel.SetActive(false);
    }

    private void ChooseA()
    {
        if (manager != null)
            manager.ChooseA();
    }

    private void ChooseB()
    {
        if (manager != null)
            manager.ChooseB();
    }

    private void ChooseC()
    {
        if (manager != null)
            manager.ChooseC();
    }
}
