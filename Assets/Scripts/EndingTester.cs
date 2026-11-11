using UnityEngine;

// Temporary test helper. Delete when you're done testing.
public class EndingTester : MonoBehaviour
{
    [SerializeField] private EnvironmentSystem environment;

    [ContextMenu("Cold (-5)")]
    private void Cold() => environment.SetTemperature(-5f, "test");

    [ContextMenu("Mild (15)")]
    private void Mild() => environment.SetTemperature(15f, "test");

    [ContextMenu("Extreme heat (40)")]
    private void ExtremeHeat() => environment.SetTemperature(40f, "test");

    [ContextMenu("Pollution +50%")]
    private void PollutionUp() => environment.ChangePollution(0.5f);

    [ContextMenu("Pollution -50%")]
    private void PollutionDown() => environment.ChangePollution(-0.5f);

    [ContextMenu("Log status")]
    private void LogStatus()
    {
        Debug.Log("Test status | zone = " + environment.ActiveZoneName +
                  " | temperature = " + environment.Temperature.ToString("F1") +
                  " | pollution = " + environment.Pollution.ToString("F3") +
                  " | growth = " + environment.GrowthCondition);
    }
}
