using UnityEngine;
using UnityEngine.Rendering;

public class TemperatureVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private Light sunLight;                 // your Directional Light
    [SerializeField] private ParticleSystem snow;            // optional

    [Header("Range")]
    [SerializeField] private float coldStartTemperature = 15f;  // below this it starts getting cold
    [SerializeField] private float hotStartTemperature = 25f;   // above this it starts getting hot
    [SerializeField] private float smoothSpeed = 2f;            // how fast visuals follow the slider

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
    [SerializeField] private float maxSnowRate = 200f;

    [Header("Post Processing (optional)")]
    [SerializeField] private Volume coldVolume;   // weight 0 in the profile
    [SerializeField] private Volume hotVolume;    // weight 0 in the profile

    private float coldAmount;   // 0 = not cold, 1 = coldest
    private float hotAmount;    // 0 = not hot, 1 = hottest

    // "Normal" look, read from your Lighting window / scene at the start
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

    private void Start()
    {
        // Use the values from your Lighting window as the normal look
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

        // Use a copy of the skybox so the asset itself isn't changed
        if (RenderSettings.skybox != null)
        {
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;

            if (skyInstance.HasProperty("_SkyTint")) skyProperty = "_SkyTint";
            else if (skyInstance.HasProperty("_Tint")) skyProperty = "_Tint";

            if (skyProperty != null)
                normalSkyTint = skyInstance.GetColor(skyProperty);
        }

        // Start with the right look immediately
        coldAmount = GetTargetCold();
        hotAmount = GetTargetHot();
        Apply();
    }

    private void Update()
    {
        if (environment == null)
            return;

        coldAmount = Mathf.MoveTowards(coldAmount, GetTargetCold(), smoothSpeed * Time.deltaTime);
        hotAmount = Mathf.MoveTowards(hotAmount, GetTargetHot(), smoothSpeed * Time.deltaTime);

        Apply();
    }

    private float GetTargetCold()
    {
        if (environment == null) return 0f;

        float t = Mathf.InverseLerp(
            coldStartTemperature,
            environment.MinTemperature,
            environment.Temperature
        );

        return Mathf.SmoothStep(0f, 1f, t);
    }

    private float GetTargetHot()
    {
        if (environment == null) return 0f;

        float t = Mathf.InverseLerp(
            hotStartTemperature,
            environment.MaxTemperature,
            environment.Temperature
        );

        return Mathf.SmoothStep(0f, 1f, t);
    }

    // Blend normal -> cold by coldAmount, then -> hot by hotAmount.
    // Only one of them is above 0 at a time.
    private Color Blend(Color normal, Color cold, Color hot)
    {
        return Color.Lerp(Color.Lerp(normal, cold, coldAmount), hot, hotAmount);
    }

    private float Blend(float normal, float cold, float hot)
    {
        return Mathf.Lerp(Mathf.Lerp(normal, cold, coldAmount), hot, hotAmount);
    }

    private void Apply()
    {
        // Fog
        RenderSettings.fogColor = Blend(normalFogColor, coldFogColor, hotFogColor);

        if (fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = Blend(normalFogStart, coldFogStart, hotFogStart);
            RenderSettings.fogEndDistance = Blend(normalFogEnd, coldFogEnd, hotFogEnd);
        }
        else
        {
            RenderSettings.fogDensity = Blend(normalFogDensity, coldFogDensity, hotFogDensity);
        }

        // Ambient light
        RenderSettings.ambientLight = Blend(normalAmbientColor, coldAmbientColor, hotAmbientColor);

        // Sun
        if (sunLight != null)
        {
            sunLight.color = Blend(normalLightColor, coldLightColor, hotLightColor);
            sunLight.intensity = Blend(
                normalLightIntensity,
                normalLightIntensity * coldLightIntensityMultiplier,
                normalLightIntensity * hotLightIntensityMultiplier
            );
        }

        // Sky
        if (skyInstance != null && skyProperty != null)
            skyInstance.SetColor(skyProperty, Blend(normalSkyTint, coldSkyTint, hotSkyTint));

        // Snow (cold only)
        if (snow != null)
        {
            var emission = snow.emission;
            emission.rateOverTime = maxSnowRate * coldAmount;

            if (coldAmount > 0.05f && !snow.isPlaying) snow.Play();
            else if (coldAmount <= 0.05f && snow.isPlaying) snow.Stop();
        }

        // Post processing volumes
        if (coldVolume != null) coldVolume.weight = coldAmount;
        if (hotVolume != null) hotVolume.weight = hotAmount;
    }
}