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
    RiseFromGround,
    GrowUp
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

        foreach (GameObject obj in change.objectsToShow)
        {
            if (obj == null || obj.activeSelf)
                continue;

            if (change.revealChildrenOneByOne && obj.transform.childCount > 0)
            {
                // Hide the children, show the parent, then reveal
                // each child in turn (e.g. building by building).
                List<GameObject> children = new List<GameObject>();

                foreach (Transform child in obj.transform)
                {
                    if (!child.gameObject.activeSelf)
                        continue;

                    children.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }

                obj.SetActive(true);

                foreach (GameObject child in children)
                {
                    StartCoroutine(RevealObject(child, change));

                    if (change.delayBetweenObjects > 0f)
                        yield return new WaitForSeconds(change.delayBetweenObjects);
                }
            }
            else
            {
                StartCoroutine(RevealObject(obj, change));

                if (change.delayBetweenObjects > 0f)
                    yield return new WaitForSeconds(change.delayBetweenObjects);
            }
        }
    }

    private IEnumerator RevealObject(GameObject obj, DilemmaSceneChange change)
    {
        Transform target = obj.transform;

        if (change.revealStyle == RevealStyle.Instant ||
            change.revealDuration <= 0f)
        {
            obj.SetActive(true);
            yield break;
        }

        Vector3 endPosition = target.position;
        Vector3 endScale = target.localScale;

        Vector3 startPosition = endPosition;
        Vector3 startScale = endScale;

        if (change.revealStyle == RevealStyle.RiseFromGround)
            startPosition = endPosition - Vector3.up * change.riseDistance;
        else if (change.revealStyle == RevealStyle.GrowUp)
            startScale = endScale * 0.001f;

        target.position = startPosition;
        target.localScale = startScale;
        obj.SetActive(true);

        float elapsed = 0f;

        while (elapsed < change.revealDuration)
        {
            if (obj == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / change.revealDuration);
            float smoothT = t * t * (3f - 2f * t);

            target.position = Vector3.Lerp(startPosition, endPosition, smoothT);
            target.localScale = Vector3.Lerp(startScale, endScale, smoothT);

            yield return null;
        }

        if (obj != null)
        {
            target.position = endPosition;
            target.localScale = endScale;
        }
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
        // TEMPORARY: replace with the end of the quiz / back-to-start button later.
        TelemetryManager.Instance?.EndSession("ending shown");

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