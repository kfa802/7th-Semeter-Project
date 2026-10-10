using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DilemmaManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;

    [Tooltip("Drag your DilemmaPanel GameObject here.")]
    [SerializeField] private GameObject dilemmaPanel;

    private DilemmaUI dilemmaUI;

    [Header("Dilemmas")]
    [SerializeField] private List<Dilemma> dilemmas = new();

    [Header("Travel Integration")]
    public UnityEvent<string> onTravelRequested;

    [Header("Repeating Mountain Dilemma")]
    [SerializeField] private string repeatingZoneName = "Mountain";
    [SerializeField] private float repeatDelay = 30f;

    private readonly HashSet<Dilemma> completedDilemmas = new();

    private Dilemma activeDilemma;

    private string currentZone = "";
    private float zoneEnteredTime;

    private Dilemma repeatingDilemma;
    private float nextDilemmaTime;
    private bool waitingForRepeat;

    private void Awake()
    {
        if (dilemmaPanel != null)
        {
            dilemmaUI = dilemmaPanel.GetComponent<DilemmaUI>();

            if (dilemmaUI == null)
            {
                Debug.LogError(
                    "DilemmaManager: The assigned panel does not have a DilemmaUI component.",
                    dilemmaPanel
                );
            }
        }
        else
        {
            Debug.LogError(
                "DilemmaManager: Please assign your DilemmaPanel."
            );
        }
    }

    private void Update()
    {
        if (environment == null || dilemmaUI == null)
            return;

        if (activeDilemma != null)
            return;

        string zoneName = environment.ActiveZoneName;
        bool zoneChanged = zoneName != currentZone;

        if (zoneChanged)
        {
            currentZone = zoneName;
            zoneEnteredTime = Time.time;

            Debug.Log("DilemmaManager: Active zone changed to " + zoneName);
        }

        // Show the Mountain dilemma again after the repeat delay.
        if (waitingForRepeat &&
            Time.time >= nextDilemmaTime &&
            environment.ActiveZoneName == repeatingZoneName)
        {
            waitingForRepeat = false;

            if (repeatingDilemma != null)
            {
                Debug.Log("DilemmaManager: Repeating Mountain dilemma.");
                ShowDilemma(repeatingDilemma);
                return;
            }
        }

        foreach (Dilemma dilemma in dilemmas)
        {
            if (dilemma == null || completedDilemmas.Contains(dilemma))
                continue;

            // Don't trigger the same dilemma through its normal trigger
            // while it is waiting for its repeat.
            if (waitingForRepeat && dilemma == repeatingDilemma)
                continue;

            if (!IsInRequiredZone(dilemma))
                continue;

            if (ShouldTrigger(dilemma, zoneChanged))
            {
                Debug.Log("DilemmaManager: Triggering dilemma: " + dilemma.name);
                ShowDilemma(dilemma);
                break;
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
        if (activeDilemma == null)
            return;

        Dilemma resolvedDilemma = activeDilemma;

        GameEvent gameEvent = choseA
            ? resolvedDilemma.choiceAEvent
            : resolvedDilemma.choiceBEvent;

        bool choseToStay =
            resolvedDilemma.requiredZoneName == repeatingZoneName &&
            choseA;

        if (choseToStay)
        {
            // Allow this dilemma to appear again in 30 seconds.
            repeatingDilemma = resolvedDilemma;
            nextDilemmaTime = Time.time + repeatDelay;
            waitingForRepeat = true;

            Debug.Log(
                "DilemmaManager: Stayed in Mountain. Dilemma will repeat in "
                + repeatDelay + " seconds."
            );
        }
        else
        {
            completedDilemmas.Add(resolvedDilemma);

            if (resolvedDilemma == repeatingDilemma)
            {
                repeatingDilemma = null;
                waitingForRepeat = false;
            }
        }

        activeDilemma = null;
        dilemmaUI.HideDilemma();

        ExecuteEvent(gameEvent);
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
                    "No travel action is connected to DilemmaManager."
                );
            }
        }
    }
}
