
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

    private void Start()
    {
        if (pollutionSlider != null)
        {
            pollutionSlider.minValue = 0f;
            pollutionSlider.maxValue = 1f;
            pollutionSlider.interactable = false;
        }

        // Start hidden if pollution is zero.
        if (pollutionPanel != null)
            pollutionPanel.SetActive(false);
    }

    private void Update()
    {
        if (environment == null || pollutionPanel == null)
            return;

        float pollution = Mathf.Clamp01(environment.Pollution);

        // Show the panel only when pollution is present.
        bool showUI = pollution > visibilityThreshold;

        if (pollutionPanel.activeSelf != showUI)
            pollutionPanel.SetActive(showUI);

        if (pollutionSlider != null)
            pollutionSlider.value = pollution;

        if (pollutionText != null)
        {
            pollutionText.text =
                "Pollution: " + Mathf.RoundToInt(pollution * 100f) + "%";
        }
    }
}