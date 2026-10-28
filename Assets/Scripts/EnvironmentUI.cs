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
    [SerializeField] private Slider waterSlider;
    [SerializeField] private Slider bambooSlider;
    [SerializeField] private Slider pandaHealthSlider;

    [Header("Text")]
    [SerializeField] private TMP_Text temperatureText;
    [SerializeField] private TMP_Text waterText;
    [SerializeField] private TMP_Text bambooText;
    [SerializeField] private TMP_Text pandaHealthText;

    [Header("Visual Slider Speeds")]
    [SerializeField] private float bambooSliderSpeed = 4f;
    [SerializeField] private float pandaHealthSliderSpeed = 20f;

    [Tooltip("How fast the water bar slides, in % per second " +
             "(e.g. down to 0 when travelling, up again on arrival).")]
    [SerializeField] private float waterSliderSpeed = 15f;

    private EcosystemZone lastWaterZone;

    private bool bambooInitialized;
    private EcosystemZone lastBambooZone;

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

        // WATER
        if (waterSlider != null)
        {
            waterSlider.minValue = 0f;
            waterSlider.maxValue = 100f;
            waterSlider.wholeNumbers = false;
            waterSlider.interactable = false;

            // Start at the current water level immediately
            // (0 if the panda is not in a zone).
            waterSlider.SetValueWithoutNotify(GetTargetWater());
            lastWaterZone = environment.ActiveZone;
        }

        // BAMBOO
        if (bambooSlider != null)
        {
            bambooSlider.minValue = 0f;
            bambooSlider.maxValue = 100f;
            bambooSlider.wholeNumbers = false;
            bambooSlider.interactable = false;

            bambooSlider.SetValueWithoutNotify(0f);
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

        UpdateBambooDisplay();
        UpdateText();
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (environment == null || panda == null)
            return;

        // WATER
        // Slides gradually towards the value from EnvironmentSystem.
        UpdateWaterDisplay();

        // BAMBOO
        UpdateBambooDisplay();

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
    // WATER DISPLAY
    // =====================================================

    private void UpdateWaterDisplay()
    {
        if (environment == null || waterSlider == null)
            return;

        EcosystemZone currentZone = environment.ActiveZone;
        float targetWater = GetTargetWater();

        if (currentZone != null && currentZone != lastWaterZone)
        {
            // Arrived in a zone (also the first zone at game start):
            // jump straight to that zone's water, like the bamboo bar.
            waterSlider.SetValueWithoutNotify(targetWater);
        }
        else
        {
            // Slide gradually: down to 0 while travelling,
            // and along with temperature changes inside a zone.
            waterSlider.SetValueWithoutNotify(
                Mathf.MoveTowards(
                    waterSlider.value,
                    targetWater,
                    waterSliderSpeed * Time.deltaTime
                )
            );
        }

        lastWaterZone = currentZone;
    }

    // The real water value of the active zone, or 0 while travelling.
    // The zone's actual water is still kept in EnvironmentSystem / the zone.
    private float GetTargetWater()
    {
        if (environment == null || environment.ActiveZone == null)
            return 0f;

        return Mathf.Clamp(environment.Water, 0f, 100f);
    }

    // =====================================================
    // BAMBOO DISPLAY
    // =====================================================

    private void UpdateBambooDisplay()
    {
        if (bambooSlider == null)
            return;

        BambooManager manager = BambooManager.Instance;

        EcosystemZone activeZone =
            environment != null ? environment.ActiveZone : null;

        if (manager == null || activeZone == null)
        {
            currentBambooCount = 0;
            totalBambooCount = 0;

            bambooSlider.SetValueWithoutNotify(0f);

            return;
        }

        // Actual bamboo objects in this zone.
        currentBambooCount =
            manager.GetCurrentCountForZone(activeZone);

        // Total growth spots available in this zone.
        totalBambooCount =
            manager.GetCapacityForZone(activeZone);

        // Convert the bamboo count into a slider percentage.
        float targetPercentage = totalBambooCount > 0
            ? Mathf.Clamp01(
                (float)currentBambooCount / totalBambooCount
              ) * 100f
            : 0f;

        // Set the correct starting value immediately.
        // Also reset immediately when entering another zone.
        if (!bambooInitialized || lastBambooZone != activeZone)
        {
            bambooSlider.SetValueWithoutNotify(targetPercentage);

            bambooInitialized = true;
            lastBambooZone = activeZone;
        }
        else
        {
            // Animate bamboo changes after initialization.
            bambooSlider.value = Mathf.MoveTowards(
                bambooSlider.value,
                targetPercentage,
                bambooSliderSpeed * Time.deltaTime
            );
        }
    }

    // =====================================================
    // TEMPERATURE
    // =====================================================

    private void OnTemperatureChanged(float value)
    {
        if (environment == null)
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

    // What the water bar currently shows, so the warning
    // follows the bar as it slides (0 while travelling).
    public float WaterPercent =>
        waterSlider != null ? waterSlider.value : GetTargetWater();

    public bool HasActiveZone =>
        environment != null && environment.ActiveZone != null;

    public float HealthPercent =>
        panda != null ? Mathf.Clamp(panda.Health, 0f, 100f) : 0f;

    public bool HasPanda => panda != null;
}