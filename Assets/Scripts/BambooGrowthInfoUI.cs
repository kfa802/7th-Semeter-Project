
using System.Collections;
using TMPro;
using UnityEngine;

public class BambooGrowthInfoUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text messageText;

    [Header("Slide Settings")]
    [SerializeField] private float slideDistance = 450f;
    [SerializeField] private float slideSpeed = 700f;

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private Coroutine slideCoroutine;

    private string currentMessage = "";
    private bool isVisible;

    private void Start()
    {
        if (panel == null || messageText == null)
        {
            Debug.LogError(
                "BambooGrowthInfoUI: Assign Panel and Message Text.",
                this
            );

            enabled = false;
            return;
        }

        shownPosition = panel.anchoredPosition;
        hiddenPosition =
            shownPosition + Vector2.right * slideDistance;

        panel.anchoredPosition = hiddenPosition;
        panel.gameObject.SetActive(true);

        CheckGrowthConditions();
    }

    private void Update()
    {
        CheckGrowthConditions();
    }

    private void CheckGrowthConditions()
    {
        if (EnvironmentSystem.Instance == null)
            return;

        float water = EnvironmentSystem.Instance.Water;
        float temperature = Mathf.Clamp01(
            EnvironmentSystem.Instance.TemperatureGrowthFactor
        );

        float waterFactor = 0f;

        if (BambooManager.Instance != null)
        {
            waterFactor = Mathf.Clamp01(
                BambooManager.Instance.GetWaterGrowthMultiplier()
            );
        }

        // Growth factors are multiplied because both affect growth time.
        float totalGrowth = temperature * waterFactor;

        string message;

        if (water <= 0f || waterFactor <= 0f)
        {
            message = "BAMBOO GROWTH: PAUSED\nNo water available.";
        }
        else if (temperature <= 0f)
        {
            message = "BAMBOO GROWTH: PAUSED\nTemperature prevents growth.";
        }
        else
        {
            int totalPercent = Mathf.RoundToInt(totalGrowth * 100f);
            int temperaturePercent =
                Mathf.RoundToInt(temperature * 100f);
            int waterPercent =
                Mathf.RoundToInt(waterFactor * 100f);

            string explanation = "";

            if (temperature < 0.999f && waterFactor < 0.999f)
            {
                explanation = "Slowed by water and temperature.";
            }
            else if (temperature < 0.999f)
            {
                explanation = "Slowed by temperature.";
            }
            else if (waterFactor < 0.999f)
            {
                explanation = "Slowed by low water.";
            }
            else
            {
                explanation = "Conditions are ideal for growth.";
            }

            message =
                $"BAMBOO GROWTH: {totalPercent}% SPEED\n" +
                $"Temperature: {temperaturePercent}%  |  " +
                $"Water: {waterPercent}%\n" +
                explanation;
        }

        if (message == currentMessage)
            return;

        currentMessage = message;
        messageText.text = message;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (isVisible == visible)
            return;

        isVisible = visible;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        slideCoroutine = StartCoroutine(SlidePanel());
    }

    private IEnumerator SlidePanel()
    {
        Vector2 target = isVisible
            ? shownPosition
            : hiddenPosition;

        while (Vector2.Distance(
            panel.anchoredPosition, target) > 0.5f)
        {
            panel.anchoredPosition = Vector2.MoveTowards(
                panel.anchoredPosition,
                target,
                slideSpeed * Time.deltaTime
            );

            yield return null;
        }

        panel.anchoredPosition = target;
        slideCoroutine = null;
    }
}