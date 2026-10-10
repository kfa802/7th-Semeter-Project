
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
    [Tooltip("Seconds to wait before displaying the selected ending.")]
    [SerializeField] private float endingDelay = 3f;

    private DilemmaUI dilemmaUI;
    private EndingUI endingUI;

    private readonly HashSet<Dilemma> completedDilemmas = new();

    private readonly Dictionary<Dilemma, float> scheduledDilemmas = new();

    private readonly Dictionary<Dilemma, bool> resolvedChoices = new();

    private Dilemma activeDilemma;

    private string currentZone = "";
    private float zoneEnteredTime;

    private bool gameEnded;

    private void Awake()
    {
        if (dilemmaPanel != null)
            dilemmaUI = dilemmaPanel.GetComponent<DilemmaUI>();

        if (dilemmaUI == null)
            Debug.LogError(
                "DilemmaManager: Assign a DilemmaPanel with DilemmaUI."
            );

        if (endingPanel != null)
        {
            endingUI = endingPanel.GetComponent<EndingUI>();
            endingPanel.SetActive(false);
        }

        if (endingPanel != null && endingUI == null)
            Debug.LogError(
                "DilemmaManager: EndingPanel needs an EndingUI component."
            );
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

        // Check dilemmas waiting for their scheduled delay.
        foreach (Dilemma dilemma in dilemmas)
        {
            if (dilemma == null || completedDilemmas.Contains(dilemma))
                continue;

            if (!scheduledDilemmas.ContainsKey(dilemma))
                continue;

            if (Time.time < scheduledDilemmas[dilemma])
                continue;

            if (!IsInRequiredZone(dilemma))
                continue;

            scheduledDilemmas.Remove(dilemma);

            Debug.Log(
                "DilemmaManager: Delay finished. Showing " + dilemma.name
            );

            ShowDilemma(dilemma);
            return;
        }

        // Check normal dilemmas without prerequisites.
        foreach (Dilemma dilemma in dilemmas)
        {
            if (dilemma == null || completedDilemmas.Contains(dilemma))
                continue;

            // Follow-up dilemmas must be scheduled by a matching choice.
            if (dilemma.prerequisiteDilemma != null)
                continue;

            if (!IsInRequiredZone(dilemma))
                continue;

            if (ShouldTrigger(dilemma, zoneChanged))
            {
                Debug.Log(
                    "DilemmaManager: Triggering " + dilemma.name
                );

                ShowDilemma(dilemma);
                return;
            }
        }
    }

    private bool IsInRequiredZone(Dilemma dilemma)
    {
        if (string.IsNullOrWhiteSpace(dilemma.requiredZoneName))
            return true;

        return environment.ActiveZoneName == dilemma.requiredZoneName;
    }

    private bool ShouldTrigger(Dilemma dilemma, bool zoneChanged)
    {
        switch (dilemma.triggerType)
        {
            case DilemmaTriggerType.ZoneDelay:
                return Time.time - zoneEnteredTime >= dilemma.delaySeconds;

            case DilemmaTriggerType.ZoneEntered:
                return zoneChanged;

            case DilemmaTriggerType.WaterBelow:
                return environment.Water < dilemma.threshold;

            case DilemmaTriggerType.PollutionAbove:
                return environment.Pollution > dilemma.threshold;

            default:
                return false;
        }
    }

    private void ShowDilemma(Dilemma dilemma)
    {
        activeDilemma = dilemma;

        Debug.Log("DilemmaManager: Showing " + dilemma.name);

        dilemmaUI.ShowDilemma(dilemma, this);
    }

    public void ChooseA()
    {
        ResolveDilemma(true);
    }

    public void ChooseB()
    {
        ResolveDilemma(false);
    }

    private void ResolveDilemma(bool choseA)
    {
        if (activeDilemma == null || gameEnded)
            return;

        Dilemma resolvedDilemma = activeDilemma;

        Debug.Log(
            "DilemmaManager: " + resolvedDilemma.name
            + " resolved with Choice " + (choseA ? "A" : "B")
        );

        completedDilemmas.Add(resolvedDilemma);
        resolvedChoices[resolvedDilemma] = choseA;

        GameEvent gameEvent = choseA
            ? resolvedDilemma.choiceAEvent
            : resolvedDilemma.choiceBEvent;

        Ending selectedEnding = choseA
            ? resolvedDilemma.choiceAEnding
            : resolvedDilemma.choiceBEnding;

        activeDilemma = null;
        dilemmaUI.HideDilemma();

        // Apply the selected choice's environmental/travel consequences.
        ExecuteEvent(gameEvent);

        // If this choice has an ending assigned, show that ending.
        if (selectedEnding != null)
        {
            BeginEnding(selectedEnding);
            return;
        }

        // Otherwise, schedule follow-up dilemmas whose prerequisites match.
        foreach (Dilemma nextDilemma in dilemmas)
        {
            if (nextDilemma == null ||
                completedDilemmas.Contains(nextDilemma))
                continue;

            if (nextDilemma.prerequisiteDilemma != resolvedDilemma)
                continue;

            bool choiceMatches =
                nextDilemma.prerequisiteChoice == PrerequisiteChoice.Either ||
                (nextDilemma.prerequisiteChoice == PrerequisiteChoice.ChoiceA
                    && choseA) ||
                (nextDilemma.prerequisiteChoice == PrerequisiteChoice.ChoiceB
                    && !choseA);

            if (!choiceMatches)
            {
                Debug.Log(
                    nextDilemma.name
                    + ": Prerequisite choice did not match."
                );

                continue;
            }

            scheduledDilemmas[nextDilemma] =
                Time.time + Mathf.Max(0f, nextDilemma.delaySeconds);

            Debug.Log(
                nextDilemma.name + " scheduled in "
                + nextDilemma.delaySeconds + " seconds."
            );
        }
    }

    private void BeginEnding(Ending ending)
    {
        gameEnded = true;

        Debug.Log(
            "DilemmaManager: Ending '" + ending.endingTitle
            + "' will appear in " + endingDelay + " seconds."
        );

        Invoke(nameof(ShowSelectedEnding), Mathf.Max(0f, endingDelay));

        // Store the selected ending for the delayed display.
        pendingEnding = ending;
    }

    private Ending pendingEnding;

    private void ShowSelectedEnding()
    {
        if (pendingEnding == null)
        {
            Debug.LogError("DilemmaManager: No ending was selected.");
            return;
        }

        if (endingUI != null)
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
            "DilemmaManager: Displaying ending " + pendingEnding.endingTitle
        );
    }

    private void ExecuteEvent(GameEvent gameEvent)
    {
        if (gameEvent == null)
            return;

        if (environment != null)
        {
            environment.ChangeTemperature(gameEvent.temperatureChange);
            environment.ChangePollution(gameEvent.pollutionChange);
        }

        if (gameEvent.requestTravel)
        {
            if (onTravelRequested != null &&
                onTravelRequested.GetPersistentEventCount() > 0)
            {
                onTravelRequested.Invoke(gameEvent.targetZoneName);
            }
            else
            {
                Debug.LogWarning(
                    "DilemmaManager: No travel action is connected."
                );
            }
        }
    }
}