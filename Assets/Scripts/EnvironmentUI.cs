
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnvironmentUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private PandaSystem panda;

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

    private bool waterSliderInitialized = false;
    private EcosystemZone previousWaterZone;

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
            temperatureSlider.interactable = true;

            temperatureSlider.SetValueWithoutNotify(
                environment.Temperature
            );

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

            // Start at the current water level immediately.
            waterSlider.SetValueWithoutNotify(environment.Water);

            waterSliderInitialized = true;
            previousWaterZone = environment.ActiveZone;
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
        // Immediately follows the value from EnvironmentSystem.
        UpdateWaterDisplay();

        // BAMBOO
        UpdateBambooDisplay();

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

    // =====================================================
    // WATER DISPLAY
    // =====================================================

    private void UpdateWaterDisplay()
    {
        if (environment == null || waterSlider == null)
            return;

        EcosystemZone currentZone = environment.ActiveZone;
        float targetWater = Mathf.Clamp(
            environment.Water,
            0f,
            100f
        );

        // Set the correct value immediately at startup
        // and whenever the panda enters or leaves a zone.
        if (!waterSliderInitialized ||
            currentZone != previousWaterZone)
        {
            waterSlider.SetValueWithoutNotify(targetWater);

            waterSliderInitialized = true;
            previousWaterZone = currentZone;
        }
        else
        {
            // No animation: the slider immediately matches the
            // current water value from EnvironmentSystem.
            waterSlider.SetValueWithoutNotify(targetWater);
        }
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
            float displayedWater = waterSlider != null
                ? waterSlider.value
                : environment.Water;

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
}