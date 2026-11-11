using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Game intro + tutorial, before the game starts:
/// 1. INTRO: the MAIN camera starts far away and glides slowly in to its
///    normal (panda) view while a narrator speaks.
/// 2. TUTORIAL: step by step, each function is introduced (its UI appears,
///    lights up, the narrator explains it) and the player tries it
///    (e.g. feed the panda, change the temperature).
/// 3. The game starts. Until then nothing hurts the panda and no dilemmas
///    or hints appear.
///
/// Setup:
/// 1. Put this on an empty GameObject ("Intro").
/// 2. Create one or more empty GameObjects where the camera should START
///    (and pass through), rotated the way the camera should look.
///    Add them to 'Camera Path' in order. The camera always ENDS at the main
///    camera's normal position, so don't add that.
/// 3. Add the narration clip (and optional subtitles).
/// </summary>
public class IntroSequence : MonoBehaviour
{
    public enum WaitFor
    {
        NarrationOnly,       // Continue when the narration has finished.
        Tap,                 // Continue when the player taps the screen.
        FeedPanda,           // Continue when the player has fed the panda.
        ChangeTemperature    // Continue when the player has changed the temperature.
    }

    [System.Serializable]
    public class TutorialStep
    {
        [Tooltip("A note for yourself, e.g. 'Feeding'.")]
        public string label;

        [Tooltip("Shown on screen during this step. Empty = nothing shown.")]
        [TextArea(2, 4)] public string instruction;

        [Tooltip("Optional voice-over for this step.")]
        public AudioClip narration;

        [Tooltip("UI that appears at this step (and stays). Also add it to " +
                 "'Hide During Intro' so it's hidden before.")]
        public GameObject[] showUI;

        [Tooltip("Same as 'Show UI', but by the object's NAME in the scene " +
                 "(e.g. 'TempSlider'). Use this when the Intro is a prefab.")]
        public string[] showUINames;

        [Tooltip("UI that gently pulses (grows/shrinks) during this step, to show " +
                 "what to use. Empty = the 'Show UI' objects pulse.")]
        public GameObject[] highlight;

        [Tooltip("Same as 'Highlight', but by the object's NAME in the scene.")]
        public string[] highlightNames;

        [Tooltip("What the player must do before the next step.")]
        public WaitFor waitFor = WaitFor.NarrationOnly;

        [Tooltip("For 'Change Temperature': how many degrees it must change.")]
        [Min(0.1f)] public float degreesToChange = 5f;

        [Tooltip("Seconds to wait after the step is done, before the next one.")]
        [Min(0f)] public float pauseAfter = 1f;

        [Tooltip("Subtitles for this step's narration (uses the same subtitle " +
                 "panel as the intro). Times are seconds into the clip.")]
        public List<SubtitleLine> subtitles = new List<SubtitleLine>();
    }

    /// <summary>True while the intro or tutorial plays (other scripts pause gameplay).</summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>True while the intro narration is speaking (for ducking ambience).</summary>
    public static bool IsNarrating { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
        IsNarrating = false;
    }

    [Header("Play")]
    [Tooltip("Off: skip the intro (handy while testing other things).")]
    [SerializeField] private bool playIntro = true;

    [Header("Camera")]
    [Tooltip("The main (panda) camera. Empty = Camera.main.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Where the camera starts, and points it passes through, in order. " +
             "It always ends at the main camera's normal view.")]
    [SerializeField] private List<Transform> cameraPath = new List<Transform>();

    [Tooltip("Seconds the camera holds still at the start before moving.")]
    [SerializeField, Min(0f)] private float holdAtStart = 1.5f;

    [Tooltip("Seconds the whole camera move takes.")]
    [SerializeField, Min(0.1f)] private float moveDuration = 15f;

    [Tooltip("On: the move takes as long as the narration (plus 'Hold At Start'), " +
             "so the camera arrives when the narrator finishes.")]
    [SerializeField] private bool matchNarrationLength = true;

    [Tooltip("How the move speeds up and slows down. The default starts and ends gently.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Field of view at the start. 0 = same as the main camera. " +
             "A smaller number than the main camera = starts zoomed in.")]
    [SerializeField, Range(0f, 120f)] private float startFieldOfView = 0f;

    [Header("Narration")]
    [SerializeField] private AudioClip narrationClip;

    [Tooltip("Seconds after the intro starts before the narrator speaks.")]
    [SerializeField, Min(0f)] private float narrationDelay = 1f;

    [SerializeField, Range(0f, 1f)] private float narrationVolume = 1f;
    [SerializeField] private AudioMixerGroup narrationMixerGroup;

    [Header("Subtitles (optional)")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private List<SubtitleLine> subtitles = new List<SubtitleLine>();

    [Header("Tutorial")]
    [Tooltip("Off: go straight to the game after the camera intro.")]
    [SerializeField] private bool playTutorial = true;

    [Tooltip("The panel showing the step's instruction (hidden between steps).")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TMP_Text instructionText;

    [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    public enum AppearStyle { Fade, PopUp }

    [Header("UI")]
    [Tooltip("UI that is HIDDEN while the camera moves (and during the tutorial until " +
             "its step shows it). Anything not shown by a step appears when the game " +
             "starts. E.g. TempSlider, BambooPanel, PollutionPanel, mood/health UI.")]
    [SerializeField] private GameObject[] hideDuringIntro;

    [Tooltip("Same as 'Hide During Intro', but by the object's NAME in the scene " +
             "(e.g. 'TempSlider', 'PollutionPanel'). Use this when the Intro is a " +
             "prefab, so you don't have to change the scene.")]
    [SerializeField] private string[] hideDuringIntroNames;

    [SerializeField, Min(0.01f)] private float uiFadeInTime = 0.8f;

    [Header("Tutorial UI Appearing")]
    [Tooltip("How a tutorial step's UI appears. Pop Up = grows in with a little bounce.")]
    [SerializeField] private AppearStyle tutorialAppear = AppearStyle.PopUp;

    [Tooltip("Seconds the pop-up takes.")]
    [SerializeField, Min(0.05f)] private float popUpTime = 0.5f;

    [Tooltip("How much it overshoots before settling. 0 = no bounce.")]
    [SerializeField, Range(0f, 4f)] private float popUpBounce = 1.7f;

    // The normal size of every UI the intro hides, pops or pulses.
    private readonly Dictionary<Transform, Vector3> normalScales = new Dictionary<Transform, Vector3>();

    private Vector3 NormalScale(Transform t)
    {
        if (!normalScales.TryGetValue(t, out Vector3 scale))
        {
            scale = t.localScale;
            normalScales[t] = scale;
        }

        return scale;
    }

    [Header("Skip")]
    [Tooltip("Tap/click anywhere to skip the camera intro (not the tutorial).")]
    [SerializeField] private bool allowSkip = true;

    [Tooltip("Skipping only works after this many seconds (so a stray tap doesn't skip it).")]
    [SerializeField, Min(0f)] private float skipAllowedAfter = 2f;

    [Header("When The Intro Ends")]
    [Tooltip("Hook up anything that should happen when the game starts " +
             "(e.g. later: the UI tutorial).")]
    public UnityEvent onIntroFinished;

    private AudioSource narrationSource;
    private CanvasGroup[] uiGroups;
    private CanvasGroup subtitleGroup;
    private bool skipRequested;
    private bool tappedThisFrame;
    private bool inTutorial;
    private CanvasGroup instructionGroup;
    private bool showInstruction;
    private PandaSystem panda;
    private bool fedDuringStep;

    // Default steps when the component is added.
    private void Reset()
    {
        tutorialSteps = new List<TutorialStep>
        {
            new TutorialStep
            {
                label = "Feeding",
                instruction = "Drag the bamboo to the panda to feed it.",
                waitFor = WaitFor.FeedPanda
            },
            new TutorialStep
            {
                label = "Temperature",
                instruction = "Slide the thermometer to change the temperature. " +
                              "Warmer makes bamboo grow faster - but the panda can get too hot.",
                waitFor = WaitFor.ChangeTemperature
            },
            new TutorialStep
            {
                label = "Human disturbance",
                instruction = "This shows the human disturbance. It rises when it gets warmer.",
                waitFor = WaitFor.NarrationOnly,
                pauseAfter = 3f
            }
        };
    }

    private void Awake()
    {
        // Set before any other Start/Update runs, so nothing happens in frame 1.
        IsPlaying = playIntro;
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        PrepareUI();

        panda = FindFirstObjectByType<PandaSystem>();
        if (panda != null)
            panda.Fed += OnPandaFed;

        if (instructionPanel != null)
        {
            instructionGroup = instructionPanel.GetComponent<CanvasGroup>();
            if (instructionGroup == null)
                instructionGroup = instructionPanel.AddComponent<CanvasGroup>();

            instructionGroup.blocksRaycasts = false;
            instructionGroup.alpha = 0f;
            instructionPanel.SetActive(false);
        }

        if (subtitlePanel != null)
        {
            subtitleGroup = subtitlePanel.GetComponent<CanvasGroup>();
            if (subtitleGroup == null)
                subtitleGroup = subtitlePanel.AddComponent<CanvasGroup>();

            subtitleGroup.blocksRaycasts = false;
            subtitleGroup.alpha = 0f;
            subtitlePanel.SetActive(false);
        }

        if (!playIntro || mainCamera == null || cameraPath.Count == 0)
        {
            if (playIntro && mainCamera == null)
                Debug.LogWarning("IntroSequence: no main camera found. Skipping the camera intro.", this);
            else if (playIntro)
                Debug.LogWarning("IntroSequence: 'Camera Path' is empty. Skipping the camera intro.", this);

            // No camera intro, but the tutorial can still run.
            if (playIntro && playTutorial && tutorialSteps.Count > 0)
                StartCoroutine(TutorialOnly());
            else
                Finish(true);

            return;
        }

        StartCoroutine(PlayIntro());
    }

    private IEnumerator TutorialOnly()
    {
        yield return RunTutorial();
        Finish(false);
    }

    private void Update()
    {
        tappedThisFrame = IsPlaying && TappedThisFrame();

        if (tappedThisFrame && allowSkip && !inTutorial)
            skipRequested = true;

        UpdateInstructionPanel();
        UpdateDim();
        UpdateSubtitles();
    }

    private void OnDestroy()
    {
        if (panda != null)
            panda.Fed -= OnPandaFed;
    }

    private void OnPandaFed(float health)
    {
        fedDuringStep = true;
    }

    private static bool TappedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Pointer.current != null &&
               UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0) ||
               (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
#endif
    }

    // =========================================================
    // INTRO
    // =========================================================

    private IEnumerator PlayIntro()
    {
        Transform cam = mainCamera.transform;

        // The end of the move = the main camera's normal view.
        Vector3 endPosition = cam.position;
        Quaternion endRotation = cam.rotation;
        float endFov = mainCamera.fieldOfView;
        float startFov = startFieldOfView > 0f ? startFieldOfView : endFov;

        // Path: the start/through points, then the normal view.
        List<Vector3> points = new List<Vector3>();
        List<Quaternion> rotations = new List<Quaternion>();

        foreach (Transform p in cameraPath)
        {
            if (p == null)
                continue;

            points.Add(p.position);
            rotations.Add(p.rotation);
        }

        points.Add(endPosition);
        rotations.Add(endRotation);

        // Distance along the path, so the camera moves at an even pace.
        float[] distanceAt = new float[points.Count];
        for (int i = 1; i < points.Count; i++)
            distanceAt[i] = distanceAt[i - 1] + Vector3.Distance(points[i - 1], points[i]);

        float totalDistance = Mathf.Max(distanceAt[points.Count - 1], 0.001f);

        float duration = moveDuration;
        if (matchNarrationLength && narrationClip != null)
            duration = Mathf.Max(0.1f, narrationDelay + narrationClip.length - holdAtStart);

        // Start position.
        cam.SetPositionAndRotation(points[0], rotations[0]);
        mainCamera.fieldOfView = startFov;

        float elapsed = 0f;
        float total = holdAtStart + duration;
        bool narrationStarted = false;

        while (elapsed < total)
        {
            if (skipRequested && elapsed >= skipAllowedAfter)
                break;

            elapsed += Time.unscaledDeltaTime;

            // Narration.
            if (!narrationStarted && narrationClip != null && elapsed >= narrationDelay)
            {
                narrationStarted = true;
                PlayNarration();
            }

            IsNarrating = narrationSource != null && narrationSource.isPlaying;

            // Camera.
            float t = Mathf.Clamp01((elapsed - holdAtStart) / duration);
            float eased = ease != null && ease.length >= 2 ? ease.Evaluate(t) : t * t * (3f - 2f * t);

            Place(cam, points, rotations, distanceAt, totalDistance, eased);
            mainCamera.fieldOfView = Mathf.Lerp(startFov, endFov, eased);

            yield return null;
        }

        // Make sure it ends exactly at the normal view.
        cam.SetPositionAndRotation(endPosition, endRotation);
        mainCamera.fieldOfView = endFov;

        // Let the narration finish if it's still going (unless skipped).
        while (!skipRequested && narrationSource != null && narrationSource.isPlaying)
        {
            yield return null;
        }

        if (narrationSource != null)
            narrationSource.Stop();

        IsNarrating = false;

        // The subtitle panel just fades out (it stays switched on for the tutorial).
        if (playTutorial && tutorialSteps.Count > 0)
            yield return RunTutorial();

        Finish(false);
    }

    // =========================================================
    // TUTORIAL
    // =========================================================

    private IEnumerator RunTutorial()
    {
        inTutorial = true;

        foreach (TutorialStep step in tutorialSteps)
        {
            if (step == null)
                continue;

            Debug.Log("IntroSequence: Tutorial step '" + step.label + "'.");

            // Show this step's UI (it stays from now on) and light it up.
            GameObject[] show = Combine(step.showUI, step.showUINames);
            GameObject[] highlights = Combine(step.highlight, step.highlightNames);

            if (highlights.Length == 0)
                highlights = show;

            if (show.Length > 0)
                StartCoroutine(FadeInObjects(show));

            // Only this step's UI can be used; everything else is locked.
            if (lockOtherUIDuringStep)
                LockAllExcept(show, highlights);

            Coroutine pulse = pulseHighlight ? StartCoroutine(Pulse(highlights)) : null;

            if (dimAroundHighlight)
                SpotlightOn(highlights);

            if (instructionText != null && !string.IsNullOrWhiteSpace(step.instruction))
            {
                instructionText.text = step.instruction;
                showInstruction = true;
            }

            // Narration.
            if (step.narration != null)
            {
                PlayClip(step.narration);
                activeSubtitles = step.subtitles;
            }

            float startTemperature = EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.Temperature
                : 0f;

            fedDuringStep = false;

            // Wait for the narration AND the player's action.
            while (true)
            {
                IsNarrating = narrationSource != null && narrationSource.isPlaying;

                bool narrationDone = !IsNarrating;
                bool actionDone;

                switch (step.waitFor)
                {
                    case WaitFor.Tap:
                        actionDone = narrationDone && tappedThisFrame;
                        break;

                    case WaitFor.FeedPanda:
                        actionDone = fedDuringStep;
                        break;

                    case WaitFor.ChangeTemperature:
                        actionDone = EnvironmentSystem.Instance == null ||
                                     Mathf.Abs(EnvironmentSystem.Instance.Temperature - startTemperature)
                                     >= step.degreesToChange;
                        break;

                    default:
                        actionDone = true;
                        break;
                }

                if (narrationDone && actionDone)
                    break;

                yield return null;
            }

            IsNarrating = false;
            if (pulse != null)
                StopCoroutine(pulse);

            ResetPulse(highlights);
            SpotlightOff();
            showInstruction = false;

            float wait = 0f;
            while (wait < step.pauseAfter)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        inTutorial = false;
    }

    [Header("Highlight: Darken Around")]
    [Tooltip("On: everything else on screen (scene and other UI) gets darker during a " +
             "step, so only the highlighted UI (and the instruction) stays bright.")]
    [SerializeField] private bool dimAroundHighlight = true;

    [Tooltip("How dark everything else gets. 0 = not at all, 1 = black.")]
    [SerializeField, Range(0f, 1f)] private float dimAmount = 0.6f;

    [SerializeField] private Color dimColor = Color.black;

    [Tooltip("Seconds to fade the darkening in/out.")]
    [SerializeField, Min(0.01f)] private float dimFadeTime = 0.3f;

    private UnityEngine.UI.Image dimImage;
    private bool dimWanted;

    // Canvases added to bring highlighted UI above the darkening.
    private class Lifted
    {
        public Canvas canvas;
        public bool added;
        public bool oldOverride;
        public int oldOrder;
        public UnityEngine.UI.GraphicRaycaster raycaster;
    }

    private readonly List<Lifted> lifted = new List<Lifted>();

    private void SpotlightOn(GameObject[] targets)
    {
        Canvas root = null;

        foreach (GameObject t in targets)
        {
            if (t == null)
                continue;

            Canvas c = t.GetComponentInParent<Canvas>(true);
            if (c != null)
            {
                root = c.rootCanvas;
                break;
            }
        }

        if (root == null)
            return;

        // One full-screen dark image, on top of everything in that canvas.
        if (dimImage == null || dimImage.canvas == null || dimImage.canvas.rootCanvas != root)
        {
            if (dimImage != null)
                Destroy(dimImage.gameObject);

            GameObject go = new GameObject("TutorialDim", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(root.transform, false);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            dimImage = go.GetComponent<UnityEngine.UI.Image>();
            dimImage.raycastTarget = false;   // never blocks taps or drags
            Color c0 = dimColor; c0.a = 0f;
            dimImage.color = c0;
        }

        dimImage.transform.SetAsLastSibling();

        // Lift the highlighted UI (and the instruction) above the darkening.
        int order = root.sortingOrder + 1;

        foreach (GameObject t in targets)
            Lift(t, order);

        if (instructionPanel != null)
            Lift(instructionPanel, order + 1);

        if (subtitlePanel != null)
            Lift(subtitlePanel, order + 1);

        dimWanted = true;
    }

    private void Lift(GameObject obj, int order)
    {
        if (obj == null)
            return;

        Lifted l = new Lifted();
        l.canvas = obj.GetComponent<Canvas>();

        if (l.canvas == null)
        {
            l.canvas = obj.AddComponent<Canvas>();
            l.added = true;
            l.raycaster = obj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        else
        {
            l.oldOverride = l.canvas.overrideSorting;
            l.oldOrder = l.canvas.sortingOrder;
        }

        l.canvas.overrideSorting = true;
        l.canvas.sortingOrder = order;
        lifted.Add(l);
    }

    private void SpotlightOff()
    {
        dimWanted = false;

        foreach (Lifted l in lifted)
        {
            if (l.canvas == null)
                continue;

            if (l.added)
            {
                if (l.raycaster != null)
                    Destroy(l.raycaster);

                Destroy(l.canvas);
            }
            else
            {
                l.canvas.overrideSorting = l.oldOverride;
                l.canvas.sortingOrder = l.oldOrder;
            }
        }

        lifted.Clear();
    }

    private void UpdateDim()
    {
        if (dimImage == null)
            return;

        // Unity can drop 'Override Sorting' when a panel is switched on/off,
        // so keep the lifted UI (instruction, subtitles, highlighted UI) on top.
        foreach (Lifted l in lifted)
        {
            if (l.canvas != null && l.canvas.isActiveAndEnabled && !l.canvas.overrideSorting)
                l.canvas.overrideSorting = true;
        }

        Color c = dimColor;
        float current = dimImage.color.a;
        c.a = Mathf.MoveTowards(current, dimWanted ? dimAmount : 0f, Time.unscaledDeltaTime / dimFadeTime);
        dimImage.color = c;
    }

    [Header("Locking")]
    [Tooltip("On: during a step only that step's UI can be used. All other tutorial " +
             "UI (shown earlier or still hidden) is locked until the game starts.")]
    [SerializeField] private bool lockOtherUIDuringStep = true;

    // Every UI the intro/tutorial knows about.
    private readonly HashSet<CanvasGroup> tutorialGroups = new HashSet<CanvasGroup>();

    private void RememberGroup(GameObject obj)
    {
        if (obj == null)
            return;

        CanvasGroup g = obj.GetComponent<CanvasGroup>();
        if (g == null)
            g = obj.AddComponent<CanvasGroup>();

        tutorialGroups.Add(g);
    }

    private void LockAllExcept(GameObject[] show, GameObject[] highlights)
    {
        foreach (GameObject o in show) RememberGroup(o);
        foreach (GameObject o in highlights) RememberGroup(o);

        foreach (CanvasGroup g in tutorialGroups)
        {
            if (g == null)
                continue;

            bool usable = IsOrContains(g.transform, show) || IsOrContains(g.transform, highlights);

            // Popping-in UI turns itself on when it has finished appearing.
            if (usable && g.alpha < 1f)
                continue;

            g.interactable = usable;
            g.blocksRaycasts = usable;
        }
    }

    // True if 't' is one of the objects, or a parent of one of them.
    private static bool IsOrContains(Transform t, GameObject[] objects)
    {
        foreach (GameObject o in objects)
            if (o != null && (o.transform == t || o.transform.IsChildOf(t)))
                return true;

        return false;
    }

    [Header("Highlight Pulse")]
    [Tooltip("On: the highlighted UI gently grows and shrinks.")]
    [SerializeField] private bool pulseHighlight = false;

    [Tooltip("How much highlighted UI grows while pulsing. 0.08 = 8% bigger.")]
    [SerializeField, Range(0f, 0.3f)] private float pulseAmount = 0.08f;

    [Tooltip("Pulses per second.")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 1.2f;

    private readonly Dictionary<Transform, Vector3> pulseStartScales = new Dictionary<Transform, Vector3>();

    private IEnumerator Pulse(GameObject[] objects)
    {
        // Wait until the UI has finished popping in.
        float wait = 0f;
        while (tutorialAppear == AppearStyle.PopUp && wait < popUpTime)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        foreach (GameObject o in objects)
            if (o != null && !pulseStartScales.ContainsKey(o.transform))
                pulseStartScales[o.transform] = NormalScale(o.transform);

        float time = 0f;

        while (true)
        {
            time += Time.unscaledDeltaTime;
            float grow = 1f + pulseAmount * (0.5f - 0.5f * Mathf.Cos(time * pulseSpeed * Mathf.PI * 2f));

            foreach (GameObject o in objects)
                if (o != null && pulseStartScales.TryGetValue(o.transform, out Vector3 start))
                    o.transform.localScale = start * grow;

            yield return null;
        }
    }

    private void ResetPulse(GameObject[] objects)
    {
        foreach (GameObject o in objects)
        {
            if (o != null && pulseStartScales.TryGetValue(o.transform, out Vector3 start))
            {
                o.transform.localScale = start;
                pulseStartScales.Remove(o.transform);
            }
        }
    }

    private void UpdateInstructionPanel()
    {
        if (instructionGroup == null)
            return;

        if (showInstruction && !instructionPanel.activeSelf)
            instructionPanel.SetActive(true);

        instructionGroup.alpha = Mathf.MoveTowards(
            instructionGroup.alpha, showInstruction ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);

        if (!IsPlaying && !showInstruction && instructionGroup.alpha <= 0f && instructionPanel.activeSelf)
            instructionPanel.SetActive(false);
    }

    // Fades in UI objects that were hidden by 'Hide During Intro'.
    private IEnumerator FadeInObjects(GameObject[] objects)
    {
        if (tutorialAppear == AppearStyle.PopUp)
        {
            yield return PopInObjects(objects);
            yield break;
        }

        List<CanvasGroup> groups = new List<CanvasGroup>();

        foreach (GameObject obj in objects)
        {
            if (obj == null)
                continue;

            CanvasGroup g = obj.GetComponent<CanvasGroup>();
            if (g == null)
                g = obj.AddComponent<CanvasGroup>();

            groups.Add(g);
        }

        float a = groups.Count > 0 ? groups[0].alpha : 1f;

        while (a < 1f)
        {
            a = Mathf.MoveTowards(a, 1f, Time.unscaledDeltaTime / uiFadeInTime);

            foreach (CanvasGroup g in groups)
            {
                g.alpha = Mathf.Max(g.alpha, a);
                g.interactable = a >= 1f;
                g.blocksRaycasts = a >= 1f;
            }

            yield return null;
        }

        foreach (CanvasGroup g in groups)
        {
            g.alpha = 1f;
            g.interactable = true;
            g.blocksRaycasts = true;
        }
    }

    // Grows the UI in from nothing with a small bounce.
    private IEnumerator PopInObjects(GameObject[] objects)
    {
        List<CanvasGroup> groups = new List<CanvasGroup>();
        List<Transform> transforms = new List<Transform>();

        foreach (GameObject obj in objects)
        {
            if (obj == null)
                continue;

            CanvasGroup g = obj.GetComponent<CanvasGroup>();
            if (g == null)
                g = obj.AddComponent<CanvasGroup>();

            // Already visible: leave it alone.
            if (g.alpha >= 1f)
                continue;

            NormalScale(obj.transform);
            obj.transform.localScale = Vector3.zero;
            g.alpha = 1f;
            g.interactable = false;
            g.blocksRaycasts = false;

            groups.Add(g);
            transforms.Add(obj.transform);
        }

        float t = 0f;

        while (t < 1f)
        {
            t = Mathf.MoveTowards(t, 1f, Time.unscaledDeltaTime / popUpTime);

            // Ease out back: goes a bit past full size, then settles.
            float u = t - 1f;
            float size = 1f + (popUpBounce + 1f) * u * u * u + popUpBounce * u * u;

            foreach (Transform tr in transforms)
                if (tr != null)
                    tr.localScale = NormalScale(tr) * Mathf.Max(0f, size);

            yield return null;
        }

        foreach (Transform tr in transforms)
            if (tr != null)
                tr.localScale = NormalScale(tr);

        foreach (CanvasGroup g in groups)
        {
            g.interactable = true;
            g.blocksRaycasts = true;
        }
    }

    // Puts the camera at 'eased' (0-1) along the curved path.
    private static void Place(Transform cam, List<Vector3> points, List<Quaternion> rotations,
                              float[] distanceAt, float totalDistance, float eased)
    {
        float d = eased * totalDistance;
        int i = 0;

        while (i < points.Count - 2 && d > distanceAt[i + 1])
            i++;

        float segment = Mathf.Max(distanceAt[i + 1] - distanceAt[i], 0.0001f);
        float s = Mathf.Clamp01((d - distanceAt[i]) / segment);

        Vector3 p0 = points[Mathf.Max(i - 1, 0)];
        Vector3 p1 = points[i];
        Vector3 p2 = points[i + 1];
        Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];

        cam.position = CatmullRom(p0, p1, p2, p3, s);
        cam.rotation = Quaternion.Slerp(rotations[i], rotations[i + 1], s * s * (3f - 2f * s));
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (2f * p1 + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // =========================================================
    // NARRATION + SUBTITLES
    // =========================================================

    private void PlayNarration()
    {
        PlayClip(narrationClip);
        activeSubtitles = subtitles;
    }

    // The subtitles belonging to the clip that is playing now.
    private List<SubtitleLine> activeSubtitles;

    private void PlayClip(AudioClip clip)
    {
        if (narrationSource == null)
        {
            narrationSource = gameObject.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.spatialBlend = 0f;
        }

        narrationSource.clip = clip;
        narrationSource.volume = narrationVolume;
        narrationSource.outputAudioMixerGroup = narrationMixerGroup;
        narrationSource.Play();
    }

    private void UpdateSubtitles()
    {
        if (subtitlePanel == null || subtitleText == null)
            return;

        // After the intro/tutorial the panel belongs to the other scripts again.
        if (!IsPlaying)
            return;

        string current = null;

        List<SubtitleLine> lines = activeSubtitles;

        if (narrationSource != null && narrationSource.isPlaying &&
            narrationSource.clip != null && lines != null)
        {
            float time = narrationSource.time;
            float clipLength = narrationSource.clip.length;

            for (int i = 0; i < lines.Count; i++)
            {
                SubtitleLine line = lines[i];

                if (line == null || string.IsNullOrWhiteSpace(line.text))
                    continue;

                float end = line.endTime > line.startTime
                    ? line.endTime
                    : (i + 1 < lines.Count && lines[i + 1] != null
                        ? lines[i + 1].startTime
                        : clipLength);

                if (time >= line.startTime && time < end)
                {
                    current = line.text;
                    break;
                }
            }
        }

        if (current != null)
        {
            if (!subtitlePanel.activeSelf)
                subtitlePanel.SetActive(true);

            subtitleText.text = current;
        }

        subtitleGroup.alpha = Mathf.MoveTowards(
            subtitleGroup.alpha, current != null ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);

        // Keep the panel switched ON while the intro runs (only fade it), so it
        // doesn't flicker on/off between lines and keeps its place above the dark layer.
        if (!IsPlaying && current == null && subtitleGroup.alpha <= 0f && subtitlePanel.activeSelf)
            subtitlePanel.SetActive(false);
    }

    // =========================================================
    // UI + FINISH
    // =========================================================

    // ---------- Finding scene objects by name (for a prefab Intro) ----------

    private static GameObject FindInScene(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.scene.IsValid() && t.name == objectName.Trim())
                return t.gameObject;
        }

        Debug.LogWarning("IntroSequence: no object named '" + objectName + "' found in the scene.");
        return null;
    }

    private static GameObject[] Combine(GameObject[] objects, string[] names)
    {
        List<GameObject> all = new List<GameObject>();

        if (objects != null)
            foreach (GameObject o in objects)
                if (o != null && !all.Contains(o)) all.Add(o);

        if (names != null)
            foreach (string n in names)
            {
                GameObject o = FindInScene(n);
                if (o != null && !all.Contains(o)) all.Add(o);
            }

        return all.ToArray();
    }

    private void PrepareUI()
    {
        hideDuringIntro = Combine(hideDuringIntro, hideDuringIntroNames);

        int count = hideDuringIntro != null ? hideDuringIntro.Length : 0;
        uiGroups = new CanvasGroup[count];

        for (int i = 0; i < count; i++)
        {
            if (hideDuringIntro[i] == null)
                continue;

            uiGroups[i] = hideDuringIntro[i].GetComponent<CanvasGroup>();
            if (uiGroups[i] == null)
                uiGroups[i] = hideDuringIntro[i].AddComponent<CanvasGroup>();

            tutorialGroups.Add(uiGroups[i]);

            if (playIntro)
            {
                uiGroups[i].alpha = 0f;
                uiGroups[i].interactable = false;
                uiGroups[i].blocksRaycasts = false;
            }
        }
    }

    private void Finish(bool instant)
    {
        IsPlaying = false;
        IsNarrating = false;

        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);

        showInstruction = false;

        if (instant)
            SetUI(1f);
        else
            StartCoroutine(FadeInUI());

        Debug.Log("IntroSequence: Intro finished. Game starts.");
        onIntroFinished?.Invoke();
    }

    private IEnumerator FadeInUI()
    {
        float a = 0f;

        while (a < 1f)
        {
            a = Mathf.MoveTowards(a, 1f, Time.unscaledDeltaTime / uiFadeInTime);
            SetUI(a);
            yield return null;
        }
    }

    private void SetUI(float alpha)
    {
        foreach (CanvasGroup g in uiGroups)
        {
            if (g == null)
                continue;

            // UI already shown by a tutorial step stays visible.
            g.alpha = Mathf.Max(g.alpha, alpha);
            g.interactable = g.alpha >= 1f;
            g.blocksRaycasts = g.alpha >= 1f;
        }

        // The game is starting: unlock everything the tutorial locked.
        if (alpha >= 1f)
        {
            foreach (CanvasGroup g in tutorialGroups)
            {
                if (g == null || g.alpha < 1f)
                    continue;

                g.interactable = true;
                g.blocksRaycasts = true;
            }
        }
    }
}