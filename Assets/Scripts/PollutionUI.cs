using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PollutionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pollutionPanel;
    [SerializeField] private Slider pollutionSlider;
    [SerializeField] private TMP_Text pollutionText;

    [Header("Gauge Needle (optional)")]
    [Tooltip("The needle image. Its PIVOT must be at the round end it turns around.")]
    [SerializeField] private RectTransform needle;

    [Tooltip("Z rotation of the needle at 0% (pointing at the green end).")]
    [SerializeField] private float angleAtZero = 120f;

    [Tooltip("Z rotation of the needle at 100% (pointing at the other end).")]
    [SerializeField] private float angleAtFull = -120f;

    [Header("Environment")]
    [SerializeField] private EnvironmentSystem environment;

    [Header("Text")]
    [Tooltip("Optional text in front of the percentage. Empty = just the number, e.g. '35%'.")]
    [SerializeField] private string prefix = "";

    [Header("Visibility")]
    [Tooltip("On: always visible, also at 0%. Off: only shown when there is pollution.")]
    [SerializeField] private bool alwaysVisible = true;

    [SerializeField, Range(0f, 1f)]
    private float visibilityThreshold = 0.01f;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float fillSpeed = 0.5f;

    private float displayedPollution = 0f;
    private float targetPollution = 0f;

    private void Start()
    {
        if (environment == null)
            environment = EnvironmentSystem.Instance;

        if (pollutionSlider != null)
        {
            pollutionSlider.minValue = 0f;
            pollutionSlider.maxValue = 1f;
            pollutionSlider.interactable = false;
        }

        // Start at the current value instead of animating up from 0.
        displayedPollution = environment != null ? Mathf.Clamp01(environment.Pollution) : 0f;

        if (pollutionSlider != null)
            pollutionSlider.value = displayedPollution;

        UpdateNeedle();
        UpdateText();

        if (pollutionPanel != null)
            pollutionPanel.SetActive(alwaysVisible || displayedPollution > visibilityThreshold);
    }

    private void Update()
    {
        if (environment == null)
        {
            environment = EnvironmentSystem.Instance;
            if (environment == null)
                return;
        }

        // Get the actual pollution level.
        targetPollution = Mathf.Clamp01(environment.Pollution);

        if (pollutionPanel != null)
        {
            bool showUI = alwaysVisible || targetPollution > visibilityThreshold;

            if (pollutionPanel.activeSelf != showUI)
                pollutionPanel.SetActive(showUI);
        }

        // Smoothly animate the displayed value.
        displayedPollution = Mathf.MoveTowards(
            displayedPollution,
            targetPollution,
            fillSpeed * Time.deltaTime
        );

        if (pollutionSlider != null)
            pollutionSlider.value = displayedPollution;

        UpdateNeedle();
        UpdateText();
    }

    private void UpdateNeedle()
    {
        if (needle == null)
            return;

        float angle = Mathf.Lerp(angleAtZero, angleAtFull, displayedPollution);
        needle.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private void UpdateText()
    {
        if (pollutionText != null)
            pollutionText.text = prefix + Mathf.RoundToInt(displayedPollution * 100f) + "%";
    }
}