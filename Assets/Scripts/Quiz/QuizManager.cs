using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QuizManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DilemmaManager dilemmaManager;
    [SerializeField] private GameObject quizPage;
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("The Content object inside the Scroll View.")]
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("Holds the warning text and the End button. Always kept last.")]
    [SerializeField] private Transform footer;
    [SerializeField] private Button endButton;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private QuizQuestionBlock blockPrefab;
    [SerializeField] private QuizOptionButton optionPrefab;

    [Header("Behaviour")]
    [SerializeField] private bool requireAllAnswers = true;
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string missingTextDA = "Besvar alle spørgsmål (mangler: {0})";
    [SerializeField] private string missingTextEN = "Please answer all questions (missing: {0})";

    [Header("Questions (in this order)")]
    [SerializeField] private List<QuizQuestion> questions = new();

    private readonly List<QuizQuestionBlock> blocks = new();
    private float pageShownRealtime;
    private bool shown;
    private bool submitted;

    private bool English => PlayerPrefs.GetString("language", "da") == "en";

    private void Awake()
    {
        if (quizPage != null) quizPage.SetActive(false);
        if (warningText != null) warningText.text = "";
        endButton.onClick.AddListener(OnEnd);
    }

    /// <summary>Call this when the ending narration is finished.</summary>
    [ContextMenu("Show quiz (test)")]
    public void ShowQuiz()
    {
        if (shown) return;
        shown = true;

        foreach (QuizQuestionBlock old in blocks)
            if (old != null) Destroy(old.gameObject);
        blocks.Clear();

        int number = 1;

        foreach (QuizQuestion q in questions)
        {
            if (q == null || !IsApplicable(q)) continue;

            QuizQuestionBlock block = Instantiate(blockPrefab, contentRoot);
            block.Setup(q, number, English, optionPrefab, OnAnswerChanged);
            blocks.Add(block);
            number++;
        }

        if (footer != null)
            footer.SetAsLastSibling();

        if (blocks.Count == 0)
        {
            EndAndReturn();
            return;
        }

        quizPage.SetActive(true);
        quizPage.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        scrollRect.verticalNormalizedPosition = 1f;

        pageShownRealtime = Time.realtimeSinceStartup;
        TelemetryManager.Instance?.QuizPageShown();
    }

    /// <summary>Skips the quiz: saves the data, ends the session and
    /// returns to the start screen. Used for the usability test.</summary>
    public void SkipQuizAndReturn()
    {
        if (submitted) return;
        submitted = true;

        EndAndReturn();
    }

    private bool IsApplicable(QuizQuestion q)
    {
        if (dilemmaManager == null)
            return q.condition == QuizCondition.Everyone;

        switch (q.condition)
        {
            case QuizCondition.Everyone:
                return true;

            case QuizCondition.DilemmaChoice:
                return dilemmaManager.TryGetChoice(q.dilemma, out int choice) &&
                       ChoiceMatches(q.requiredChoice, choice);

            case QuizCondition.DilemmaReached:
                return dilemmaManager.HasResolved(q.dilemma);

            case QuizCondition.EndingReached:
                return q.ending != null &&
                       dilemmaManager.ReachedEnding == q.ending;

            case QuizCondition.GameEventTriggered:
                return q.gameEvent != null &&
                       dilemmaManager.HasExecuted(q.gameEvent);
        }

        return false;
    }

    private static bool ChoiceMatches(PrerequisiteChoice required, int choice)
    {
        return required == PrerequisiteChoice.Either ||
               (required == PrerequisiteChoice.ChoiceA && choice == 0) ||
               (required == PrerequisiteChoice.ChoiceB && choice == 1) ||
               (required == PrerequisiteChoice.ChoiceC && choice == 2);
    }

    private void OnAnswerChanged()
    {
        if (warningText != null)
            warningText.text = "";
    }

    private void OnEnd()
    {
        if (submitted) return;

        List<string> missing = new();

        for (int i = 0; i < blocks.Count; i++)
            if (!blocks[i].HasAnswer)
                missing.Add((i + 1).ToString());

        if (requireAllAnswers && missing.Count > 0)
        {
            if (warningText != null)
            {
                string template = English ? missingTextEN : missingTextDA;
                warningText.text = string.Format(template, string.Join(", ", missing));
            }

            return;
        }

        submitted = true;
        endButton.interactable = false;

        TelemetryManager t = TelemetryManager.Instance;

        if (t != null)
        {
            foreach (QuizQuestionBlock block in blocks)
            {
                if (!block.HasAnswer) continue;

                float seconds = block.LastChangeRealtime - pageShownRealtime;

                t.QuizAnswerRows(
                    block.Question.id,
                    block.GetSelectedOptions(),
                    block.GetCorrect(),
                    seconds);
            }

            t.QuizSubmitted();
        }

        EndAndReturn();
    }

    private void EndAndReturn()
    {
        // Writes both CSV files, then returns to the start screen.
        TelemetryManager.Instance?.EndSession("quiz submitted");

        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }
}