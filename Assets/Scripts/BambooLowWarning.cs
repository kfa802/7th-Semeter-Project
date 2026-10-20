using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulses a red outline around a resource bar (bamboo, water or health) when it is low.
/// Put one on each bar's root object and pick the resource to watch.
/// </summary>
public class BambooLowWarning : MonoBehaviour
{
    public enum WatchedResource
    {
        Bamboo,
        Water,
        Health
    }

    [Header("Source")]
    [SerializeField] private EnvironmentUI environmentUI;
    [SerializeField] private WatchedResource resource = WatchedResource.Bamboo;

    [Header("Warning Visuals")]
    [Tooltip("Red outline images placed BEHIND the bar and the icon circle.")]
    [SerializeField] private Graphic[] outlineGraphics;

    [Tooltip("Optional: the whole bamboo bar, gently scaled with the pulse.")]
    [SerializeField] private RectTransform pulseTarget;

    [SerializeField] private Color warningColor = new Color(0.85f, 0.15f, 0.12f, 1f);

    [Header("Thresholds (%)")]
    [Tooltip("Warning turns ON at or below this percentage.")]
    [SerializeField, Range(0f, 100f)] private float lowThreshold = 25f;

    [Tooltip("Warning turns OFF at or above this percentage (prevents flicker).")]
    [SerializeField, Range(0f, 100f)] private float recoverThreshold = 30f;

    [Header("Pulse")]
    [SerializeField] private float pulsesPerSecond = 1.2f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [Tooltip("How much the bar grows on each pulse. 0 = no scaling.")]
    [SerializeField] private float scaleAmount = 0.04f;
    [Tooltip("How fast the warning fades in and out.")]
    [SerializeField] private float fadeSpeed = 4f;
    [Tooltip("Pulse faster as bamboo gets closer to zero. Ignored when Sync Pulse is on.")]
    [SerializeField] private bool urgencyScaling = true;
    [Tooltip("All warnings with this ON pulse in step with each other " +
             "(they must use the same Pulses Per Second).")]
    [SerializeField] private bool syncPulse = true;

    private bool isLow;
    private float intensity;   // 0 = hidden, 1 = fully visible
    private float phase;
    private Vector3 baseScale = Vector3.one;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Awake()
    {
        if (pulseTarget != null)
            baseScale = pulseTarget.localScale;

        SetOutlineAlpha(0f);
    }

    private void OnDisable()
    {
        isLow = false;
        intensity = 0f;
        phase = 0f;

        SetOutlineAlpha(0f);

        if (pulseTarget != null)
            pulseTarget.localScale = baseScale;
    }

    private void Update()
    {
        if (environmentUI == null)
            return;

        float percent;
        bool hasData;

        switch (resource)
        {
            case WatchedResource.Water:
                percent = environmentUI.WaterPercent;
                hasData = environmentUI.HasActiveZone;
                break;

            case WatchedResource.Health:
                percent = environmentUI.HealthPercent;
                hasData = environmentUI.HasPanda;
                break;

            default:
                percent = environmentUI.BambooPercent;
                hasData = environmentUI.HasBambooZone;
                break;
        }

        // Decide whether we are in the "low" state (with hysteresis).
        if (!hasData)
            isLow = false;
        else if (!isLow && percent <= lowThreshold)
            isLow = true;
        else if (isLow && percent >= recoverThreshold)
            isLow = false;

        // Unscaled time so the warning still pulses if the game is paused
        // (e.g. while a dilemma is open).
        float dt = Time.unscaledDeltaTime;

        intensity = Mathf.MoveTowards(
            intensity,
            isLow ? 1f : 0f,
            fadeSpeed * dt
        );

        if (intensity <= 0f)
        {
            phase = 0f;
            SetOutlineAlpha(0f);

            if (pulseTarget != null)
                pulseTarget.localScale = baseScale;

            return;
        }

        if (syncPulse)
        {
            // Shared clock: every synced warning gets the same phase,
            // so bars that are low at the same time pulse together.
            phase = Mathf.Repeat(Time.unscaledTime * pulsesPerSecond, 1f);
        }
        else
        {
            // Pulse faster the emptier the zone is.
            float speed = pulsesPerSecond;

            if (urgencyScaling && isLow && lowThreshold > 0f)
            {
                float urgency = 1f - Mathf.Clamp01(percent / lowThreshold);
                speed *= Mathf.Lerp(1f, 2f, urgency);
            }

            phase = Mathf.Repeat(phase + speed * dt, 1f);
        }

        // 0..1 wave, starting at 0 so the first pulse eases in.
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave) * intensity;
        SetOutlineAlpha(alpha);

        if (pulseTarget != null)
        {
            pulseTarget.localScale =
                baseScale * (1f + scaleAmount * wave * intensity);
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private void SetOutlineAlpha(float alpha)
    {
        if (outlineGraphics == null)
            return;

        foreach (Graphic graphic in outlineGraphics)
        {
            if (graphic == null)
                continue;

            Color c = warningColor;
            c.a *= alpha;

            graphic.color = c;
            graphic.enabled = alpha > 0.001f;
        }
    }
}