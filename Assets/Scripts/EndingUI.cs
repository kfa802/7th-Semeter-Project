using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Continue button (fallback)")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private string continueTextDA = "Fortsæt";
    [SerializeField] private string continueTextEN = "Continue";

    [Header("Quiz")]
    [SerializeField] private QuizManager quizManager;
    [Tooltip("Seconds after the last narration clip before the quiz opens.")]
    [SerializeField, Min(0f)] private float delayBeforeQuiz = 1.5f;
    [Tooltip("On: the Continue button is hidden while the ending is narrated and the quiz opens by itself. " +
             "If the ending has no narration, the button is shown so nobody gets stuck.")]
    [SerializeField] private bool hideContinueWhenNarrated = true;

    [Header("Narration")]
    [Tooltip("Use your boosted 'Narration' mixer group here.")]
    [SerializeField] private AudioMixerGroup narrationMixerGroup;
    [SerializeField, Range(0f, 1f)] private float narrationVolume = 1f;
    [SerializeField, Min(0f)] private float delayBeforeFirstClip = 0.5f;
    [SerializeField, Min(0f)] private float gapBetweenClips = 0.5f;

    private AudioSource source;
    private Coroutine narrationRoutine;

    private bool English => PlayerPrefs.GetString("language", "da") == "en";

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnDisable()
    {
        StopNarration();
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

        StopNarration();
        List<AudioClip> clips = ending.GetNarrations(english);
        bool narrated = clips.Count > 0 && gameObject.activeInHierarchy;

        // Narrated endings open the quiz by themselves. Without narration
        // the Continue button stays as the way forward.
        if (continueButton != null)
            continueButton.gameObject.SetActive(!(narrated && hideContinueWhenNarrated));

        if (narrated)
            narrationRoutine = StartCoroutine(PlayNarrations(clips));
        else
            Debug.LogWarning("EndingUI: '" + ending.name + "' has no narration for this language. " +
                             "The Continue button is shown instead of opening the quiz automatically.");
    }

    private IEnumerator PlayNarrations(List<AudioClip> clips)
    {
        if (delayBeforeFirstClip > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFirstClip);

        AudioSource src = GetSource();

        for (int i = 0; i < clips.Count; i++)
        {
            src.clip = clips[i];
            src.volume = narrationVolume;
            src.Play();

            float played = 0f;
            while (played < clips[i].length + 1f && (src.isPlaying || played < 0.1f))
            {
                played += Time.unscaledDeltaTime;
                yield return null;
            }

            if (i < clips.Count - 1 && gapBetweenClips > 0f)
                yield return new WaitForSecondsRealtime(gapBetweenClips);
        }

        if (delayBeforeQuiz > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeQuiz);

        narrationRoutine = null;

        if (quizManager != null)
        {
            quizManager.ShowQuiz();
        }
        else
        {
            Debug.LogWarning("EndingUI: No QuizManager assigned. Showing the Continue button.");
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);
        }
    }

    private AudioSource GetSource()
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        source.outputAudioMixerGroup = narrationMixerGroup;
        return source;
    }

    private void StopNarration()
    {
        if (narrationRoutine != null)
        {
            StopCoroutine(narrationRoutine);
            narrationRoutine = null;
        }

        if (source != null && source.isPlaying)
            source.Stop();
    }
}