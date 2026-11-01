using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// A speech bubble in the game world, above the panda, that pops up only
/// when the panda feels bad (hungry, thirsty, too hot...). When the panda
/// is content, or stands up and walks, the bubble shrinks away and is
/// switched off completely.
///
/// The MAIN mood (the most serious one; if equal, the one that has lasted
/// longest) is shown bigger and grows more intense the longer it lasts.
///
/// The bubble stays exactly where you placed it in the scene; this script
/// only shows, hides and animates it. Drag the bubble (or its Canvas)
/// into "Bubble Root".
/// </summary>
public class PandaMoodUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PandaSystem panda;
    [SerializeField] private TMP_Text moodText;

    [Tooltip("Optional: hides the bubble while travelling.")]
    [SerializeField] private PandaPathMover pandaPathMover;

    [Header("Bubble")]
    [Tooltip("The bubble (or its Canvas) that pops up. Stays where you placed it.")]
    [SerializeField] private Transform bubbleRoot;

    [Header("Bubble Grows With The Text")]
    [Tooltip("Grow the bubble when the text gets bigger (long words, more moods).")]
    [SerializeField] private bool growWithText = true;

    [Tooltip("The bubble's image. Empty = Bubble Image below, or Bubble Root.")]
    [SerializeField] private RectTransform bubbleRect;

    [Tooltip("How much WIDER the bubble gets for a long mood like 'VERY HUNGRY' (%).")]
    [SerializeField, Range(0f, 100f)] private float wideForLongWord = 20f;

    [Tooltip("A mood with more letters than this counts as long.")]
    [SerializeField, Min(1)] private int longWordLetters = 8;

    [Tooltip("How much TALLER the bubble gets for each extra mood line (%).")]
    [SerializeField, Range(0f, 100f)] private float tallerPerExtraMood = 15f;

    [Tooltip("When the main mood's text grows and pulses, the bubble grows with it. " +
             "100% = grows exactly as much as the text, 50% = half as much, 0 = not at all.")]
    [SerializeField, Range(0f, 150f)] private float growWithMainText = 100f;

    [Tooltip("How softly the bubble changes size (0 = instantly).")]
    [SerializeField, Range(0f, 0.5f)] private float resizeSmoothTime = 0.15f;

    [Header("When It Shows")]
    [Tooltip("Something must be wrong for this many seconds before the bubble pops up.")]
    [SerializeField, Min(0f)] private float showAfter = 1f;

    [Tooltip("Once shown, the bubble stays at least this long.")]
    [SerializeField, Min(0f)] private float minShowTime = 3f;

    [Tooltip("Hide as soon as the panda stands up to travel, and while it walks.")]
    [SerializeField] private bool hideWhileTravelling = true;

    [Header("Pop Animation")]
    [Tooltip("Seconds to pop in / shrink away.")]
    [SerializeField, Min(0.05f)] private float popTime = 0.35f;

    [Tooltip("How much it overshoots when popping in (0 = none).")]
    [SerializeField, Range(0f, 3f)] private float overshoot = 1.7f;

    [Tooltip("Small bounce when a NEW problem appears while the bubble is already open.")]
    [SerializeField, Range(0f, 0.5f)] private float newMoodBounce = 0.15f;

    [Header("Too Long: Pulse And Glow")]
    [Tooltip("When the MAIN mood has lasted this many seconds, " +
             "the bubble starts pulsing (and glowing).")]
    [SerializeField, Min(0f)] private float pulseAfter = 10f;

    [Tooltip("After this many MORE seconds the pulse is at its strongest.")]
    [SerializeField, Min(0.1f)] private float fullPulseAfter = 10f;

    [Tooltip("How much the bubble grows on each pulse at its strongest (0.08 = 8%).")]
    [SerializeField, Range(0f, 0.3f)] private float pulseAmount = 0.08f;

    [Tooltip("Pulses per second: from gentle at the start to this at the strongest.")]
    [SerializeField, Min(0.1f)] private float pulseSpeedStart = 0.8f;
    [SerializeField, Min(0.1f)] private float pulseSpeedMax = 1.6f;

    [Tooltip("Optional: an Image BEHIND the bubble (a soft glow shape, slightly " +
             "bigger than the bubble). Its transparency pulses with the bubble.")]
    [SerializeField] private Image glowImage;

    [SerializeField] private Color glowColor = new Color(1f, 0.35f, 0.2f, 1f);

    [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.8f;

    [Tooltip("Optional: the bubble Image. Its colour blends towards Warning Tint " +
             "as the pulse gets stronger.")]
    [SerializeField] private Image bubbleImage;

    [SerializeField] private Color warningTint = new Color(1f, 0.8f, 0.75f, 1f);

    [Header("Text")]
    [Tooltip("Show at most this many moods at once (the worst ones).")]
    [SerializeField, Range(1, 4)] private int maxMoods = 2;

    [Header("Main Mood (most intense)")]
    [Tooltip("Text size of the main mood when it has just appeared (100 = normal).")]
    [SerializeField, Range(80f, 200f)] private float mainSizeStart = 110f;

    [Tooltip("Text size of the main mood once it has lasted until the full pulse.")]
    [SerializeField, Range(80f, 250f)] private float mainSizeMax = 140f;

    [Tooltip("Extra size swing of the main mood while pulsing (%).")]
    [SerializeField, Range(0f, 30f)] private float mainTextPulse = 8f;

    [SerializeField] private bool mainBold = true;

    [Tooltip("Size of the other moods (%), so the main one stands out.")]
    [SerializeField, Range(50f, 100f)] private float otherSize = 80f;

    [Tooltip("How faded the other moods are (1 = not faded).")]
    [SerializeField, Range(0.2f, 1f)] private float otherAlpha = 0.7f;

    [Tooltip("Your font may not have emoji. Leave off if they show as squares.")]
    [SerializeField] private bool showEmoji = false;

    [Tooltip("Dark colours read best on a light bubble.")]
    [SerializeField] private Color urgentColor = new Color(0.75f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color warningColor = new Color(0.6f, 0.38f, 0.08f, 1f);

    [Tooltip("The main mood's colour moves towards this as it lasts longer.")]
    [SerializeField] private Color mostIntenseColor = new Color(0.9f, 0.05f, 0.05f, 1f);

    private struct Mood
    {
        public string text;
        public string emoji;
        public int priority;   // higher = more important
        public bool urgent;
        public float duration;  // seconds it has lasted
    }

    private readonly List<Mood> moods = new List<Mood>();
    private readonly HashSet<string> current = new HashSet<string>();
    private readonly HashSet<string> previous = new HashSet<string>();

    private Vector3 baseScale = Vector3.one;

    private Vector2 bubbleMinSize;      // the size you made it in the scene
    private Vector2 textMinSize;
    private Vector2 resizeVelocity;
    private bool textStretchesWithBubble;
    private int shownMoodCount;
    private int longestShownMood;    // letters in the longest mood shown
    private float mainTextSize = 100f;  // current size % of the main mood text

    // The mood text / glow, when they are NOT inside the bubble, are
    // hidden and animated together with it.
    private bool textSeparate;
    private bool glowSeparate;
    private Vector3 textBaseScale = Vector3.one;
    private Vector3 glowBaseScale = Vector3.one;
    private float visible;          // 0 = hidden, 1 = fully popped in
    private bool wantVisible;
    private float badSince = -1f;
    private float shownSince;
    private float bounceTimer = 999f;
    private string lastText;

    private readonly Dictionary<string, float> moodStart = new Dictionary<string, float>();
    private readonly List<string> toRemove = new List<string>();
    private float mainMoodDuration;   // seconds the main mood has lasted
    private float wave;               // 0..1 pulse wave, shared by bubble and text
    private Canvas[] bubbleCanvases;
    private Graphic[] bubbleGraphics;
    private bool bubbleOn = true;
    private float pulseStrength;   // 0..1, smoothed
    private float pulsePhase;
    private Color bubbleBaseColor = Color.white;

    // =========================================================
    // SETUP
    // =========================================================

    private void Start()
    {
        if (panda == null) panda = FindFirstObjectByType<PandaSystem>();
        if (pandaPathMover == null) pandaPathMover = FindFirstObjectByType<PandaPathMover>();

        if (moodText == null)
        {
            Debug.LogError("PandaMoodUI: Mood Text is not assigned.");
            enabled = false;
            return;
        }

        moodText.richText = true;

        if (bubbleRoot == null)
            bubbleRoot = moodText.transform;

        baseScale = bubbleRoot.localScale;

        if (bubbleImage != null)
            bubbleBaseColor = bubbleImage.color;

        if (glowImage != null)
        {
            glowImage.raycastTarget = false;
            glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }
        // What to switch off when hidden. If this script sits inside the
        // bubble, only its visuals are switched off (so the script keeps running).
        bubbleCanvases = bubbleRoot.GetComponentsInChildren<Canvas>(true);
        bubbleGraphics = bubbleRoot.GetComponentsInChildren<Graphic>(true);

        if (bubbleRect == null)
            bubbleRect = bubbleImage != null ? bubbleImage.rectTransform : bubbleRoot as RectTransform;

        if (bubbleRect != null)
            bubbleMinSize = bubbleRect.rect.size;

        textMinSize = moodText.rectTransform.rect.size;

        // Text anchored to stretch with the bubble grows by itself.
        RectTransform tr = moodText.rectTransform;
        textStretchesWithBubble = bubbleRect != null && tr.IsChildOf(bubbleRect) && tr.anchorMin != tr.anchorMax;

        textSeparate = !moodText.transform.IsChildOf(bubbleRoot);
        textBaseScale = moodText.transform.localScale;

        if (glowImage != null)
        {
            glowSeparate = !glowImage.transform.IsChildOf(bubbleRoot);
            glowBaseScale = glowImage.transform.localScale;
        }

        bubbleRoot.localScale = Vector3.zero;   // start hidden
        SetBubbleOn(false);
    }

    private void SetBubbleOn(bool on)
    {
        if (bubbleOn == on)
            return;

        bubbleOn = on;

        // Text and glow outside the bubble: switch them off too.
        if (textSeparate)
            moodText.enabled = on;

        if (glowSeparate)
            glowImage.enabled = on;

        if (!transform.IsChildOf(bubbleRoot))
        {
            bubbleRoot.gameObject.SetActive(on);
            return;
        }

        if (bubbleCanvases.Length > 0)
        {
            foreach (Canvas c in bubbleCanvases)
                if (c != null) c.enabled = on;
        }
        else
        {
            foreach (Graphic g in bubbleGraphics)
                if (g != null) g.enabled = on;
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (panda == null)
            return;

        bool somethingWrong = BuildMoods();

        // Decide whether the bubble should be open.
        // Includes standing up: the journey starts with the stand-up animation.
        bool travelling = hideWhileTravelling && pandaPathMover != null &&
                          (pandaPathMover.IsTravelling || pandaPathMover.HasPendingTravel);

        if (somethingWrong)
        {
            if (badSince < 0f)
                badSince = Time.time;
        }
        else
        {
            badSince = -1f;
        }

        bool shouldShow =
            !travelling && somethingWrong && Time.time - badSince >= showAfter;

        if (shouldShow && !wantVisible)
        {
            wantVisible = true;
            shownSince = Time.time;
        }
        else if (!shouldShow && wantVisible)
        {
            // Keep it up long enough to read, unless travelling.
            if (travelling || Time.time - shownSince >= minShowTime)
                wantVisible = false;
        }

        // Shrink away twice as fast when the panda gets up to walk.
        float speed = travelling && !wantVisible ? 2f : 1f;
        visible = Mathf.MoveTowards(visible, wantVisible ? 1f : 0f, speed * Time.deltaTime / popTime);

        // Fully switched off while hidden.
        SetBubbleOn(wantVisible || visible > 0f);
    }

    private void LateUpdate()
    {
        if (bubbleRoot == null || !bubbleOn)
            return;

        ResizeToText();

        // Pop in with a little overshoot, shrink away smoothly.
        float scale = wantVisible ? EaseOutBack(visible) : EaseIn(visible);

        bounceTimer += Time.deltaTime;
        float bounce = Mathf.Sin(Mathf.Clamp01(bounceTimer / 0.3f) * Mathf.PI) * newMoodBounce;

        // Pulse when the main mood has lasted too long; stronger the longer it lasts.
        float target = wantVisible && mainMoodDuration >= pulseAfter
            ? Mathf.Clamp01((mainMoodDuration - pulseAfter) / fullPulseAfter)
            : 0f;

        pulseStrength = Mathf.MoveTowards(pulseStrength, target, Time.deltaTime * 0.5f);

        float speed = Mathf.Lerp(pulseSpeedStart, pulseSpeedMax, pulseStrength);
        pulsePhase += Time.deltaTime * speed * Mathf.PI * 2f;

        wave = 0.5f - 0.5f * Mathf.Cos(pulsePhase);   // 0..1
        float pulse = wave * pulseAmount * pulseStrength;

        float size = Mathf.Max(0f, scale + bounce * visible + pulse * visible);

        bubbleRoot.localScale = baseScale * size;

        // Text and glow outside the bubble pop and pulse with it.
        if (textSeparate)
            moodText.transform.localScale = textBaseScale * size;

        if (glowSeparate)
            glowImage.transform.localScale = glowBaseScale * size;

        if (glowImage != null)
        {
            Color g = glowColor;
            g.a = glowMaxAlpha * Mathf.Lerp(0.3f, 1f, wave) * pulseStrength * visible;
            glowImage.color = g;
        }

        if (bubbleImage != null)
        {
            bubbleImage.color = Color.Lerp(
                bubbleBaseColor, bubbleBaseColor * warningTint, wave * pulseStrength);
        }
    }

    // =========================================================
    // MOODS
    // =========================================================

    // Returns true if something is wrong.
    private bool BuildMoods()
    {
        moods.Clear();

        if (panda.IsVeryHungry)
            Add("VERY HUNGRY", "🍃", 100, true);
        else if (panda.IsHungry)
            Add("HUNGRY", "🍃", 60, false);

        if (panda.IsThirsty)
            Add("THIRSTY", "💧", 80, true);

        if (panda.IsHot)
            Add("TOO HOT", "🥵", 70, true);

        if (panda.IsCold)
            Add("TOO COLD", "🥶", 70, true);

        if (panda.IsStressed)
            Add("STRESSED", "😰", 50, false);

        // How long has each mood lasted? (Forget moods that are gone.)
        for (int i = 0; i < moods.Count; i++)
        {
            Mood m = moods[i];

            if (!moodStart.TryGetValue(m.text, out float since))
            {
                since = Time.time;
                moodStart[m.text] = since;
            }

            m.duration = Time.time - since;
            moods[i] = m;
        }

        toRemove.Clear();

        foreach (string key in moodStart.Keys)
        {
            if (!moods.Exists(m => m.text == key))
                toRemove.Add(key);
        }

        foreach (string key in toRemove)
            moodStart.Remove(key);

        // Most serious first; if equally serious, the one that has lasted longest.
        moods.Sort((a, b) =>
        {
            int byPriority = b.priority.CompareTo(a.priority);
            return byPriority != 0 ? byPriority : b.duration.CompareTo(a.duration);
        });

        int count = Mathf.Min(maxMoods, moods.Count);
        mainMoodDuration = count > 0 ? moods[0].duration : 0f;

        // Remember for resizing (kept while the bubble shrinks away).
        if (count > 0)
        {
            shownMoodCount = count;
            longestShownMood = 0;

            for (int i = 0; i < count; i++)
                longestShownMood = Mathf.Max(longestShownMood, moods[i].text.Length);
        }

        StringBuilder sb = new StringBuilder();
        current.Clear();

        for (int i = 0; i < count; i++)
        {
            Mood m = moods[i];
            current.Add(m.text);

            if (sb.Length > 0)
                sb.Append('\n');

            string label = (showEmoji ? m.emoji + " " : "") + m.text;
            Color colour = m.urgent ? urgentColor : warningColor;

            if (i == 0)
            {
                // MAIN mood: bigger, bolder and redder the longer it lasts.
                float intensity = Mathf.Clamp01(m.duration / Mathf.Max(0.1f, pulseAfter + fullPulseAfter));
                float size = Mathf.Lerp(mainSizeStart, mainSizeMax, intensity)
                             + mainTextPulse * wave * pulseStrength;

                colour = Color.Lerp(colour, mostIntenseColor, intensity);
                mainTextSize = size;

                sb.Append("<size=").Append(Mathf.RoundToInt(size)).Append("%>");
                if (mainBold) sb.Append("<b>");
                AppendColoured(sb, colour, label);
                if (mainBold) sb.Append("</b>");
                sb.Append("</size>");
            }
            else
            {
                colour.a *= otherAlpha;

                sb.Append("<size=").Append(Mathf.RoundToInt(otherSize)).Append("%>");
                AppendColoured(sb, colour, label);
                sb.Append("</size>");
            }
        }

        // Bounce when a new problem appears while the bubble is open.
        if (visible > 0.9f)
        {
            foreach (string m in current)
            {
                if (!previous.Contains(m))
                {
                    bounceTimer = 0f;
                    break;
                }
            }
        }

        previous.Clear();
        previous.UnionWith(current);

        // Keep the last text while the bubble shrinks away.
        string text = sb.ToString();

        if (count > 0 && text != lastText)
        {
            moodText.text = text;
            lastText = text;
        }

        return count > 0;
    }

    private void Add(string text, string emoji, int priority, bool urgent)
    {
        moods.Add(new Mood { text = text, emoji = emoji, priority = priority, urgent = urgent });
    }

    private static void AppendColoured(StringBuilder sb, Color colour, string text)
    {
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(colour)).Append('>')
          .Append(text).Append("</color>");
    }

    // =========================================================
    // RESIZE
    // =========================================================

    private void ResizeToText()
    {
        if (!growWithText || bubbleRect == null || string.IsNullOrEmpty(moodText.text))
            return;

        // Percent bigger than the size you made it in the scene.
        Vector2 grow = new Vector2(
            1f + (longestShownMood > longWordLetters ? wideForLongWord / 100f : 0f),
            1f + Mathf.Max(0, shownMoodCount - 1) * tallerPerExtraMood / 100f);

        // Grow along with the main mood's text as it gets bigger and pulses.
        float textGrowth = mainTextSize / Mathf.Max(1f, mainSizeStart) - 1f;
        grow *= 1f + Mathf.Max(0f, textGrowth) * growWithMainText / 100f;

        Vector2 target = Vector2.Scale(bubbleMinSize, grow);

        Vector2 size = resizeSmoothTime > 0f && visible > 0.01f
            ? Vector2.SmoothDamp(bubbleRect.rect.size, target, ref resizeVelocity, resizeSmoothTime)
            : target;

        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

        // The text box grows by the same amount, so the text has room too.
        if (!textStretchesWithBubble)
        {
            Vector2 textSize = Vector2.Scale(textMinSize, new Vector2(
                size.x / Mathf.Max(0.0001f, bubbleMinSize.x),
                size.y / Mathf.Max(0.0001f, bubbleMinSize.y)));

            RectTransform tr = moodText.rectTransform;
            tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textSize.x);
            tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textSize.y);
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private float EaseOutBack(float t)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseIn(float t)
    {
        return t * t;
    }
}