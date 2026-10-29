using UnityEngine;
using TMPro;

/// <summary>
/// Short pop-up message next to a resource bar that explains WHY the bar
/// is changing (e.g. "Too hot: water evaporating").
/// Put one next to the water bar, one next to the bamboo bar and one
/// under the health bar, and pick the resource to explain.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ResourceFeedback : MonoBehaviour
{
    public enum WatchedResource
    {
        Water,
        Bamboo,
        Health
    }

    [Header("Source")]
    [SerializeField] private WatchedResource resource = WatchedResource.Water;
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private PandaSystem panda;

    [Header("UI")]
    [SerializeField] private TMP_Text messageText;

    [Header("Colors")]
    [SerializeField] private Color goodColor = new Color(0.25f, 0.65f, 0.25f, 1f);
    [SerializeField] private Color badColor = new Color(0.85f, 0.2f, 0.15f, 1f);

    [Header("Arrows")]
    [Tooltip("If these show as squares, your font lacks the glyphs: " +
             "use another font or clear these fields.")]
    [SerializeField] private string goodPrefix = "▲ ";
    [SerializeField] private string badPrefix = "▼ ";

    [Header("Timing (seconds)")]
    [Tooltip("How long a message stays fully visible.")]
    [SerializeField] private float showDuration = 3f;
    [SerializeField] private float fadeDuration = 0.3f;
    [Tooltip("A situation must last this long before it is shown " +
             "(stops flicker while the slider is dragged).")]
    [SerializeField] private float stableTime = 0.5f;
    [Tooltip("Show the same message again after this long if it still applies. " +
             "0 = never repeat.")]
    [SerializeField] private float repeatInterval = 12f;

    [Header("Water Messages")]
    [SerializeField] private string waterEvaporating = "Too hot: water evaporating";
    [SerializeField] private string waterFreezing = "Freezing: water turning to ice";
    [SerializeField] private string waterRefillingSlow = "Cool: water refills slowly";
    [SerializeField] private string waterRefilling = "Water refilling";

    [Header("Bamboo Messages")]
    [SerializeField] private string bambooTooCold = "Too cold: bamboo grows slowly";
    [SerializeField] private string bambooTooHot = "Too hot: bamboo grows slowly";
    [SerializeField] private string bambooIdeal = "Perfect temperature: bamboo grows fast";

    [Header("Health Messages")]
    [SerializeField] private string healthHeatstroke = "Heatstroke: losing health";
    [SerializeField] private string healthThirsty = "Thirsty: losing health";
    [SerializeField] private string healthHungry = "Hungry: losing health";
    [Tooltip("{0} is replaced with the amount.")]
    [SerializeField] private string healthFed = "Fed: +{0} health";
    // OVERFEEDING DISABLED
    // [Tooltip("{0} is replaced with the amount.")]
    // [SerializeField] private string healthOverfed = "Overfed: -{0} health";

    private CanvasGroup canvasGroup;

    // The situation currently on screen (or last shown).
    private string shownMessage;
    private float shownAt = -999f;

    // The situation waiting to become stable before it is shown.
    private string candidateMessage;
    private float candidateSince;

    // One-off messages (fed / overfed) shown on top of the state messages.
    private float eventUntil = -999f;

    private float alpha;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (environment == null)
            environment = EnvironmentSystem.Instance;
    }

    private void OnEnable()
    {
        if (panda != null && resource == WatchedResource.Health)
        {
            panda.Fed += OnFed;
            // panda.Overfed += OnOverfed;   // OVERFEEDING DISABLED
        }
    }

    private void OnDisable()
    {
        if (panda != null)
        {
            panda.Fed -= OnFed;
            // panda.Overfed -= OnOverfed;   // OVERFEEDING DISABLED
        }

        alpha = 0f;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (environment == null)
            environment = EnvironmentSystem.Instance;

        // Unscaled time so it also works if the game is paused.
        float now = Time.unscaledTime;

        // One-off messages win until they have finished.
        if (now >= eventUntil)
            UpdateStateMessage(now);

        UpdateFade(now);
    }

    // =====================================================
    // STATE MESSAGES
    // =====================================================

    private void UpdateStateMessage(float now)
    {
        GetCurrentState(out string message, out bool good);

        // Wait until the situation has been the same for a moment.
        if (message != candidateMessage)
        {
            candidateMessage = message;
            candidateSince = now;
            return;
        }

        if (now - candidateSince < stableTime)
            return;

        if (string.IsNullOrEmpty(message))
        {
            shownMessage = null;
            return;
        }

        bool isNew = message != shownMessage;

        bool shouldRepeat =
            repeatInterval > 0f &&
            now - shownAt >= repeatInterval;

        if (isNew || shouldRepeat)
            Show(message, good, now, showDuration);
    }

    private void GetCurrentState(out string message, out bool good)
    {
        message = null;
        good = false;

        if (environment == null)
            return;

        switch (resource)
        {
            case WatchedResource.Water:
                GetWaterState(out message, out good);
                break;

            case WatchedResource.Bamboo:
                GetBambooState(out message, out good);
                break;

            case WatchedResource.Health:
                GetHealthState(out message, out good);
                break;
        }
    }

    private void GetWaterState(out string message, out bool good)
    {
        message = null;
        good = false;

        switch (environment.WaterTrend)
        {
            case EnvironmentSystem.WaterTrendType.Evaporating:
                message = waterEvaporating;
                break;

            case EnvironmentSystem.WaterTrendType.Freezing:
                message = waterFreezing;
                break;

            case EnvironmentSystem.WaterTrendType.RefillingSlow:
                message = waterRefillingSlow;
                good = true;
                break;

            case EnvironmentSystem.WaterTrendType.Refilling:
                message = waterRefilling;
                good = true;
                break;
        }
    }

    private void GetBambooState(out string message, out bool good)
    {
        message = null;
        good = false;

        EcosystemZone zone = environment.ActiveZone;
        BambooManager manager = BambooManager.Instance;

        // No zone (travelling) or no manager: nothing is growing.
        if (zone == null || manager == null)
            return;

        // All growth spots are full: nothing is growing, so say nothing.
        if (manager.GetCurrentCountForZone(zone) >=
            manager.GetCapacityForZone(zone))
            return;

        switch (environment.GrowthCondition)
        {
            case EnvironmentSystem.GrowthConditionType.TooCold:
                message = bambooTooCold;
                break;

            case EnvironmentSystem.GrowthConditionType.TooHot:
                message = bambooTooHot;
                break;

            default:
                message = bambooIdeal;
                good = true;
                break;
        }
    }

    private void GetHealthState(out string message, out bool good)
    {
        message = null;
        good = false;

        if (panda == null)
            return;

        // One message at a time, most serious first.
        if (panda.IsInExtremeHeat)
            message = healthHeatstroke;
        else if (panda.IsThirsty)
            message = healthThirsty;
        else if (panda.IsHungry)
            message = healthHungry;
    }

    // =====================================================
    // EVENT MESSAGES
    // =====================================================

    private void OnFed(float amount)
    {
        ShowEvent(string.Format(healthFed, amount.ToString("0")), true);
    }

    // OVERFEEDING DISABLED
    // private void OnOverfed(float amount)
    // {
    //     ShowEvent(string.Format(healthOverfed, amount.ToString("0")), false);
    // }

    private void ShowEvent(string message, bool good)
    {
        float now = Time.unscaledTime;

        Show(message, good, now, showDuration);
        eventUntil = now + showDuration + fadeDuration;

        // Let the current state message show again right after the event.
        shownMessage = null;
        candidateMessage = null;
    }

    // =====================================================
    // DISPLAY
    // =====================================================

    private float visibleUntil = -999f;

    private void Show(string message, bool good, float now, float duration)
    {
        shownMessage = message;
        shownAt = now;
        visibleUntil = now + duration;

        if (messageText != null)
        {
            messageText.text = (good ? goodPrefix : badPrefix) + message;
            messageText.color = good ? goodColor : badColor;
        }
    }

    private void UpdateFade(float now)
    {
        float target = now < visibleUntil ? 1f : 0f;
        float speed = fadeDuration > 0f ? 1f / fadeDuration : 1000f;

        alpha = Mathf.MoveTowards(alpha, target, speed * Time.unscaledDeltaTime);
        canvasGroup.alpha = alpha;
    }
}