
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

                Debug.Log(
                    "BambooBelow: " + zone.zoneName +
                    " has " + bambooPercent.ToString("F1") +
                    "% healthy bamboo. Threshold: " +
                    dilemma.threshold + "%"
                );

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

        Debug.Log(
            "DilemmaManager: Temperature condition met for " +
            dilemma.name +
            " | Type = " + dilemma.triggerType +
            " | Current = " + environment.Temperature +
            " | Threshold = " + dilemma.temperatureThreshold +
            " | Required duration = " +
            dilemma.temperatureDurationSeconds + " seconds"
        );
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

        Debug.Log("DilemmaManager: Showing " + dilemma.name);

        dilemmaUI.ShowDilemma(dilemma, this);
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

        Debug.Log(
            "DilemmaManager: Executing GameEvent '" + gameEvent.name +
            "' | RequestTravel = " + gameEvent.requestTravel +
            " | TargetZoneName = '" + gameEvent.targetZoneName + "'"
        );

        if (environment != null)
        {
            environment.ChangeTemperature(gameEvent.temperatureChange);
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
