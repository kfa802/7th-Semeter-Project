
using UnityEngine;
using UnityEngine.Rendering;

public class TemperatureVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private Light sunLight;
    [SerializeField] private ParticleSystem snow;

    [Header("Range")]
    [SerializeField] private float coldStartTemperature = 15f;
    [SerializeField] private float hotStartTemperature = 25f;
    [SerializeField] private float smoothSpeed = 2f;

    [Header("Fog Mode")]
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Fog Colors")]
    [SerializeField] private Color coldFogColor = new Color(0.92f, 0.96f, 1f);
    [SerializeField] private Color hotFogColor = new Color(1f, 0.82f, 0.55f);

    [Header("Fog Density (Exponential modes)")]
    [SerializeField] private float coldFogDensity = 0.06f;
    [SerializeField] private float hotFogDensity = 0.02f;

    [Header("Fog Distance (Linear mode)")]
    [SerializeField] private float coldFogStart = 0f;
    [SerializeField] private float coldFogEnd = 40f;
    [SerializeField] private float hotFogStart = 10f;
    [SerializeField] private float hotFogEnd = 120f;

    [Header("Light")]
    [SerializeField] private Color coldLightColor = new Color(0.75f, 0.85f, 1f);
    [SerializeField] private float coldLightIntensityMultiplier = 0.8f;
    [SerializeField] private Color hotLightColor = new Color(1f, 0.85f, 0.6f);
    [SerializeField] private float hotLightIntensityMultiplier = 1.2f;

    [Header("Ambient")]
    [SerializeField] private Color coldAmbientColor = new Color(0.8f, 0.88f, 1f);
    [SerializeField] private Color hotAmbientColor = new Color(1f, 0.9f, 0.75f);

    [Header("Skybox (optional)")]
    [SerializeField] private Color coldSkyTint = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color hotSkyTint = new Color(1f, 0.8f, 0.6f);

    [Header("Snow")]
    [SerializeField] private float freezingTemperature = 0f;

    [Tooltip("How quickly snow emission increases when it gets cold.")]
    [SerializeField] private float snowFadeInSpeed = 0.5f;

    [Tooltip("How slowly snow emission fades just above freezing.")]
    [SerializeField] private float minimumSnowFadeOutSpeed = 0.1f;

    [Tooltip("How much faster snow fades for each degree above freezing.")]
    [SerializeField] private float snowFadeOutSpeedPerDegree = 0.15f;

    [Header("Post Processing (optional)")]
    [SerializeField] private Volume coldVolume;
    [SerializeField] private Volume hotVolume;

    private float coldAmount;
    private float hotAmount;

    // Controls the snow emission independently of the cold visuals.
    private float snowAmount;

    private Color normalFogColor;
    private float normalFogDensity;
    private float normalFogStart;
    private float normalFogEnd;

    private Color normalLightColor;
    private float normalLightIntensity;
    private Color normalAmbientColor;

    private Material skyInstance;
    private string skyProperty;
    private Color normalSkyTint;

    private ParticleSystem.EmissionModule snowEmission;
    private float originalSnowEmissionRate;

    private void Start()
    {
        // Store the normal lighting settings.
        normalFogColor = RenderSettings.fogColor;
        normalFogDensity = RenderSettings.fogDensity;
        normalFogStart = RenderSettings.fogStartDistance;
        normalFogEnd = RenderSettings.fogEndDistance;

        RenderSettings.fog = true;
        RenderSettings.fogMode = fogMode;

        normalAmbientColor = RenderSettings.ambientLight;

        if (sunLight != null)
        {
            normalLightColor = sunLight.color;
            normalLightIntensity = sunLight.intensity;
        }

        // Use a copy of the skybox so the original asset is not changed.
        if (RenderSettings.skybox != null)
        {
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;

            if (skyInstance.HasProperty("_SkyTint"))
                skyProperty = "_SkyTint";
            else if (skyInstance.HasProperty("_Tint"))
                skyProperty = "_Tint";

            if (skyProperty != null)
                normalSkyTint = skyInstance.GetColor(skyProperty);
        }

        // Set up snow.
        if (snow != null)
        {
            snowEmission = snow.emission;

            originalSnowEmissionRate =
                snowEmission.rateOverTime.constant;

            snowAmount = 0f;
            snowEmission.rateOverTime = 0f;

            // Clear particles only during initial setup.
            snow.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        // Initialize the visuals using the current temperature.
        coldAmount = GetTargetCold();
        hotAmount = GetTargetHot();

        Apply();
    }

    private void Update()
    {
        if (environment == null)
            return;

        coldAmount = Mathf.MoveTowards(
            coldAmount,
            GetTargetCold(),
            smoothSpeed * Time.deltaTime
        );

        hotAmount = Mathf.MoveTowards(
            hotAmount,
            GetTargetHot(),
            smoothSpeed * Time.deltaTime
        );

        UpdateSnow();
        Apply();
    }

    private float GetTargetCold()
    {
        if (environment == null)
            return 0f;

        float t = Mathf.InverseLerp(
            coldStartTemperature,
            environment.MinTemperature,
            environment.Temperature
        );

        return Mathf.SmoothStep(0f, 1f, t);
    }

    private float GetTargetHot()
    {
        if (environment == null)
            return 0f;

        float t = Mathf.InverseLerp(
            hotStartTemperature,
            environment.MaxTemperature,
            environment.Temperature
        );

        return Mathf.SmoothStep(0f, 1f, t);
    }

    private void UpdateSnow()
    {
        if (snow == null || environment == null)
            return;

        float temperature = environment.Temperature;

        // Snow emission is active only below freezing.
        float targetSnowAmount =
            temperature < freezingTemperature ? 1f : 0f;

        float fadeSpeed;

        if (targetSnowAmount > 0f)
        {
            // Fade in at a steady speed when it gets cold.
            fadeSpeed = snowFadeInSpeed;
        }
        else
        {
            // The warmer it gets, the faster snow emission fades out.
            float degreesAboveFreezing = Mathf.Max(
                0f,
                temperature - freezingTemperature
            );

            fadeSpeed =
                minimumSnowFadeOutSpeed +
                degreesAboveFreezing * snowFadeOutSpeedPerDegree;
        }

        snowAmount = Mathf.MoveTowards(
            snowAmount,
            targetSnowAmount,
            fadeSpeed * Time.deltaTime
        );

        // Gradually change the emission rate.
        snowEmission.rateOverTime =
            originalSnowEmissionRate * snowAmount;

        // Start the system when snow begins to fade in.
        if (snowAmount > 0f && !snow.isPlaying)
        {
            snow.Play();
        }

        // Stop emitting without clearing existing particles.
        // They will finish their lifetimes naturally.
        if (snowAmount <= 0f && snow.isPlaying)
        {
            snow.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting
            );
        }
    }

    private Color Blend(Color normal, Color cold, Color hot)
    {
        return Color.Lerp(
            Color.Lerp(normal, cold, coldAmount),
            hot,
            hotAmount
        );
    }

    private float Blend(float normal, float cold, float hot)
    {
        return Mathf.Lerp(
            Mathf.Lerp(normal, cold, coldAmount),
            hot,
            hotAmount
        );
    }

    private void Apply()
    {
        // Fog.
        RenderSettings.fogColor =
            Blend(normalFogColor, coldFogColor, hotFogColor);

        if (fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance =
                Blend(normalFogStart, coldFogStart, hotFogStart);

            RenderSettings.fogEndDistance =
                Blend(normalFogEnd, coldFogEnd, hotFogEnd);
        }
        else
        {
            RenderSettings.fogDensity =
                Blend(normalFogDensity, coldFogDensity, hotFogDensity);
        }

        // Ambient light.
        RenderSettings.ambientLight =
            Blend(normalAmbientColor, coldAmbientColor, hotAmbientColor);

        // Sun.
        if (sunLight != null)
        {
            sunLight.color =
                Blend(normalLightColor, coldLightColor, hotLightColor);

            sunLight.intensity = Blend(
                normalLightIntensity,
                normalLightIntensity * coldLightIntensityMultiplier,
                normalLightIntensity * hotLightIntensityMultiplier
            );
        }

        // Skybox.
        if (skyInstance != null && skyProperty != null)
        {
            skyInstance.SetColor(
                skyProperty,
                Blend(normalSkyTint, coldSkyTint, hotSkyTint)
            );
        }

        // Post-processing volumes.
        if (coldVolume != null)
            coldVolume.weight = coldAmount;

        if (hotVolume != null)
            hotVolume.weight = hotAmount;
    }

    private void OnDestroy()
    {
        // Clean up the runtime skybox copy.
        if (skyInstance != null)
        {
            if (RenderSettings.skybox == skyInstance)
                RenderSettings.skybox = null;

            Destroy(skyInstance);
        }
    }
}
