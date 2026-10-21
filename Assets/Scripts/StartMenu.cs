using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Startmenu: flagene vælger sprog (gemmes), START åbner den valgte scene.
///
/// On Click () på knapperne:
///   Engelsk flag  → StartMenu.SelectEnglish
///   Dansk flag    → StartMenu.SelectDanish
///   START         → StartMenu.StartGame
/// </summary>
public class StartMenu : MonoBehaviour
{
    [Header("Navne på scenerne (skal være i File → Build Settings)")]
    [SerializeField] private string danishScene = "Game_DA";
    [SerializeField] private string englishScene = "Game_EN";

    [Header("Valgfrit: ring/flueben der viser det valgte flag")]
    [SerializeField] private GameObject danishSelected;
    [SerializeField] private GameObject englishSelected;

    [Header("Participant")]
    [SerializeField] private TMP_Text nextIdLabel;

    private const string PrefKey = "language"; // "da" eller "en"

    private string Current => PlayerPrefs.GetString(PrefKey, "da");

    private void Start()
    {
        Debug.Log($"[StartMenu] Scriptet kører på '{name}'. Gemt sprog: {Current}", this);
        UpdateSelection();
        UpdateIdLabel();
    }

    public void SelectDanish() => Select("da");
    public void SelectEnglish() => Select("en");

    public void StartGame()
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.BeginSession(Current);
        else
            Debug.LogError("[StartMenu] No TelemetryManager in the scene. Nothing will be logged.");

        SceneManager.LoadScene(Current == "en" ? englishScene : danishScene);
    }

    private void Select(string language)
    {
        PlayerPrefs.SetString(PrefKey, language);
        PlayerPrefs.Save();
        Debug.Log($"[StartMenu] Sprog valgt: {language} (på objektet '{name}')", this);
        UpdateSelection();
    }

    private void UpdateIdLabel()
    {
        if (nextIdLabel != null && TelemetryManager.Instance != null)
            nextIdLabel.text = "Participant: " + TelemetryManager.Instance.PeekNextId();
    }

    private void UpdateSelection()
    {
        if (danishSelected == null || englishSelected == null)
            Debug.LogWarning("[StartMenu] 'Danish Selected' eller 'English Selected' er tom i Inspectoren – træk ring-objekterne derind.", this);

        if (danishSelected != null) danishSelected.SetActive(Current == "da");
        if (englishSelected != null) englishSelected.SetActive(Current == "en");
    }
}
