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

    // =========================================================
    // START
    // =========================================================

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

        // =====================================================
        // TEMPERATURE
        // =====================================================

        temperatureSlider.minValue =
            environment.MinTemperature;

        temperatureSlider.maxValue =
            environment.MaxTemperature;

        temperatureSlider.wholeNumbers = false;
        temperatureSlider.interactable = true;

        temperatureSlider.SetValueWithoutNotify(
            environment.Temperature
        );

        temperatureSlider.onValueChanged.AddListener(
            OnTemperatureChanged
        );

        // =====================================================
        // WATER
        // =====================================================

        waterSlider.minValue = 0f;
        waterSlider.maxValue = 100f;

        waterSlider.wholeNumbers = false;
        waterSlider.interactable = false;

        waterSlider.SetValueWithoutNotify(
            environment.Water
        );

        // =====================================================
        // BAMBOO
        // =====================================================

        bambooSlider.minValue = 0f;
        bambooSlider.maxValue = 100f;

        bambooSlider.wholeNumbers = false;
        bambooSlider.interactable = false;

        bambooSlider.SetValueWithoutNotify(
            environment.Bamboo
        );

        // =====================================================
        // PANDA HEALTH
        // =====================================================

        pandaHealthSlider.minValue = 0f;
        pandaHealthSlider.maxValue = 100f;

        // IMPORTANT
        pandaHealthSlider.wholeNumbers = false;
        pandaHealthSlider.interactable = false;

        pandaHealthSlider.SetValueWithoutNotify(
            panda.Health
        );

        UpdateText();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (environment == null ||
            panda == null)
        {
            return;
        }

        // =====================================================
        // WATER
        // =====================================================

        waterSlider.value =
            Mathf.MoveTowards(
                waterSlider.value,
                environment.Water,
                waterSliderSpeed * Time.deltaTime
            );

        // =====================================================
        // BAMBOO
        // =====================================================

        bambooSlider.value =
            Mathf.MoveTowards(
                bambooSlider.value,
                environment.Bamboo,
                bambooSliderSpeed * Time.deltaTime
            );

        // =====================================================
        // PANDA HEALTH
        // =====================================================

        pandaHealthSlider.value =
            Mathf.MoveTowards(
                pandaHealthSlider.value,
                panda.Health,
                pandaHealthSliderSpeed * Time.deltaTime
            );

        UpdateText();
    }

    // =========================================================
    // TEMPERATURE
    // =========================================================

    private void OnTemperatureChanged(float value)
    {
        if (environment == null)
            return;

        environment.SetTemperature(value);
    }

    // =========================================================
    // TEXT
    // =========================================================

    private void UpdateText()
    {
        if (environment == null ||
            panda == null)
        {
            return;
        }

        temperatureText.text =
    environment.Temperature.ToString("0.0") +
    "°C";

        waterText.text =
            "Clean Water: " +
            waterSlider.value.ToString("0") +
            "%";

        bambooText.text =
            "Bamboo: " +
            bambooSlider.value.ToString("0") +
            "%";

        pandaHealthText.text =
            "Panda Health: " +
            pandaHealthSlider.value.ToString("0") +
            "%";
    }
}