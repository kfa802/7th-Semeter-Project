using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum SceneChangeTiming
{
    WhenShown,
    ChoiceA,
    ChoiceB,
    ChoiceC,
    AnyChoice
}

public enum RevealStyle
{
    Instant,
    RiseFromGround,   // Rises up and settles with a small bounce.
    GrowUp,           // Scales up from nothing with a little overshoot.
    PopUp,            // Quick, bouncy pop (good for small props).
    SproutUp          // Stretches up from the ground like a plant/building growing.
}

public enum RevealOrder
{
    ListOrder,        // In the order of the list / hierarchy.
    Random,           // Shuffled.
    FromCenterOut     // Nearest to the group's centre first, spreading outwards.
}

[System.Serializable]
public class DilemmaSceneChange
{
    [Tooltip("A note for yourself, e.g. 'City + road A'.")]
    public string label;

    [Tooltip("The dilemma this change belongs to.")]
    public Dilemma dilemma;

    [Tooltip("When the change happens: when the dilemma appears, or after a specific choice.")]
    public SceneChangeTiming when = SceneChangeTiming.AnyChoice;

    [Tooltip("Seconds to wait before the change starts.")]
    public float startDelay = 0f;

    [Header("Objects")]
    [Tooltip("Hidden objects in the scene to reveal.")]
    public List<GameObject> objectsToShow = new List<GameObject>();

    [Tooltip("Objects to hide (e.g. trees where the city goes).")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    [Header("Reveal")]
    public RevealStyle revealStyle = RevealStyle.RiseFromGround;

    [Tooltip("Seconds each object takes to appear.")]
    public float revealDuration = 2f;

    [Tooltip("How far below its final position an object starts (Rise From Ground).")]
    public float riseDistance = 5f;

    [Tooltip("Reveal each child of an object one by one (e.g. building by building).")]
    public bool revealChildrenOneByOne = true;

    [Tooltip("Seconds between each object or child starting to appear.")]
    public float delayBetweenObjects = 0.15f;

    [Header("Reveal Feel")]
    [Tooltip("Order the objects/children appear in.")]
    public RevealOrder revealOrder = RevealOrder.FromCenterOut;

    [Tooltip("How much it overshoots before settling. 0 = no bounce, 1.7 = normal, 3 = very bouncy.")]
    [Range(0f, 4f)]
    public float overshoot = 1.7f;

    [Tooltip("Random variation of duration and spacing, so objects don't all move identically. 0.25 = ±25%.")]
    [Range(0f, 0.6f)]
    public float randomness = 0.25f;

    [Tooltip("Small wobble (degrees) that fades out as the object settles. 0 = off.")]
    [Range(0f, 20f)]
    public float wobbleAngle = 4f;

    [Tooltip("Optional: effect spawned at each object's base when it appears (e.g. a dust puff or leaves).")]
    public GameObject spawnEffectPrefab;

    [Tooltip("Seconds before the spawned effect is removed.")]
    public float spawnEffectLifetime = 3f;
}

public class DilemmaManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;

    [Tooltip("Drag your DilemmaPanel GameObject here.")]
    [SerializeField] private GameObject dilemmaPanel;

    [Tooltip("Drag the GameObject with your EndingUI component here.")]
    [SerializeField] private GameObject endingPanel;

    [Header("Dilemmas")]
    [SerializeField] private List<Dilemma> dilemmas = new();

    [Header("Scene Changes")]
    [Tooltip("Objects to show or hide in the scene when a dilemma appears or a choice is made.")]
    [SerializeField] private List<DilemmaSceneChange> sceneChanges = new();

    [Header("Travel Integration")]
    public UnityEvent<string> onTravelRequested;

    [Header("Ending Settings")]
    [SerializeField] private float endingDelay = 3f;

    private DilemmaUI dilemmaUI;
    private EndingUI endingUI;

    private readonly HashSet<Dilemma> completedDilemmas = new();

    private readonly Dictionary<Dilemma, int> resolvedChoices = new();

    private readonly Dictionary<Dilemma, float> scheduledDilemmas = new();

    private readonly Dictionary<Dilemma, float> temperatureStartTimes = new();

    private Dilemma activeDilemma;

    private string currentZone = "";
    private float zoneEnteredTime;

    private bool gameEnded;
    private Ending pendingEnding;
    private int activeDilemmaNumber;
    private readonly HashSet<GameEvent> executedEvents = new();
    public Ending ReachedEnding => pendingEnding;

    public bool HasResolved(Dilemma dilemma) =>
        dilemma != null && resolvedChoices.ContainsKey(dilemma);

    public bool TryGetChoice(Dilemma dilemma, out int choice)
    {
        choice = -1;
        return dilemma != null && resolvedChoices.TryGetValue(dilemma, out choice);
    }

    public bool HasExecuted(GameEvent gameEvent) =>
        gameEvent != null && executedEvents.Contains(gameEvent);

    private void Awake()
    {
        if (dilemmaPanel != null)
            dilemmaUI = dilemmaPanel.GetComponent<DilemmaUI>();

        if (dilemmaUI == null)
        {
            Debug.LogError(
                "DilemmaManager: Assign a DilemmaPanel with DilemmaUI."
            );
        }

        if (endingPanel != null)
        {
            endingUI = endingPanel.GetComponent<EndingUI>();
            endingPanel.SetActive(false);
        }

        if (endingPanel != null && endingUI == null)
        {
            Debug.LogError(
                "DilemmaManager: EndingPanel needs an EndingUI component."
            );
        }

        if (onTravelRequested == null)
        {
            Debug.LogError(
                "DilemmaManager: Travel UnityEvent is missing."
            );
        }
        else
        {
            Debug.Log(
                "DilemmaManager: Travel event has " +
                onTravelRequested.GetPersistentEventCount() +
                " Inspector listener(s)."
            );
        }
    }

    private void Start()
    {
        TelemetryManager.Instance?.DeclareDilemmas(dilemmas.Count);
    }

    private void Update()
    {
        if (gameEnded || environment == null || dilemmaUI == null)
            return;

        if (activeDilemma != null)
            return;

        string zoneName = environment.ActiveZoneName;
        bool zoneChanged = zoneName != currentZone;

        if (zoneChanged)
        {
            currentZone = zoneName;
            zoneEnteredTime = Time.time;

            Debug.Log(
                "DilemmaManager: Active zone changed to " + zoneName
            );
        }

        foreach (Dilemma dilemma in dilemmas)
        {
            if (dilemma == null ||
                completedDilemmas.Contains(dilemma))
                continue;

            // A blocking choice makes this dilemma unavailable.
            if (IsBlockedByAnyDilemma(dilemma))
            {
                scheduledDilemmas.Remove(dilemma);
                temperatureStartTimes.Remove(dilemma);
                continue;
            }

            bool isScheduled =
                scheduledDilemmas.TryGetValue(
                    dilemma,
                    out float scheduledTime
                );

            // A dilemma with prerequisites must first be unlocked.
            if (HasPrerequisites(dilemma) && !isScheduled)
                continue;

            if (!IsInRequiredZone(dilemma))
            {
                temperatureStartTimes.Remove(dilemma);
                continue;
            }

            if (isScheduled && Time.time < scheduledTime)
                continue;

            if (!ShouldTrigger(dilemma, zoneChanged))
                continue;

            if (isScheduled)
                scheduledDilemmas.Remove(dilemma);

            Debug.Log(
                "DilemmaManager: Triggering " + dilemma.name +
                " | Trigger Type = " + dilemma.triggerType +
                " | Temperature = " + environment.Temperature +
                " | Threshold = " + dilemma.temperatureThreshold
            );

            ShowDilemma(dilemma);
            return;
        }
    }

    private bool HasPrerequisites(Dilemma dilemma)
    {
        if (dilemma.prerequisiteDilemmas != null &&
            dilemma.prerequisiteDilemmas.Count > 0)
        {
            return true;
        }

        // Backward compatibility with the old single prerequisite.
        return dilemma.prerequisiteDilemma != null;
    }

    private bool IsInRequiredZone(Dilemma dilemma)
    {
        if (string.IsNullOrWhiteSpace(dilemma.requiredZoneName))
            return true;

        return environment.ActiveZoneName == dilemma.requiredZoneName;
    }

    private bool IsBlockedByAnyDilemma(Dilemma dilemma)
    {
        if (dilemma.blockedByDilemmas == null)
            return false;

        foreach (DilemmaBlockRule rule in dilemma.blockedByDilemmas)
        {
            if (rule == null || rule.dilemma == null)
                continue;

            // The blocking dilemma must have been resolved.
            if (!resolvedChoices.TryGetValue(
                    rule.dilemma,
                    out int selectedChoice))
            {
                continue;
            }

            if (ChoiceMatches(rule.requiredChoice, selectedChoice))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldTrigger(Dilemma dilemma, bool zoneChanged)
    {
        switch (dilemma.triggerType)
        {
            case DilemmaTriggerType.ZoneDelay:
                return Time.time - zoneEnteredTime >=
                       dilemma.delaySeconds;

            case DilemmaTriggerType.ZoneEntered:
                return zoneChanged;

            case DilemmaTriggerType.WaterBelow:
                return environment.Water < dilemma.threshold;

            case DilemmaTriggerType.PollutionAbove:
                return environment.Pollution > dilemma.threshold;

            case DilemmaTriggerType.BambooBelow:
                {
                    if (BambooManager.Instance == null ||
                        environment.ActiveZone == null)
                    {
                        return false;
                    }

                    EcosystemZone zone = environment.ActiveZone;

                    int capacity =
                        BambooManager.Instance.GetCapacityForZone(zone);

                    if (capacity <= 0)
                    {
                        Debug.LogWarning(
                            "BambooBelow: No growth spots registered for zone " +
                            zone.zoneName
                        );

                        return false;
                    }

                    float bambooPercent =
                        BambooManager.Instance.GetPercentForZone(zone);

                    return bambooPercent < dilemma.threshold;
                }

            case DilemmaTriggerType.TemperatureTooLow:
                {
                    bool belowThreshold =
                        environment.Temperature < dilemma.temperatureThreshold;

                    DebugTemperatureCondition(dilemma, belowThreshold);

                    return CheckTemperatureDuration(
                        dilemma,
                        belowThreshold
                    );
                }

            case DilemmaTriggerType.TemperatureTooHigh:
                {
                    bool aboveThreshold =
                        environment.Temperature > dilemma.temperatureThreshold;

                    DebugTemperatureCondition(dilemma, aboveThreshold);

                    return CheckTemperatureDuration(
                        dilemma,
                        aboveThreshold
                    );
                }

            case DilemmaTriggerType.TemperatureModerate:
                {
                    bool isModerate =
                        environment.Temperature >= dilemma.temperatureThreshold &&
                        environment.Temperature <= dilemma.temperatureHighThreshold;

                    return CheckTemperatureDuration(dilemma, isModerate);
                }

            default:
                return false;
        }
    }

    private void DebugTemperatureCondition(
        Dilemma dilemma,
        bool conditionIsMet)
    {
        if (!conditionIsMet)
            return;
    }

    private bool CheckTemperatureDuration(
        Dilemma dilemma,
        bool conditionIsMet)
    {
        if (!conditionIsMet)
        {
            temperatureStartTimes.Remove(dilemma);
            return false;
        }

        if (!temperatureStartTimes.TryGetValue(
                dilemma,
                out float startTime))
        {
            temperatureStartTimes[dilemma] = Time.time;
            return dilemma.temperatureDurationSeconds <= 0f;
        }

        float elapsedTime = Time.time - startTime;

        return elapsedTime >= dilemma.temperatureDurationSeconds;
    }

    private void ShowDilemma(Dilemma dilemma)
    {
        activeDilemma = dilemma;
        activeDilemmaNumber = dilemmas.IndexOf(dilemma) + 1;

        Debug.Log("DilemmaManager: Showing " + dilemma.name);

        dilemmaUI.ShowDilemma(dilemma, this);

        TelemetryManager.Instance?.DilemmaShown(activeDilemmaNumber, dilemma.name);

        // The buttons are live immediately for now. When narration gates the
        // buttons, call LogChoicesAvailable() instead; it overwrites this value.
        TelemetryManager.Instance?.DilemmaChoiceAvailable(activeDilemmaNumber);

        // Scene changes set to "When Shown".
        ApplySceneChanges(dilemma, -1);
    }

    public void ChooseA()
    {
        ResolveDilemma(0);
    }

    public void ChooseB()
    {
        ResolveDilemma(1);
    }

    public void ChooseC()
    {
        ResolveDilemma(2);
    }

    public void LogNarrationStarted() =>
    TelemetryManager.Instance?.DilemmaNarrationStart(activeDilemmaNumber);

    public void LogNarrationEnded() =>
        TelemetryManager.Instance?.DilemmaNarrationEnd(activeDilemmaNumber);

    public void LogChoicesAvailable() =>
        TelemetryManager.Instance?.DilemmaChoiceAvailable(activeDilemmaNumber);

    private void ResolveDilemma(int choice)
    {
        if (activeDilemma == null || gameEnded)
        {
            Debug.LogWarning(
                "DilemmaManager: Choice ignored. No active dilemma " +
                "or the game has ended."
            );
            return;
        }

        if (choice < 0 || choice > 2)
        {
            Debug.LogError(
                "DilemmaManager: Invalid choice index " + choice
            );
            return;
        }

        Dilemma resolvedDilemma = activeDilemma;

        string choiceName = choice == 0 ? "A" :
                            choice == 1 ? "B" : "C";

        Debug.Log(
            "DilemmaManager: " + resolvedDilemma.name +
            " resolved with Choice " + choiceName
        );

        int number = activeDilemmaNumber;
        string choiceText = choice == 0 ? resolvedDilemma.choiceA
                  : choice == 1 ? resolvedDilemma.choiceB
                  : resolvedDilemma.choiceC;

        TelemetryManager.Instance?.DilemmaChoice(number, choiceText);

        completedDilemmas.Add(resolvedDilemma);
        resolvedChoices[resolvedDilemma] = choice;

        temperatureStartTimes.Remove(resolvedDilemma);
        scheduledDilemmas.Remove(resolvedDilemma);

        GameEvent gameEvent = choice == 0
            ? resolvedDilemma.choiceAEvent
            : choice == 1
                ? resolvedDilemma.choiceBEvent
                : resolvedDilemma.choiceCEvent;

        Ending selectedEnding = choice == 0
            ? resolvedDilemma.choiceAEnding
            : choice == 1
                ? resolvedDilemma.choiceBEnding
                : resolvedDilemma.choiceCEnding;

        activeDilemma = null;
        dilemmaUI.HideDilemma();

        TelemetryManager.Instance?.DilemmaEnded(number);

        // Scene changes for this choice (e.g. city and roads).
        ApplySceneChanges(resolvedDilemma, choice);

        // Remove pending schedules for dilemmas that this choice blocks.
        foreach (Dilemma candidate in dilemmas)
        {
            if (candidate == null)
                continue;

            if (IsBlockedByAnyDilemma(candidate))
            {
                if (scheduledDilemmas.Remove(candidate))
                {
                    Debug.Log(
                        "DilemmaManager: " + candidate.name +
                        " removed from its schedule because it is blocked."
                    );
                }

                temperatureStartTimes.Remove(candidate);
            }
        }

        ExecuteEvent(gameEvent);

        if (selectedEnding != null)
        {
            BeginEnding(selectedEnding);
            return;
        }

        // Schedule any follow-up unlocked by this choice.
        foreach (Dilemma nextDilemma in dilemmas)
        {
            if (nextDilemma == null ||
                completedDilemmas.Contains(nextDilemma) ||
                scheduledDilemmas.ContainsKey(nextDilemma))
            {
                continue;
            }

            // Do not schedule a dilemma that has already been blocked.
            if (IsBlockedByAnyDilemma(nextDilemma))
                continue;

            if (!MatchesAnyPrerequisite(
                    nextDilemma,
                    resolvedDilemma,
                    choice))
            {
                continue;
            }

            scheduledDilemmas[nextDilemma] =
                Time.time + Mathf.Max(0f, nextDilemma.delaySeconds);

            temperatureStartTimes.Remove(nextDilemma);

            Debug.Log(
                nextDilemma.name + " unlocked by " +
                resolvedDilemma.name + " (Choice " + choiceName + ")" +
                " and scheduled in " +
                nextDilemma.delaySeconds + " seconds."
            );
        }
    }

    // =========================================================
    // SCENE CHANGES (city, roads, ...)
    // =========================================================

    // choice = -1 means the dilemma was just shown.
    private void ApplySceneChanges(Dilemma dilemma, int choice)
    {
        foreach (DilemmaSceneChange change in sceneChanges)
        {
            if (change == null || change.dilemma != dilemma)
                continue;

            if (!TimingMatches(change.when, choice))
                continue;

            Debug.Log(
                "DilemmaManager: Applying scene change '" +
                change.label + "' for " + dilemma.name
            );

            StartCoroutine(RunSceneChange(change));
        }
    }

    private bool TimingMatches(SceneChangeTiming timing, int choice)
    {
        if (choice < 0)
            return timing == SceneChangeTiming.WhenShown;

        return timing == SceneChangeTiming.AnyChoice ||
               (timing == SceneChangeTiming.ChoiceA && choice == 0) ||
               (timing == SceneChangeTiming.ChoiceB && choice == 1) ||
               (timing == SceneChangeTiming.ChoiceC && choice == 2);
    }

    private IEnumerator RunSceneChange(DilemmaSceneChange change)
    {
        if (change.startDelay > 0f)
            yield return new WaitForSeconds(change.startDelay);

        foreach (GameObject obj in change.objectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Collect everything that should appear, one entry per object/child.
        List<GameObject> toReveal = new List<GameObject>();

        foreach (GameObject obj in change.objectsToShow)
        {
            if (obj == null || obj.activeSelf)
                continue;

            if (change.revealChildrenOneByOne && obj.transform.childCount > 0)
            {
                // Hide the children, show the parent, then reveal
                // each child in turn (e.g. building by building).
                foreach (Transform child in obj.transform)
                {
                    if (!child.gameObject.activeSelf)
                        continue;

                    toReveal.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }

                obj.SetActive(true);
            }
            else
            {
                toReveal.Add(obj);
            }
        }

        SortForReveal(toReveal, change.revealOrder);

        foreach (GameObject obj in toReveal)
        {
            StartCoroutine(RevealObject(obj, change));

            if (change.delayBetweenObjects > 0f)
            {
                float spacing = change.delayBetweenObjects * Random.Range(
                    1f - change.randomness,
                    1f + change.randomness
                );

                yield return new WaitForSeconds(Mathf.Max(0f, spacing));
            }
        }
    }

    private void SortForReveal(List<GameObject> objects, RevealOrder order)
    {
        if (objects.Count < 2)
            return;

        if (order == RevealOrder.Random)
        {
            for (int i = objects.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (objects[i], objects[j]) = (objects[j], objects[i]);
            }
        }
        else if (order == RevealOrder.FromCenterOut)
        {
            Vector3 center = Vector3.zero;

            foreach (GameObject obj in objects)
                center += obj.transform.position;

            center /= objects.Count;

            objects.Sort((a, b) =>
                (a.transform.position - center).sqrMagnitude.CompareTo(
                (b.transform.position - center).sqrMagnitude));
        }
    }

    private IEnumerator RevealObject(GameObject obj, DilemmaSceneChange change)
    {
        Transform target = obj.transform;

        if (change.revealStyle == RevealStyle.Instant ||
            change.revealDuration <= 0f)
        {
            obj.SetActive(true);
            SpawnRevealEffect(change, target.position);
            yield break;
        }

        Vector3 endPosition = target.position;
        Vector3 endScale = target.localScale;
        Quaternion endRotation = target.localRotation;

        // Every object gets a slightly different duration and wobble direction.
        float duration = change.revealDuration * Random.Range(
            1f - change.randomness,
            1f + change.randomness
        );

        duration = Mathf.Max(0.05f, duration);

        Vector3 wobbleAxis = new Vector3(
            Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)
        ).normalized;

        if (wobbleAxis == Vector3.zero)
            wobbleAxis = Vector3.right;

        // Apply the first frame before showing, so nothing flashes at full size.
        ApplyReveal(target, change, 0f, endPosition, endScale, endRotation, wobbleAxis);
        obj.SetActive(true);

        SpawnRevealEffect(change, endPosition);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (obj == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            ApplyReveal(target, change, t, endPosition, endScale, endRotation, wobbleAxis);

            yield return null;
        }

        if (obj != null)
        {
            target.position = endPosition;
            target.localScale = endScale;
            target.localRotation = endRotation;
        }
    }

    // Sets position/scale/rotation for a point t (0..1) in the reveal.
    private void ApplyReveal(
        Transform target,
        DilemmaSceneChange change,
        float t,
        Vector3 endPosition,
        Vector3 endScale,
        Quaternion endRotation,
        Vector3 wobbleAxis)
    {
        float s = change.overshoot;

        Vector3 position = endPosition;
        Vector3 scale = endScale;

        switch (change.revealStyle)
        {
            case RevealStyle.RiseFromGround:
            {
                // Rises past its spot a little, then settles down.
                float rise = EaseOutBack(t, s * 0.6f);
                position = endPosition - Vector3.up * change.riseDistance * (1f - rise);
                break;
            }

            case RevealStyle.GrowUp:
            {
                // Smooth grow with a soft overshoot.
                float grow = EaseOutBack(t, s * 0.7f);
                scale = endScale * Mathf.Max(0.001f, grow);
                break;
            }

            case RevealStyle.PopUp:
            {
                // Fast and bouncy: most of the growth happens early.
                float pop = EaseOutBack(EaseOutCubic(t), s * 1.4f);
                scale = endScale * Mathf.Max(0.001f, pop);
                break;
            }

            case RevealStyle.SproutUp:
            {
                // Height shoots up first, width follows a little later,
                // which reads as "growing out of the ground".
                float height = EaseOutBack(t, s);
                float width = EaseOutBack(Mathf.Clamp01(t * 1.15f - 0.1f), s * 0.5f);

                scale = new Vector3(
                    endScale.x * Mathf.Lerp(0.35f, 1f, Mathf.Max(0f, width)),
                    endScale.y * Mathf.Max(0.001f, height),
                    endScale.z * Mathf.Lerp(0.35f, 1f, Mathf.Max(0f, width))
                );
                break;
            }
        }

        target.position = position;
        target.localScale = scale;

        // Wobble that fades out as the object settles.
        float wobble = change.wobbleAngle *
                       Mathf.Sin(t * Mathf.PI * 3f) *
                       (1f - t);

        target.localRotation = endRotation * Quaternion.AngleAxis(wobble, wobbleAxis);
    }

    private void SpawnRevealEffect(DilemmaSceneChange change, Vector3 position)
    {
        if (change.spawnEffectPrefab == null)
            return;

        GameObject effect = Instantiate(
            change.spawnEffectPrefab,
            position,
            Quaternion.identity
        );

        if (change.spawnEffectLifetime > 0f)
            Destroy(effect, change.spawnEffectLifetime);
    }

    // Goes slightly past 1 and comes back: a natural "settle" bounce.
    private static float EaseOutBack(float t, float overshoot)
    {
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    // =========================================================
    // PREREQUISITES / ENDINGS / EVENTS
    // =========================================================

    private bool MatchesAnyPrerequisite(
        Dilemma nextDilemma,
        Dilemma resolvedDilemma,
        int choice)
    {
        if (nextDilemma.prerequisiteDilemmas != null &&
            nextDilemma.prerequisiteDilemmas.Count > 0)
        {
            foreach (DilemmaPrerequisite prerequisite
                     in nextDilemma.prerequisiteDilemmas)
            {
                if (prerequisite == null ||
                    prerequisite.dilemma != resolvedDilemma)
                {
                    continue;
                }

                if (ChoiceMatches(prerequisite.requiredChoice, choice))
                    return true;
            }

            return false;
        }

        // Backward compatibility with the old single prerequisite.
        return nextDilemma.prerequisiteDilemma == resolvedDilemma &&
               ChoiceMatches(nextDilemma.prerequisiteChoice, choice);
    }

    private bool ChoiceMatches(
        PrerequisiteChoice requiredChoice,
        int selectedChoice)
    {
        return requiredChoice == PrerequisiteChoice.Either ||
               (requiredChoice == PrerequisiteChoice.ChoiceA &&
                selectedChoice == 0) ||
               (requiredChoice == PrerequisiteChoice.ChoiceB &&
                selectedChoice == 1) ||
               (requiredChoice == PrerequisiteChoice.ChoiceC &&
                selectedChoice == 2);
    }

    private void BeginEnding(Ending ending)
    {
        TelemetryManager.Instance?.EndingReached(ending.endingTitle);
        gameEnded = true;
        pendingEnding = ending;

        Debug.Log(
            "DilemmaManager: Ending '" + ending.endingTitle +
            "' will appear in " + endingDelay + " seconds."
        );

        Invoke(
            nameof(ShowSelectedEnding),
            Mathf.Max(0f, endingDelay)
        );
    }

    private void ShowSelectedEnding()
    {

        if (pendingEnding == null)
        {
            Debug.LogError("DilemmaManager: No ending was selected.");
            return;
        }

        if (endingUI != null && endingPanel != null)
        {
            endingPanel.SetActive(true);
            endingUI.ShowEnding(pendingEnding);
        }
        else
        {
            Debug.LogError(
                "DilemmaManager: Assign an EndingPanel with EndingUI."
            );
        }

        Debug.Log(
            "DilemmaManager: Displaying ending " +
            pendingEnding.endingTitle
        );
    }

    private void ExecuteEvent(GameEvent gameEvent)
    {
        if (gameEvent == null)
        {
            Debug.LogWarning(
                "DilemmaManager: No GameEvent assigned to the selected " +
                "choice. Check the choice's GameEvent field if effects " +
                "were expected."
            );
            return;
        }

        executedEvents.Add(gameEvent);

        Debug.Log(
            "DilemmaManager: Executing GameEvent '" + gameEvent.name +
            "' | RequestTravel = " + gameEvent.requestTravel +
            " | TargetZoneName = '" + gameEvent.targetZoneName + "'"
        );

        if (environment != null)
        {
            environment.ChangeTemperature(gameEvent.temperatureChange, "event");
            environment.ChangePollution(gameEvent.pollutionChange);
        }
        else
        {
            Debug.LogWarning(
                "DilemmaManager: Environment reference is missing."
            );
        }

        if (!gameEvent.requestTravel)
        {
            Debug.Log(
                "DilemmaManager: Travel was NOT requested by this GameEvent."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(gameEvent.targetZoneName))
        {
            Debug.LogError(
                "DilemmaManager: Travel requested, but Target Zone Name " +
                "is empty!"
            );
            return;
        }

        if (onTravelRequested == null)
        {
            Debug.LogError(
                "DilemmaManager: onTravelRequested UnityEvent is NULL."
            );
            return;
        }

        int listenerCount =
            onTravelRequested.GetPersistentEventCount();

        Debug.Log(
            "DilemmaManager: Requesting travel to '" +
            gameEvent.targetZoneName + "'. Inspector listeners = " +
            listenerCount
        );

        if (listenerCount == 0)
        {
            Debug.LogError(
                "DilemmaManager: No travel listener configured! " +
                "Connect PandaPathMover.TravelToZone(string) " +
                "in the Inspector."
            );
            return;
        }

        onTravelRequested.Invoke(gameEvent.targetZoneName);

        Debug.Log(
            "DilemmaManager: Travel event invoked for '" +
            gameEvent.targetZoneName + "'."
        );
    }
}