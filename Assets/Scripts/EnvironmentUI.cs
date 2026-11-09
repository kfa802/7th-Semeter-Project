using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnvironmentUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private PandaSystem panda;
    [SerializeField] private PandaPathMover pandaPathMover;
    [SerializeField] private DilemmaUI dilemmaUI;

    [Header("Sliders")]
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private Slider pandaHealthSlider;

    [Header("Text")]
    [SerializeField] private TMP_Text temperatureText;
    [SerializeField] private TMP_Text waterText;
    [SerializeField] private TMP_Text bambooText;
    [SerializeField] private TMP_Text pandaHealthText;

    [Header("Visual Slider Speeds")]
    [SerializeField] private float pandaHealthSliderSpeed = 20f;

    private bool ignoreSliderCallback;

    private int currentBambooCount;
    private int totalBambooCount;

    // =====================================================
    // START
    // =====================================================

    private void Start()
    {
        if (environment == null)
        {
            Debug.LogError(
                "EnvironmentUI: EnvironmentSystem is not assigned."
            );
            return;
        }

        if (panda == null)
        {
            Debug.LogError(
                "EnvironmentUI: PandaSystem is not assigned."
            );
            return;
        }

        // TEMPERATURE
        if (temperatureSlider != null)
        {
            temperatureSlider.minValue = environment.MinTemperature;
            temperatureSlider.maxValue = environment.MaxTemperature;
            temperatureSlider.wholeNumbers = false;

            // Start with the current temperature.
            temperatureSlider.SetValueWithoutNotify(
                environment.Temperature
            );

            // Prevent manual temperature changes during travel
            // or while a dilemma is open.
            bool travelling =
                pandaPathMover != null &&
                pandaPathMover.IsTravelling;

            bool dilemmaOpen =
                dilemmaUI != null &&
                dilemmaUI.IsDilemmaOpen;

            temperatureSlider.interactable =
                !travelling && !dilemmaOpen;

            temperatureSlider.onValueChanged.AddListener(
                OnTemperatureChanged
            );
        }

        // PANDA HEALTH
        if (pandaHealthSlider != null)
        {
            pandaHealthSlider.minValue = 0f;
            pandaHealthSlider.maxValue = 100f;
            pandaHealthSlider.wholeNumbers = false;
            pandaHealthSlider.interactable = false;

            pandaHealthSlider.SetValueWithoutNotify(panda.Health);
        }

        UpdateBambooCount();
        UpdateText();
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (environment == null || panda == null)
            return;

        // BAMBOO (count only, for the bamboo text and other scripts)
        UpdateBambooCount();

        // TEMPERATURE
        UpdateTemperatureDisplay();

        // PANDA HEALTH
        // Health changes still animate smoothly.
        if (pandaHealthSlider != null)
        {
            pandaHealthSlider.value = Mathf.MoveTowards(
                pandaHealthSlider.value,
                panda.Health,
                pandaHealthSliderSpeed * Time.deltaTime
            );
        }

        UpdateText();
    }

    private void UpdateTemperatureDisplay()
    {
        if (environment == null || temperatureSlider == null)
            return;

        // Each zone has its own min/max temperature, so keep the
        // slider range in sync with the active zone.
        if (!Mathf.Approximately(temperatureSlider.minValue, environment.MinTemperature) ||
            !Mathf.Approximately(temperatureSlider.maxValue, environment.MaxTemperature))
        {
            // Changing the range can clamp the value and fire the
            // slider's callback; ignore that so it isn't logged
            // as a player change.
            ignoreSliderCallback = true;
            temperatureSlider.minValue = environment.MinTemperature;
            temperatureSlider.maxValue = environment.MaxTemperature;
            ignoreSliderCallback = false;
        }

        // Keep the slider synchronized with the actual environment.
        // SetValueWithoutNotify prevents the slider from changing
        // the environment again through its own callback.
        temperatureSlider.SetValueWithoutNotify(
            Mathf.Clamp(
                environment.Temperature,
                temperatureSlider.minValue,
                temperatureSlider.maxValue
            )
        );

        // Lock the slider while travelling or while a dilemma is open.
        bool travelling =
            pandaPathMover != null &&
            pandaPathMover.IsTravelling;

        bool dilemmaOpen =
            dilemmaUI != null &&
            dilemmaUI.IsDilemmaOpen;

        temperatureSlider.interactable =
            !travelling && !dilemmaOpen;
    }

    // =====================================================
    // WATER / BAMBOO VALUES (no sliders)
    // =====================================================

    // The real water value of the active zone, or 0 while travelling.
    private float GetTargetWater()
    {
        if (environment == null || environment.ActiveZone == null)
            return 0f;

        return Mathf.Clamp(environment.Water, 0f, 100f);
    }

    private void UpdateBambooCount()
    {
        BambooManager manager = BambooManager.Instance;

        EcosystemZone activeZone =
            environment != null ? environment.ActiveZone : null;

        if (manager == null || activeZone == null)
        {
            currentBambooCount = 0;
            totalBambooCount = 0;
            return;
        }

        // Fully grown bamboo in this zone / total growth spots.
        currentBambooCount = manager.GetCurrentCountForZone(activeZone);
        totalBambooCount = manager.GetCapacityForZone(activeZone);
    }

    // =====================================================
    // TEMPERATURE
    // =====================================================

    private void OnTemperatureChanged(float value)
    {
        if (environment == null || ignoreSliderCallback)
            return;

        // Extra protection: ignore slider changes during a dilemma
        // or while the panda is travelling.
        if (dilemmaUI != null && dilemmaUI.IsDilemmaOpen)
            return;

        if (pandaPathMover != null && pandaPathMover.IsTravelling)
            return;

        environment.SetTemperature(value);
    }

    // =====================================================
    // TEXT
    // =====================================================

    private void UpdateText()
    {
        if (environment == null || panda == null)
            return;

        if (temperatureText != null)
        {
            temperatureText.text =
                environment.Temperature.ToString("0.0") + "°C";
        }

        if (waterText != null)
        {
            float displayedWater = WaterPercent;

            waterText.text =
                "Clean Water: " +
                displayedWater.ToString("0") + "%";
        }

        // Show bamboo as a count, not a percentage.
        if (bambooText != null)
        {
            bambooText.text =
                currentBambooCount + "/" + totalBambooCount;
        }

        if (pandaHealthText != null)
        {
            float displayedHealth = pandaHealthSlider != null
                ? pandaHealthSlider.value
                : panda.Health;

            pandaHealthText.text =
                "Panda Health: " +
                displayedHealth.ToString("0") + "%";
        }
    }

    // =====================================================
    // CLEANUP
    // =====================================================

    private void OnDestroy()
    {
        if (temperatureSlider != null)
        {
            temperatureSlider.onValueChanged.RemoveListener(
                OnTemperatureChanged
            );
        }
    }

    // =====================================================
    // PUBLIC VALUES (used by BambooLowWarning)
    // =====================================================

    // True while the panda is between zones.
    public bool IsBetweenZones =>
        environment != null && environment.ActiveZone == null;

    public float BambooPercent =>
        totalBambooCount > 0
            ? (float)currentBambooCount / totalBambooCount * 100f
            : 0f;

    public bool HasBambooZone => totalBambooCount > 0;

    // The active zone's water (0 while travelling).
    public float WaterPercent => GetTargetWater();

    public bool HasActiveZone =>
        environment != null && environment.ActiveZone != null;

    public float HealthPercent =>
        panda != null ? Mathf.Clamp(panda.Health, 0f, 100f) : 0f;

    public bool HasPanda => panda != null;
}