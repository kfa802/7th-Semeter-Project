using System.Text;
using UnityEngine;
using TMPro;

public class PandaMoodUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PandaSystem panda;

    [SerializeField]
    private TMP_Text moodText;


    [Header("Display")]
    [SerializeField]
    private bool showEmoji = true;


    private void Start()
    {
        if (panda == null)
        {
            Debug.LogError(
                "PandaMoodUI: PandaSystem is not assigned."
            );

            return;
        }

        if (moodText == null)
        {
            Debug.LogError(
                "PandaMoodUI: Mood Text is not assigned."
            );

            return;
        }

        UpdateMood();
    }


    private void Update()
    {
        if (panda == null || moodText == null)
            return;

        UpdateMood();
    }


    private void UpdateMood()
    {
        StringBuilder moods =
            new StringBuilder();


        if (panda.IsOverfed)
        {
            AddMood(
                moods,
                showEmoji ? "🤢 OVERFED" : "OVERFED"
            );
        }


        if (panda.IsVeryHungry)
        {
            AddMood(
                moods,
                showEmoji ? "🍃 VERY HUNGRY" : "VERY HUNGRY"
            );
        }
        else if (panda.IsHungry)
        {
            AddMood(
                moods,
                showEmoji ? "🍃 HUNGRY" : "HUNGRY"
            );
        }


        if (panda.IsThirsty)
        {
            AddMood(
                moods,
                showEmoji ? "💧 THIRSTY" : "THIRSTY"
            );
        }


        if (panda.IsHot)
        {
            AddMood(
                moods,
                showEmoji ? "🥵 HOT" : "HOT"
            );
        }


        if (panda.IsCold)
        {
            AddMood(
                moods,
                showEmoji ? "🥶 COLD" : "COLD"
            );
        }


        if (panda.IsStressed)
        {
            AddMood(
                moods,
                showEmoji ? "😰 STRESSED" : "STRESSED"
            );
        }


        if (moods.Length == 0)
        {
            moods.Append(
                showEmoji
                    ? "😊 HAPPY"
                    : "HAPPY"
            );
        }


        moodText.text =
            moods.ToString();
    }


    private void AddMood(
        StringBuilder moods,
        string mood)
    {
        if (moods.Length > 0)
        {
            moods.Append("\n");
        }

        moods.Append(mood);
    }
}