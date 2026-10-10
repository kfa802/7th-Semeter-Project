
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DilemmaManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;

    [Tooltip("Drag your DilemmaPanel GameObject here.")]
    [SerializeField] private GameObject dilemmaPanel;

    [Tooltip("Optional panel displayed when the game ends.")]
    [SerializeField] private GameObject gameOverPanel;

    private DilemmaUI dilemmaUI;

    [Header("Dilemmas")]
    [SerializeField] private List<Dilemma> dilemmas = new();

    [Header("Travel Integration")]
    public UnityEvent<string> onTravelRequested;

    [Header("Game Over Settings")]
    [Tooltip("Seconds to wait before showing the Game Over panel.")]
    [SerializeField] private float gameOverDelay = 3f;

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
        {
            dilemmaUI = dilemmaPanel.GetComponent<DilemmaUI>();
        }

        if (dilemmaUI == null)
        {
            Debug.LogError(
                "DilemmaManager: Assign a DilemmaPanel with DilemmaUI."
            );
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
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

        // Check dilemmas scheduled after a prerequisite choice.
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

        // Check ordinary dilemmas without prerequisites.
        foreach (Dilemma dilemma in dilemmas)
        {
            if (dilemma == null || completedDilemmas.Contains(dilemma))
                continue;

            // Prerequisite dilemmas are scheduled after a matching choice.
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

        activeDilemma = null;
        dilemmaUI.HideDilemma();

        ExecuteEvent(gameEvent);

        if (resolvedDilemma.endsGame)
        {
            EndGame();
            return;
        }

        // Schedule matching follow-up dilemmas.
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

            // Countdown starts when the choice is made.
            scheduledDilemmas[nextDilemma] =
                Time.time + Mathf.Max(0f, nextDilemma.delaySeconds);

            Debug.Log(
                nextDilemma.name + " scheduled in "
                + nextDilemma.delaySeconds + " seconds."
            );
        }
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

    private void EndGame()
    {
        gameEnded = true;

        Debug.Log(
            "DilemmaManager: Game Over panel will appear in "
            + gameOverDelay + " seconds."
        );

        Invoke(nameof(ShowGameOverPanel), Mathf.Max(0f, gameOverDelay));
    }

    private void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "DilemmaManager: No Game Over panel is assigned."
            );
        }

        Debug.Log("DilemmaManager: GAME OVER");
    }
}