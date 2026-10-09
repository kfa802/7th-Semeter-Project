
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
    [SerializeField] private float waterSliderSpeed = 15f;
    [SerializeField] private float bambooSliderSpeed = 4f;
    [SerializeField] private float pandaHealthSliderSpeed = 20f;

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

        // WATER

        waterSlider.minValue = 0f;
        waterSlider.maxValue = 100f;
        waterSlider.wholeNumbers = false;
        waterSlider.interactable = false;

        waterSlider.SetValueWithoutNotify(environment.Water);

        // BAMBOO
        // The slider represents the percentage of occupied
        // bamboo growth spots in the current zone.

        bambooSlider.minValue = 0f;
        bambooSlider.maxValue = 100f;
        bambooSlider.wholeNumbers = false;
        bambooSlider.interactable = false;

        bambooSlider.SetValueWithoutNotify(0f);

        // PANDA HEALTH

        pandaHealthSlider.minValue = 0f;
        pandaHealthSlider.maxValue = 100f;
        pandaHealthSlider.wholeNumbers = false;
        pandaHealthSlider.interactable = false;

        pandaHealthSlider.SetValueWithoutNotify(panda.Health);

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

        waterSlider.value = Mathf.MoveTowards(
            waterSlider.value,
            environment.Water,
            waterSliderSpeed * Time.deltaTime
        );

        // BAMBOO

        UpdateBambooDisplay();

        // PANDA HEALTH

        pandaHealthSlider.value = Mathf.MoveTowards(
            pandaHealthSlider.value,
            panda.Health,
            pandaHealthSliderSpeed * Time.deltaTime
        );

        UpdateText();
    }

    // =====================================================
    // BAMBOO DISPLAY
    // =====================================================

    private void UpdateBambooDisplay()
    {
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
            // Animate changes after initialization.
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

        temperatureText.text =
            environment.Temperature.ToString("0.0") + "°C";

        waterText.text =
            "Clean Water: " +
            waterSlider.value.ToString("0") + "%";

        // Show bamboo as a count, not a percentage.
        bambooText.text =
            currentBambooCount + "/" + totalBambooCount;

        pandaHealthText.text =
            "Panda Health: " +
            pandaHealthSlider.value.ToString("0") + "%";
    }
}