using UnityEngine;
using TMPro;

public class ZoneStatusUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text zoneText;

    private const string LanguageKey = "language";

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
        // Get selected language
        string language = PlayerPrefs.GetString(LanguageKey, "da");

        bool isDanish = language == "da";

        // Check if EnvironmentSystem exists
        if (EnvironmentSystem.Instance == null)
        {
            zoneText.text = isDanish ? "På vej" : "Travelling";
            return;
        }

        // Get current zone
        string activeZone = EnvironmentSystem.Instance.ActiveZoneName;

        // Travelling or no active zone
        if (string.IsNullOrEmpty(activeZone) ||
            activeZone == "None" ||
            activeZone == "Travelling")
        {
            zoneText.text = isDanish ? "På vej" : "Travelling";
            return;
        }

        // Translate zone names
        switch (activeZone)
        {
            case "Mountain":
                zoneText.text = isDanish ? "Bjerget" : "Mountain";
                break;

            case "Midway":
                zoneText.text = isDanish ? "Bjergsiden" : "Midway";
                break;

            case "Valley":
                zoneText.text = isDanish ? "Dalen" : "Valley";
                break;

            default:
                // If a zone has no translation, show its original name
                zoneText.text = activeZone;
                break;
        }
    }
}