using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ultra-short messages next to the thermometer that explain what the
/// temperature is doing, e.g. "Above 25°C water evaporates".
///
/// Messages ONLY show when they matter (one line each, max two):
///  - WATER: only when there is little or no water. Says whether it is
///    draining (too hot / freezing), refilling slowly, or refilling (green).
///  - BAMBOO: only when bamboo is low (or the panda is hungry with nothing
///    to eat) AND the temperature slows its growth. A short green message
///    confirms it when the player fixes the temperature.
/// While dragging the slider it reacts instantly.
///
/// Put this on a UI object next to the thermometer, with a TMP text.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TemperatureFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private PandaSystem panda;
    [SerializeField] private PandaPathMover pandaPathMover;
    [SerializeField] private DilemmaUI dilemmaUI;

    [Tooltip("The temperature slider (so messages react instantly while dragging).")]
    [SerializeField] private Slider temperatureSlider;

    [Header("When Is It Relevant")]
    [Tooltip("The water message only shows when the water is below this (%).")]
    [SerializeField, Range(0f, 100f)] private float waterLowBelow = 40f;

    [Tooltip("Bamboo counts as low at or below this many ready bamboo.")]
    [SerializeField, Min(0)] private int bambooLowAtOrBelow = 1;

    [Tooltip("After letting go of the slider, keep reacting instantly for this long.")]
    [SerializeField, Min(0f)] private float afterDragTime = 1f;

    [Header("Timing")]
    [Tooltip("A situation must last this long before it pops up (stops flicker).")]
    [SerializeField, Min(0f)] private float showAfter = 0.75f;

    [Tooltip("Seconds the green 'good' message stays after a problem is fixed.")]
    [SerializeField, Min(0f)] private float goodMessageTime = 2.5f;

    [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;

    [Tooltip("Small pop when the message changes.")]
    [SerializeField, Range(0f, 0.4f)] private float popAmount = 0.12f;

    [Header("Messages ({0} = temperature limit, {1} = second limit)")]
    [SerializeField] private string waterEvaporates = "Above {0}°C water evaporates";
    [SerializeField] private string waterFreezes = "Below {0}°C water freezes";
    [SerializeField] private string waterRefillsSlowly = "Below {0}°C water refills slowly";
    [SerializeField] private string bambooTooHot = "Above {0}°C bamboo grows slowly";
    [SerializeField] private string bambooTooCold = "Below {0}°C bamboo grows slowly";
    [SerializeField] private string waterGood = "{0}–{1}°C: water refills";
    [SerializeField] private string bambooGood = "{0}–{1}°C: bamboo grows fast";

    [Header("Colours")]
    [SerializeField] private Color badColor = new Color(1f, 0.55f, 0.3f, 1f);
    [SerializeField] private Color goodColor = new Color(0.55f, 0.95f, 0.5f, 1f);

    private enum Kind { None, WaterBad, BambooBad }

    private CanvasGroup group;
    private Vector3 baseScale;
    private float alpha;
    private float popTimer = 999f;

    private string shownText;
    private string candidateText;

    // Does the line on screen tell the player something is wrong (orange)?
    private bool builtBad;
    private bool candidateBad;
    private bool shownBad;
    private float candidateSince;

    private bool bambooProblemShown;
    private string goodText;
    private float goodUntil = -999f;

    private bool dragging;
    private float lastDragTime = -999f;

    // =========================================================
    // SETUP
    // =========================================================

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        baseScale = transform.localScale;
    }

    private void Start()
    {
        if (panda == null) panda = FindFirstObjectByType<PandaSystem>();
        if (pandaPathMover == null) pandaPathMover = FindFirstObjectByType<PandaPathMover>();

        if (messageText != null)
            messageText.richText = true;

        if (temperatureSlider != null)
        {
            EventTrigger trigger = temperatureSlider.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = temperatureSlider.gameObject.AddComponent<EventTrigger>();

            AddEvent(trigger, EventTriggerType.PointerDown, () => dragging = true);
            AddEvent(trigger, EventTriggerType.PointerUp, () => { dragging = false; lastDragTime = Time.unscaledTime; });
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        EnvironmentSystem env = EnvironmentSystem.Instance;

        string text = null;

        builtBad = false;

        if (env != null && env.ActiveZone != null && !IsBusy())
            text = BuildText(env);

        // Wait until the situation has lasted a moment (live while dragging).
        bool live = dragging || Time.unscaledTime - lastDragTime < afterDragTime;

        if (text != candidateText)
        {
            candidateText = text;
            candidateBad = builtBad;
            candidateSince = Time.unscaledTime;
        }

        bool settled = live || Time.unscaledTime - candidateSince >= showAfter;

        if (settled && candidateText != shownText)
        {
            shownText = candidateText;
            shownBad = candidateBad;

            if (shownText != null && messageText != null)
            {
                messageText.text = shownText;
                popTimer = 0f;
            }
        }

        UpdateVisuals();

        // A warning on screen = ask the player to adjust the temperature.
        SliderHintArrow.SetTemperaturePrompt(this, shownText != null && shownBad);
    }

    // =========================================================
    // WHAT TO SAY
    // =========================================================

    // Returns up to two lines (water and/or bamboo), or null.
    private string BuildText(EnvironmentSystem env)
    {
        float temp = env.Temperature;

        // ---------- WATER: only when there is little or no water ----------
        string waterLine = null;

        if (env.Water < waterLowBelow)
        {
            // Decided by the temperature (not the water trend), so it is
            // still right when the water is already at 0.
            if (temp >= env.WaterEvaporatesAbove)
                waterLine = Colour(Format(waterEvaporates, env.WaterEvaporatesAbove), badColor);
            else if (temp <= env.WaterFreezesBelow)
                waterLine = Colour(Format(waterFreezes, env.WaterFreezesBelow), badColor);
            else if (temp < env.WaterComfortMin)
                waterLine = Colour(Format(waterRefillsSlowly, env.WaterComfortMin), badColor);
            else if (temp <= env.WaterComfortMax)
                waterLine = Colour(Format(waterGood, env.WaterComfortMin, env.WaterComfortMax), goodColor);
            // Between the comfortable range and evaporation the water holds
            // steady, so there is nothing to say.
        }

        // ---------- BAMBOO: only when bamboo is low ----------
        string bambooBad = null;

        if (env.GrowthCondition == EnvironmentSystem.GrowthConditionType.TooHot)
            bambooBad = Format(bambooTooHot, env.BambooIdealMax);
        else if (env.GrowthCondition == EnvironmentSystem.GrowthConditionType.TooCold)
            bambooBad = Format(bambooTooCold, env.BambooIdealMin);

        bool bambooLow = BambooIsLow(env);
        string bambooLine = null;

        if (bambooLow && bambooBad != null)
        {
            bambooLine = Colour(bambooBad, badColor);
            bambooProblemShown = true;
        }
        else
        {
            // The player fixed the temperature while bamboo was low: confirm it.
            if (bambooProblemShown && bambooBad == null)
                SetGood(Format(bambooGood, env.BambooIdealMin, env.BambooIdealMax));

            bambooProblemShown = false;

            if (Time.unscaledTime < goodUntil && goodText != null)
                bambooLine = Colour(goodText, goodColor);
        }

        // ---------- Combine (one or two lines) ----------
        if (waterLine != null && bambooLine != null)
            return waterLine + "\n" + bambooLine;

        return waterLine ?? bambooLine;
    }

    private bool BambooIsLow(EnvironmentSystem env)
    {
        BambooManager manager = BambooManager.Instance;

        if (manager == null)
            return false;

        int ready = manager.GetCurrentCountForZone(env.ActiveZone);
        int capacity = manager.GetCapacityForZone(env.ActiveZone);

        // Every spot is full: growth doesn't matter right now.
        if (capacity <= 0 || ready >= capacity)
            return false;

        bool hungryWithNothing = panda != null && panda.IsHungry &&
                                 !manager.HasAvailableBambooInCurrentZone();

        return ready <= bambooLowAtOrBelow || hungryWithNothing;
    }

    private void SetGood(string text)
    {
        goodText = text;
        goodUntil = Time.unscaledTime + goodMessageTime;
    }

    private string Colour(string text, Color colour)
    {
        if (colour == badColor && !string.IsNullOrWhiteSpace(text))
            builtBad = true;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return "<color=#" + ColorUtility.ToHtmlStringRGBA(colour) + ">" + text + "</color>";
    }

    private static string Format(string template, float a, float b = 0f)
    {
        if (string.IsNullOrEmpty(template))
            return null;

        return string.Format(template, Mathf.RoundToInt(a), Mathf.RoundToInt(b));
    }

    private bool IsBusy()
    {
        if (pandaPathMover != null && pandaPathMover.IsTravelling)
            return true;

        if (dilemmaUI != null && dilemmaUI.IsDilemmaOpen)
            return true;

        return false;
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void UpdateVisuals()
    {
        float target = shownText != null ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / fadeTime);
        group.alpha = alpha;

        popTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(popTimer / 0.25f);
        transform.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * popAmount);
    }

    private static void AddEvent(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}