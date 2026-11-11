using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// Dynamic narrative: short lines that appear (and optionally are spoken)
/// when they are relevant, e.g. "The panda does not thrive in these high
/// temperatures" when it is hot and health is low.
///
/// Every line has its own conditions (temperature, health, bamboo, human
/// disturbance and whether it is rising or falling). Only one line shows at
/// a time, never during dilemmas or travel, and each line has a cooldown so
/// the player isn't flooded.
///
/// Put this on an empty GameObject (e.g. "Narrative"). When you add it, the
/// lines from the draft are filled in automatically.
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public enum Trend { Any, Rising, Falling }

    [System.Serializable]
    public class NarrativeLine
    {
        [Tooltip("A note for yourself, e.g. 'Hot - low health'.")]
        public string label;

        [TextArea(2, 4)]
        public string text;

        [Tooltip("Optional voice-over. Empty = text only.")]
        public AudioClip voice;

        [Tooltip("This line tells the player to change the temperature: the " +
                 "slider arrows show while it is on screen.")]
        public bool pointsToTemperature = true;

        [Header("Temperature (°C)")]
        public bool checkTemperature = true;
        public float temperatureMin = -100f;
        public float temperatureMax = 100f;

        [Header("Panda Health")]
        public bool checkHealth;
        [Tooltip("Only when the panda's health is below this.")]
        [Range(0f, 100f)] public float healthBelow = 50f;

        [Tooltip("Only while the panda is actually losing health from the heat " +
                 "(after its 'Seconds Until Hot' delay, above the extreme heat temperature).")]
        public bool requireHeatDamage;

        [Header("Bamboo")]
        public bool checkBamboo;
        [Tooltip("Only when there are this many ready bamboo or fewer in the zone.")]
        [Min(0)] public int bambooAtOrBelow = 2;

        [Header("Human Disturbance (%)")]
        public bool checkDisturbance;
        [Range(0f, 100f)] public float disturbanceMin = 0f;
        [Range(0f, 100f)] public float disturbanceMax = 100f;

        [Tooltip("Rising = only while it is going up. Falling = only while it is going down.")]
        public Trend disturbanceTrend = Trend.Any;

        [Header("Zone")]
        [Tooltip("Only in this zone (e.g. 'Valley'). Empty = every zone.")]
        public string zoneName = "";

        [Header("Timing")]
        [Tooltip("The conditions must be true for this many seconds before the line plays.")]
        [Min(0f)] public float holdFor = 2f;

        [Tooltip("Seconds before this same line may play again.")]
        [Min(0f)] public float cooldown = 60f;

        [Tooltip("Only ever play this line once per game.")]
        public bool onlyOnce;

        [Tooltip("When several lines are ready, the highest priority plays first.")]
        public int priority;

        // Runtime
        [System.NonSerialized] public float trueSince = -1f;
        [System.NonSerialized] public float lastPlayed = -9999f;
        [System.NonSerialized] public bool played;
    }

    [Header("References")]
    [SerializeField] private PandaSystem panda;
    [SerializeField] private PandaPathMover pandaPathMover;
    [SerializeField] private DilemmaManager dilemmaManager;
    [SerializeField] private DilemmaUI dilemmaUI;

    [Header("Display")]
    [Tooltip("The panel/bar that shows the line (hidden when nothing is said).")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text lineText;

    [Tooltip("Seconds a text-only line stays (longer texts stay a bit longer).")]
    [SerializeField, Min(1f)] private float minShowTime = 5f;

    [Tooltip("Extra seconds per word for text-only lines.")]
    [SerializeField, Min(0f)] private float secondsPerWord = 0.3f;

    [SerializeField, Min(0.01f)] private float fadeTime = 0.3f;

    [Header("Pacing")]
    [Tooltip("Minimum seconds between two different lines.")]
    [SerializeField, Min(0f)] private float gapBetweenLines = 20f;

    [Tooltip("No lines in the first seconds of the game (counted from the end of the tutorial).")]
    [SerializeField, Min(0f)] private float quietAtStart = 10f;

    [Header("Voice")]
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [SerializeField] private AudioMixerGroup voiceMixerGroup;

    [Header("Lines")]
    [SerializeField] private List<NarrativeLine> lines = new List<NarrativeLine>();

    [Header("Live (read only)")]
    [SerializeField] private string currentLine;
    [SerializeField] private Trend disturbanceTrendNow;

    /// <summary>True while a narrative line with voice is playing (used to duck ambience).</summary>
    public static bool IsSpeaking { get; private set; }

    private CanvasGroup group;

    // True from showing a line until it has faded out. Only then does this
    // script touch the panel, so other scripts can share it.
    private bool usingPanel;
    private bool currentPointsToTemperature;
    private AudioSource voiceSource;
    private float showUntil = -1f;
    private float lastLineEnded = -9999f;
    private float introEndedAt;

    // Disturbance trend
    private float lastSample;
    private float sampleTimer;

    // =========================================================
    // DEFAULT LINES (from the draft)
    // =========================================================

    private void Reset()
    {
        lines = new List<NarrativeLine>
        {
            new NarrativeLine
            {
                label = "Hot - taking heat damage",
                text = "The panda does not thrive in these high temperatures.",
                checkTemperature = false,
                requireHeatDamage = true,
                holdFor = 1f,
                priority = 3
            },
            new NarrativeLine
            {
                label = "Hot - disturbance rising",
                text = "Human disturbance increases when the temperature rises.",
                checkTemperature = true, temperatureMin = 25f, temperatureMax = 100f,
                checkDisturbance = true, disturbanceMin = 15f, disturbanceMax = 100f,
                disturbanceTrend = Trend.Rising,
                pointsToTemperature = false,
                priority = 2
            },
            new NarrativeLine
            {
                label = "Bamboo low",
                text = "You are running low on food for the panda. Bamboo grows faster in higher temperatures.",
                checkTemperature = true, temperatureMin = -100f, temperatureMax = 25f,
                checkBamboo = true, bambooAtOrBelow = 2,
                priority = 2
            },
            new NarrativeLine
            {
                label = "Cold - disturbance falling",
                text = "Human disturbance decreases in low temperatures.",
                checkTemperature = true, temperatureMin = -100f, temperatureMax = 15f,
                checkDisturbance = true, disturbanceTrend = Trend.Falling,
                pointsToTemperature = false,
                priority = 1
            }
        };
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void Start()
    {
        if (panda == null) panda = FindFirstObjectByType<PandaSystem>();
        if (pandaPathMover == null) pandaPathMover = FindFirstObjectByType<PandaPathMover>();
        if (dilemmaManager == null) dilemmaManager = FindFirstObjectByType<DilemmaManager>();

        if (panel != null)
        {
            group = panel.GetComponent<CanvasGroup>();
            if (group == null)
                group = panel.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.blocksRaycasts = false;
            panel.SetActive(false);
        }

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.outputAudioMixerGroup = voiceMixerGroup;

        if (EnvironmentSystem.Instance != null)
            lastSample = EnvironmentSystem.Instance.Pollution;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // 'Quiet At Start' counts from when the tutorial ends.
        if (IntroSequence.IsPlaying)
            introEndedAt = Time.time;

        UpdateDisturbanceTrend();
        UpdateConditions();
        UpdateDisplay();

        IsSpeaking = voiceSource != null && voiceSource.isPlaying;
    }

    // Is human disturbance going up, down, or staying the same?
    private void UpdateDisturbanceTrend()
    {
        EnvironmentSystem env = EnvironmentSystem.Instance;

        if (env == null)
            return;

        sampleTimer += Time.deltaTime;

        if (sampleTimer < 1f)
            return;

        float now = env.Pollution;
        float change = now - lastSample;

        disturbanceTrendNow =
            change > 0.0005f ? Trend.Rising :
            change < -0.0005f ? Trend.Falling :
            Trend.Any;

        lastSample = now;
        sampleTimer = 0f;
    }

    private void UpdateConditions()
    {
        bool busy = IsBusy();
        bool showing = Time.time < showUntil;

        NarrativeLine best = null;

        foreach (NarrativeLine line in lines)
        {
            if (line == null)
                continue;

            bool met = !busy && ConditionsMet(line);

            if (!met)
            {
                line.trueSince = -1f;
                continue;
            }

            if (line.trueSince < 0f)
                line.trueSince = Time.time;

            bool ready =
                Time.time - line.trueSince >= line.holdFor &&
                Time.time - line.lastPlayed >= line.cooldown &&
                !(line.onlyOnce && line.played);

            if (ready && (best == null || line.priority > best.priority))
                best = line;
        }

        if (best == null || showing || busy)
            return;

        if (Time.time - introEndedAt < quietAtStart || Time.time - lastLineEnded < gapBetweenLines)
            return;

        Play(best);
    }

    private bool ConditionsMet(NarrativeLine line)
    {
        EnvironmentSystem env = EnvironmentSystem.Instance;

        if (env == null || env.ActiveZone == null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.zoneName) &&
            !string.Equals(env.ActiveZoneName, line.zoneName.Trim(), System.StringComparison.OrdinalIgnoreCase))
            return false;

        if (line.checkTemperature &&
            (env.Temperature < line.temperatureMin || env.Temperature > line.temperatureMax))
            return false;

        if (line.checkHealth && (panda == null || panda.Health >= line.healthBelow))
            return false;

        if (line.requireHeatDamage && (panda == null || !panda.IsInExtremeHeat))
            return false;

        if (line.checkBamboo)
        {
            BambooManager manager = BambooManager.Instance;

            if (manager == null ||
                manager.GetCurrentCountForZone(env.ActiveZone) > line.bambooAtOrBelow)
                return false;
        }

        if (line.checkDisturbance)
        {
            float percent = env.Pollution * 100f;

            if (percent < line.disturbanceMin || percent > line.disturbanceMax)
                return false;

            if (line.disturbanceTrend != Trend.Any && disturbanceTrendNow != line.disturbanceTrend)
                return false;
        }

        return true;
    }

    private bool IsBusy()
    {
        // No feedback lines while the intro/tutorial plays.
        if (IntroSequence.IsPlaying)
            return true;

        if (pandaPathMover != null && pandaPathMover.IsTravelling)
            return true;

        if (dilemmaUI != null && dilemmaUI.IsDilemmaOpen)
            return true;

        // A dilemma or choice narration is speaking.
        if (dilemmaManager != null && dilemmaManager.IsNarrating)
            return true;

        return false;
    }

    // =========================================================
    // PLAY / SHOW
    // =========================================================

    private void Play(NarrativeLine line)
    {
        line.played = true;
        line.lastPlayed = Time.time;
        currentPointsToTemperature = line.pointsToTemperature;
        line.trueSince = -1f;
        currentLine = line.label;

        float duration = minShowTime;

        if (!string.IsNullOrEmpty(line.text))
            duration = Mathf.Max(minShowTime, line.text.Split(' ').Length * secondsPerWord);

        if (line.voice != null)
        {
            voiceSource.clip = line.voice;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            duration = Mathf.Max(duration, line.voice.length + 0.5f);
        }

        if (lineText != null)
            lineText.text = line.text;

        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);

        usingPanel = true;
        showUntil = Time.time + duration;

        Debug.Log("Narrative: '" + line.label + "'");
    }

    private void UpdateDisplay()
    {
        bool showing = Time.time < showUntil;

        SliderHintArrow.SetTemperaturePrompt(this, showing && currentPointsToTemperature);

        // The intro/tutorial may use the same panel for its subtitles:
        // don't touch it until the game has started.
        if (IntroSequence.IsPlaying)
            return;

        // Hide right away if a dilemma or travel starts.
        if (showing && IsBusy())
        {
            showUntil = Time.time;
            showing = false;

            if (voiceSource != null && voiceSource.isPlaying)
                voiceSource.Stop();
        }

        if (!showing && showUntil > 0f && lastLineEnded < showUntil)
            lastLineEnded = Time.time;

        if (group == null || !usingPanel)
            return;

        group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.deltaTime / fadeTime);

        if (!showing && group.alpha <= 0f)
        {
            if (panel.activeSelf)
                panel.SetActive(false);

            currentLine = "";
            usingPanel = false;
        }
    }
}