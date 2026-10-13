
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

    [Header("Input Blocking")]
    [Tooltip("Optional full-screen UI Image behind the dilemma content. " +
             "It should cover the screen and have Raycast Target enabled.")]
    [SerializeField] private GameObject inputBlocker;

    private DilemmaManager manager;

    private float previousTimeScale = 1f;
    private bool pausedByDilemma;

    public bool IsDilemmaOpen { get; private set; }

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (inputBlocker != null)
            inputBlocker.SetActive(false);

        if (choiceAButton != null)
            choiceAButton.onClick.AddListener(ChooseA);

        if (choiceBButton != null)
            choiceBButton.onClick.AddListener(ChooseB);

        if (choiceCButton != null)
            choiceCButton.onClick.AddListener(ChooseC);
    }

    public void ShowDilemma(
        Dilemma dilemma,
        DilemmaManager dilemmaManager)
    {
        if (dilemma == null)
        {
            Debug.LogError("DilemmaUI: Cannot show a missing dilemma.");
            return;
        }

        manager = dilemmaManager;

        // Update the dilemma text.
        if (titleText != null)
            titleText.text = dilemma.title;

        if (descriptionText != null)
            descriptionText.text = dilemma.description;

        if (choiceAText != null)
            choiceAText.text = dilemma.choiceA;

        if (choiceBText != null)
            choiceBText.text = dilemma.choiceB;

        // Only show choice C when it has text.
        bool hasChoiceC =
            !string.IsNullOrWhiteSpace(dilemma.choiceC);

        if (choiceCButton != null)
            choiceCButton.gameObject.SetActive(hasChoiceC);

        if (hasChoiceC && choiceCText != null)
            choiceCText.text = dilemma.choiceC;

        // Pause gameplay, remembering the previous time scale.
        if (!pausedByDilemma)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByDilemma = true;
        }

        // Block interaction with other UI.
        if (inputBlocker != null)
            inputBlocker.SetActive(true);

        // Mark the dilemma as open so EnvironmentUI can lock the slider.
        IsDilemmaOpen = true;

        // Show the dilemma on top of the blocker.
        if (panel != null)
            panel.SetActive(true);

        // Ensure the dilemma panel is the top UI element
        // within its own Canvas.
        if (panel != null)
            panel.transform.SetAsLastSibling();
    }

    public void HideDilemma()
    {
        IsDilemmaOpen = false;

        if (panel != null)
            panel.SetActive(false);

        if (inputBlocker != null)
            inputBlocker.SetActive(false);

        // Resume gameplay.
        if (pausedByDilemma)
        {
            Time.timeScale = previousTimeScale;
            pausedByDilemma = false;
        }

        manager = null;
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

    private void OnDestroy()
    {
        // Avoid leaving the game paused if this object is destroyed.
        if (pausedByDilemma)
            Time.timeScale = previousTimeScale;
    }
}
