
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PollutionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pollutionPanel;
    [SerializeField] private Slider pollutionSlider;
    [SerializeField] private TMP_Text pollutionText;

    [Header("Environment")]
    [SerializeField] private EnvironmentSystem environment;

    [Header("Visibility")]
    [SerializeField, Range(0f, 1f)]
    private float visibilityThreshold = 0.01f;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float fillSpeed = 0.5f;

    private float displayedPollution = 0f;
    private float targetPollution = 0f;

    private void Start()
    {
        if (pollutionSlider != null)
        {
            pollutionSlider.minValue = 0f;
            pollutionSlider.maxValue = 1f;
            pollutionSlider.interactable = false;
            pollutionSlider.value = 0f;
        }

        if (pollutionText != null)
            pollutionText.text = "Pollution: 0%";

        if (pollutionPanel != null)
            pollutionPanel.SetActive(false);
    }

    private void Update()
    {
        if (environment == null || pollutionPanel == null)
            return;

        // Get the actual pollution level.
        targetPollution = Mathf.Clamp01(environment.Pollution);

        // Show the panel when pollution is present.
        bool showUI = targetPollution > visibilityThreshold;

        if (pollutionPanel.activeSelf != showUI)
            pollutionPanel.SetActive(showUI);

        // Smoothly animate the displayed value.
        displayedPollution = Mathf.MoveTowards(
            displayedPollution,
            targetPollution,
            fillSpeed * Time.deltaTime
        );

        if (pollutionSlider != null)
            pollutionSlider.value = displayedPollution;

        if (pollutionText != null)
        {
            pollutionText.text =
                "Pollution: " +
                Mathf.RoundToInt(displayedPollution * 100f) + "%";
        }
    }
}