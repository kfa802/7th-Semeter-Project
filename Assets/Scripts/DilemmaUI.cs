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

    [Header("Hide Background UI")]
    [Tooltip("UI that is hidden while a dilemma is open (e.g. TempSlider, " +
             "BambooPanel, PollutionPanel, MenuButton). Adding a parent hides " +
             "everything inside it. Do NOT add the dilemma panel itself.")]
    [SerializeField] private GameObject[] hideWhileOpen;

    private CanvasGroup[] hiddenGroups;
    private float[] hiddenAlphas;

    [Header("Choice Pop-Up")]
    [Tooltip("Seconds a choice button takes to pop up.")]
    [SerializeField, Min(0.05f)] private float choicePopTime = 0.35f;

    [Tooltip("How much it overshoots when popping up (0 = no bounce).")]
    [SerializeField, Range(0f, 3f)] private float choicePopBounce = 1.5f;

    private Vector3[] choiceBaseScales;
    private bool[] choiceShown = new bool[3];

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

        // Update the dilemma text (Danish or English, as chosen in the start menu).
        if (titleText != null)
            titleText.text = dilemma.Title;

        if (descriptionText != null)
            descriptionText.text = dilemma.Description;

        if (choiceAText != null)
            choiceAText.text = dilemma.ChoiceA;

        if (choiceBText != null)
            choiceBText.text = dilemma.ChoiceB;

        // Only show choice C when it has text.
        bool hasChoiceC =
            !string.IsNullOrWhiteSpace(dilemma.ChoiceC);

        if (choiceCButton != null)
            choiceCButton.gameObject.SetActive(hasChoiceC);

        if (hasChoiceC && choiceCText != null)
            choiceCText.text = dilemma.ChoiceC;

        // Pause gameplay, remembering the previous time scale.
        if (!pausedByDilemma)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByDilemma = true;
        }

        // Block interaction with other UI: put the blocker on top of ALL
        // the other UI (the dilemma panel goes on top of it below).
        if (inputBlocker != null)
        {
            inputBlocker.SetActive(true);
            inputBlocker.transform.SetAsLastSibling();
        }

        SetBackgroundHidden(true);

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

    /// <summary>
    /// Closes the dilemma and resumes the game. holdBackground = true keeps the
    /// background UI hidden until ReleaseHold() is called (used when the choice's
    /// narration is important).
    /// </summary>
    public void HideDilemma(bool holdBackground = false)
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

        holding = holdBackground;

        if (!holdBackground)
            SetBackgroundHidden(false);
    }

    private bool holding;

    /// <summary>Shows the background UI again.</summary>
    public void ReleaseHold()
    {
        if (!holding)
            return;

        holding = false;
        SetBackgroundHidden(false);
    }

    public bool IsHolding => holding;

    // Hides the background UI with a CanvasGroup (alpha 0, no clicks) instead
    // of turning it off, so the scripts on it keep working.
    private void SetBackgroundHidden(bool hide)
    {
        if (hideWhileOpen == null)
            return;

        if (hiddenGroups == null || hiddenGroups.Length != hideWhileOpen.Length)
        {
            hiddenGroups = new CanvasGroup[hideWhileOpen.Length];
            hiddenAlphas = new float[hideWhileOpen.Length];
        }

        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            GameObject ui = hideWhileOpen[i];

            if (ui == null)
                continue;

            if (hiddenGroups[i] == null)
            {
                hiddenGroups[i] = ui.GetComponent<CanvasGroup>();

                if (hiddenGroups[i] == null)
                    hiddenGroups[i] = ui.AddComponent<CanvasGroup>();
            }

            CanvasGroup g = hiddenGroups[i];

            if (hide)
            {
                hiddenAlphas[i] = g.alpha;
                g.alpha = 0f;
                g.interactable = false;
                g.blocksRaycasts = false;
            }
            else
            {
                g.alpha = hiddenAlphas[i] > 0f ? hiddenAlphas[i] : 1f;
                g.interactable = true;
                g.blocksRaycasts = true;
            }
        }
    }

    // =========================================================
    // CHOICE BUTTONS POPPING UP ONE BY ONE
    // =========================================================

    private Button ChoiceButton(int i) =>
        i == 0 ? choiceAButton : i == 1 ? choiceBButton : choiceCButton;

    /// <summary>Hides the choice buttons that should pop up later.</summary>
    public void HideChoicesUntilTheirTime(bool[] hide)
    {
        if (choiceBaseScales == null)
        {
            choiceBaseScales = new Vector3[3];
            for (int i = 0; i < 3; i++)
                if (ChoiceButton(i) != null)
                    choiceBaseScales[i] = ChoiceButton(i).transform.localScale;
        }

        for (int i = 0; i < 3; i++)
        {
            Button b = ChoiceButton(i);
            if (b == null)
                continue;

            choiceShown[i] = !hide[i];
            b.transform.localScale = hide[i] ? Vector3.zero : choiceBaseScales[i];
        }
    }

    /// <summary>Pops choice 0/1/2 up (if it isn't already shown).</summary>
    public void RevealChoice(int i)
    {
        Button b = ChoiceButton(i);

        if (b == null || choiceShown[i] || !b.gameObject.activeInHierarchy)
            return;

        choiceShown[i] = true;
        StartCoroutine(PopIn(b.transform, choiceBaseScales != null ? choiceBaseScales[i] : Vector3.one));
    }

    private System.Collections.IEnumerator PopIn(Transform t, Vector3 baseScale)
    {
        float time = 0f;

        while (time < 1f)
        {
            // Unscaled: the game is paused while a dilemma is open.
            time = Mathf.MoveTowards(time, 1f, Time.unscaledDeltaTime / choicePopTime);

            float u = time - 1f;
            float size = 1f + (choicePopBounce + 1f) * u * u * u + choicePopBounce * u * u;

            t.localScale = baseScale * Mathf.Max(0f, size);
            yield return null;
        }

        t.localScale = baseScale;
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