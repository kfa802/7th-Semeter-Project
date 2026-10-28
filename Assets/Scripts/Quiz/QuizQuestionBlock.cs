using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizQuestionBlock : MonoBehaviour
{
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Transform verticalOptions;
    [SerializeField] private Transform horizontalOptions;

    [Tooltip("Height of each option in a horizontal row (the smiley scale).")]
    [SerializeField] private float horizontalOptionHeight = 200f;

    private readonly HashSet<int> selected = new();
    private readonly List<QuizOptionButton> buttons = new();
    private QuizQuestion question;
    private Action onChanged;

    public QuizQuestion Question => question;
    public bool HasAnswer => selected.Count > 0;
    public float LastChangeRealtime { get; private set; }

    public static string Pick(string da, string en, bool english)
    {
        string wanted = english ? en : da;
        return string.IsNullOrWhiteSpace(wanted) ? (english ? da : en) : wanted;
    }

    public void Setup(QuizQuestion q, int number, bool english,
                      QuizOptionButton optionPrefab, Action changed)
    {
        question = q;
        onChanged = changed;

        questionText.text = number + ". " + Pick(q.textDA, q.textEN, english);

        verticalOptions.gameObject.SetActive(!q.horizontalLayout);
        horizontalOptions.gameObject.SetActive(q.horizontalLayout);

        Transform container = q.horizontalLayout ? horizontalOptions : verticalOptions;

        for (int i = 0; i < q.options.Count; i++)
        {
            int optionIndex = i;
            QuizOption option = q.options[i];

            string text = Pick(option.textDA, option.textEN, english);
            if (q.showLetters)
                text = (char)('A' + i) + ". " + text;

            QuizOptionButton button = Instantiate(optionPrefab, container);

            if (q.horizontalLayout)
            {
                LayoutElement element = button.GetComponent<LayoutElement>();
                if (element != null) element.minHeight = horizontalOptionHeight;
            }

            button.Setup(text, option.icon, () => OnOptionClicked(optionIndex));
            buttons.Add(button);
        }
    }

    private void OnOptionClicked(int i)
    {
        if (!question.multiSelect)
        {
            selected.Clear();
            selected.Add(i);
        }
        else if (selected.Contains(i))
        {
            selected.Remove(i);
        }
        else
        {
            if (question.options[i].exclusive)
                selected.Clear();
            else
                selected.RemoveWhere(j => question.options[j].exclusive);

            selected.Add(i);
        }

        for (int b = 0; b < buttons.Count; b++)
            buttons[b].SetSelected(selected.Contains(b));

        LastChangeRealtime = Time.realtimeSinceStartup;
        onChanged?.Invoke();
    }

    /// <summary>One entry per chosen option: the letter (or number) and
    /// whether that option is a correct one ("1", "0" or "").</summary>
    public List<(string answer, string optionCorrect)> GetSelectedOptions()
    {
        bool anyCorrect = false;
        foreach (QuizOption option in question.options)
            if (option.correct) anyCorrect = true;

        List<int> chosen = new(selected);
        chosen.Sort();

        List<(string, string)> result = new();

        foreach (int i in chosen)
        {
            string answer = question.logAsNumber
                ? (i + 1).ToString()
                : ((char)('A' + i)).ToString();

            string correct = anyCorrect
                ? (question.options[i].correct ? "1" : "0")
                : "";

            result.Add((answer, correct));
        }

        return result;
    }

    /// <summary>"1" correct, "0" wrong, "" for opinion questions.</summary>
    public string GetCorrect()
    {
        bool hasCorrect = false;
        bool allMatch = true;

        for (int i = 0; i < question.options.Count; i++)
        {
            if (question.options[i].correct) hasCorrect = true;
            if (question.options[i].correct != selected.Contains(i)) allMatch = false;
        }

        return hasCorrect ? (allMatch ? "1" : "0") : "";
    }
}