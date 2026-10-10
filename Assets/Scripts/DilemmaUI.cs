
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

    [Header("Buttons")]
    [SerializeField] private Button choiceAButton;
    [SerializeField] private Button choiceBButton;

    private DilemmaManager manager;

    private void Awake()
    {
        panel.SetActive(false);

        choiceAButton.onClick.AddListener(ChooseA);
        choiceBButton.onClick.AddListener(ChooseB);
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

        panel.SetActive(true);
    }

    public void HideDilemma()
    {
        panel.SetActive(false);
    }

    private void ChooseA()
    {
        manager.ChooseA();
    }

    private void ChooseB()
    {
        manager.ChooseB();
    }
}