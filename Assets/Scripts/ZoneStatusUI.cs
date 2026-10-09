
using UnityEngine;
using TMPro;

public class ZoneStatusUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text zoneText;

    [Header("Display")]
    [SerializeField] private string travellingText = "Travelling";

    private void Start()
    {
        if (zoneText == null)
        {
            Debug.LogError(
                "ZoneStatusUI: Assign the Zone Text in the Inspector."
            );

            enabled = false;
            return;
        }

        UpdateZoneDisplay();
    }

    private void Update()
    {
        UpdateZoneDisplay();
    }

    private void UpdateZoneDisplay()
    {
        if (EnvironmentSystem.Instance == null)
        {
            zoneText.text = travellingText;
            return;
        }

        string activeZone =
            EnvironmentSystem.Instance.ActiveZoneName;

        if (string.IsNullOrEmpty(activeZone) ||
            activeZone == "None" ||
            activeZone == "Travelling")
        {
            zoneText.text = travellingText;
        }
        else
        {
            zoneText.text = activeZone;
        }
    }
}