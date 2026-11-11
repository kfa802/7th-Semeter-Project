using UnityEngine;

// Add to the Water object that uses WaterShader2.
// Reads the same active-environment values as TemperatureVisuals.
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class WaterEnvironmentVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;

    [Tooltip("The zone this lake belongs to. The lake only reacts to THIS zone's " +
             "temperature, water and disturbance. Empty = the zone whose area the " +
             "lake is inside is found automatically.")]
    [SerializeField] private EcosystemZone zone;
    [Tooltip("Optional: the same Directional Light used by TemperatureVisuals.")]
    [SerializeField] private Light sunLight;

    [Header("Temperature - match TemperatureVisuals")]
    [SerializeField] private float coldStartTemperature = 15f;
    [SerializeField] private float hotStartTemperature = 25f;
    [SerializeField, Min(0.01f)] private float smoothSpeed = 2f;

    [Header("Cold water colors")]
    [SerializeField] private Color coldShallow = new Color(0.40f, 0.65f, 0.75f);
    [SerializeField] private Color coldDeep = new Color(0.08f, 0.20f, 0.30f);
    [SerializeField] private Color coldHighlights = new Color(0.65f, 0.85f, 0.95f);

    [Header("Hot water colors")]
    [SerializeField] private Color hotShallow = new Color(0.32f, 0.40f, 0.22f);
    [SerializeField] private Color hotDeep = new Color(0.10f, 0.17f, 0.07f);
    [SerializeField] private Color hotHighlights = new Color(0.65f, 0.65f, 0.35f);

    [Header("Polluted water colors - pollution 1 means 100 percent")]
    [SerializeField] private Color pollutedShallow = new Color(0.28f, 0.25f, 0.10f);
    [SerializeField] private Color pollutedDeep = new Color(0.07f, 0.065f, 0.025f);
    [SerializeField] private Color pollutedHighlights = new Color(0.12f, 0.115f, 0.065f);

    [Header("Approximate lighting for the existing Unlit shader")]
    [SerializeField, Range(0f, 1f)] private float sunInfluence = 0.5f;

    private static readonly int ShallowId = Shader.PropertyToID("_Shallow_color");
    private static readonly int DeepId = Shader.PropertyToID("_Deep_color");
    private static readonly int HighlightsId = Shader.PropertyToID("_Specular_color");

    [Header("Freezing and drying (visual simulation)")]
    [SerializeField] private float freezeStartTemperature = 0f;
    [SerializeField] private float fullyFrozenTemperature = -5f;
    [SerializeField] private Color iceColor = new Color(0.72f, 0.88f, 0.95f, 1f);
    [SerializeField, Min(0.01f)] private float freezeSeconds = 3f;
    [Tooltip("Water level drop in local units when completely dry. Adjust to the lake bed.")]
    [SerializeField, Min(0f)] private float dryLevelDrop = 1f;
    [Tooltip("Below this water percentage, liquid water gradually fades to transparent. Requires the updated WaterShader2.")]
    [SerializeField, Range(1f, 100f)] private float fadeStartWaterPercent = 30f;

    public float IceAmount => iceAmount;
    public float VisibleWater => visibleWater;

    private float iceAmount;
    private float visibleWater = 1f;
    private bool waterLevelInitialized;
    private EcosystemZone previousWaterZone;
    private Vector3 fullPosition;
    private bool originalRendererEnabled;
    private float originalWaveAmplitude;
    private float originalSpecularSpeed;
    private float originalWaterOpacity = 1f;
    private static readonly int WaveAmplitudeId = Shader.PropertyToID("_Wave_Amplitude");
    private static readonly int SpecularSpeedId = Shader.PropertyToID("_Specular_Speed");
    private static readonly int WaterOpacityId = Shader.PropertyToID("_WaterOpacity");

    private Renderer waterRenderer;
    private Material originalMaterial;
    private Material runtimeMaterial;
    private Color normalShallow, normalDeep, normalHighlights;
    private Color initialSunColor;
    private float initialSunIntensity;
    private float coldAmount, hotAmount, pollutionAmount;
    private bool initialized;

    private void Awake()
    {
        // Capture before TemperatureVisuals changes the light in Start.
        if (sunLight != null)
        {
            initialSunColor = sunLight.color;
            initialSunIntensity = sunLight.intensity;
        }
    }

    private void OnEnable()
    {
        waterRenderer = GetComponent<Renderer>();
        originalMaterial = waterRenderer.sharedMaterial;
        if (originalMaterial == null || !originalMaterial.HasProperty(ShallowId) ||
            !originalMaterial.HasProperty(DeepId) || !originalMaterial.HasProperty(HighlightsId))
        {
            Debug.LogError("WaterEnvironmentVisuals needs WaterShader2 on the first material slot.", this);
            enabled = false;
            return;
        }

        // A private copy keeps the asset and other water objects unchanged.
        runtimeMaterial = new Material(originalMaterial);
        runtimeMaterial.name = originalMaterial.name + " (Water Environment Runtime)";
        waterRenderer.sharedMaterial = runtimeMaterial;
        normalShallow = originalMaterial.GetColor(ShallowId);
        normalDeep = originalMaterial.GetColor(DeepId);
        normalHighlights = originalMaterial.GetColor(HighlightsId);
        fullPosition = transform.localPosition;
        originalRendererEnabled = waterRenderer.enabled;
        originalWaveAmplitude = runtimeMaterial.HasProperty(WaveAmplitudeId)
            ? runtimeMaterial.GetFloat(WaveAmplitudeId) : 0f;
        originalSpecularSpeed = runtimeMaterial.HasProperty(SpecularSpeedId)
            ? runtimeMaterial.GetFloat(SpecularSpeedId) : 0f;
        originalWaterOpacity = runtimeMaterial.HasProperty(WaterOpacityId)
            ? runtimeMaterial.GetFloat(WaterOpacityId) : 1f;
        if (!runtimeMaterial.HasProperty(WaterOpacityId))
            Debug.LogWarning("Update WaterShader2.shadergraph to enable gradual water fading (_WaterOpacity is missing).", this);
        iceAmount = 0f;
        visibleWater = 1f;
        waterLevelInitialized = false;
        previousWaterZone = null;
        initialized = false;
    }

    private void LateUpdate()
    {
        if (environment == null) environment = EnvironmentSystem.Instance;
        if (environment == null || runtimeMaterial == null) return;

        if (zone == null)
            zone = FindMyZone();

        float temperature = Temperature;

        float targetCold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            coldStartTemperature, MinTemperature, temperature));
        float targetHot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            hotStartTemperature, MaxTemperature, temperature));
        float targetPollution = Mathf.Clamp01(Pollution);

        if (!initialized)
        {
            coldAmount = targetCold;
            hotAmount = targetHot;
            pollutionAmount = targetPollution;
            initialized = true;
        }
        else
        {
            float step = Mathf.Max(0.01f, smoothSpeed) * Time.deltaTime;
            coldAmount = Mathf.MoveTowards(coldAmount, targetCold, step);
            hotAmount = Mathf.MoveTowards(hotAmount, targetHot, step);
            pollutionAmount = Mathf.MoveTowards(pollutionAmount, targetPollution, step);
        }

        UpdateWaterState();

        runtimeMaterial.SetColor(ShallowId,
            GetColor(normalShallow, coldShallow, hotShallow, pollutedShallow));
        runtimeMaterial.SetColor(DeepId,
            GetColor(normalDeep, coldDeep, hotDeep, pollutedDeep));
        runtimeMaterial.SetColor(HighlightsId,
            GetColor(normalHighlights, coldHighlights, hotHighlights, pollutedHighlights));
    }

    private void UpdateWaterState()
    {
        float temperature = Temperature;

        // Without its own zone, the old behaviour: reset the ice when the
        // panda changes zone. With its own zone, the ice simply stays.
        if (zone == null)
        {
            EcosystemZone currentZone = environment.ActiveZone;

            if (!waterLevelInitialized || currentZone != previousWaterZone)
            {
                iceAmount = 0f;
                previousWaterZone = currentZone;
            }
        }

        if (!waterLevelInitialized)
        {
            // Start already frozen if this zone starts cold (no thaw-in at load).
            iceAmount = zone != null ? TargetIce(temperature) : 0f;
            waterLevelInitialized = true;
        }

        // Temperaturen styrer frysning og optøning.
        float targetIce = TargetIce(temperature);

        iceAmount = Mathf.MoveTowards(
            iceAmount,
            targetIce,
            Time.deltaTime / Mathf.Max(0.01f, freezeSeconds)
        );

        // Vandstanden følger altid den faktiske vandmængde.
        visibleWater = Mathf.Clamp01(Water / 100f);

        transform.localPosition = fullPosition - Vector3.up *
            (Mathf.Max(0f, dryLevelDrop) * (1f - visibleWater));

        waterRenderer.enabled =
            originalRendererEnabled && visibleWater > 0.001f;

        if (runtimeMaterial.HasProperty(WaterOpacityId))
        {
            float fadeStart =
                Mathf.Clamp(fadeStartWaterPercent, 1f, 100f) / 100f;

            float opacity = Mathf.Clamp01(visibleWater / fadeStart);

            // Isen forbliver synlig.
            opacity = Mathf.Lerp(opacity, 1f, iceAmount);

            runtimeMaterial.SetFloat(
                WaterOpacityId,
                originalWaterOpacity * opacity
            );
        }

        if (runtimeMaterial.HasProperty(WaveAmplitudeId))
        {
            runtimeMaterial.SetFloat(
                WaveAmplitudeId,
                originalWaveAmplitude * Mathf.Pow(1f - iceAmount, 4f)
            );
        }

        if (runtimeMaterial.HasProperty(SpecularSpeedId))
        {
            runtimeMaterial.SetFloat(
                SpecularSpeedId,
                originalSpecularSpeed * (1f - iceAmount)
            );
        }
    }

    // ---------- This lake's own values ----------

    private float Temperature => zone != null ? zone.CurrentTemperature : environment.Temperature;
    private float Water => zone != null ? zone.CurrentWater : environment.Water;
    private float Pollution => zone != null ? zone.CurrentPollution : environment.Pollution;
    private float MinTemperature => zone != null ? zone.minTemperature : environment.MinTemperature;
    private float MaxTemperature => zone != null ? zone.maxTemperature : environment.MaxTemperature;

    private float TargetIce(float temperature)
    {
        return Mathf.InverseLerp(
            freezeStartTemperature,
            Mathf.Min(fullyFrozenTemperature, freezeStartTemperature - 0.01f),
            temperature);
    }

    // The zone whose trigger area contains this lake.
    private EcosystemZone FindMyZone()
    {
        Vector3 position = waterRenderer != null ? waterRenderer.bounds.center : transform.position;
        EcosystemZone best = null;
        float bestDistance = float.MaxValue;

        foreach (EcosystemZone z in FindObjectsByType<EcosystemZone>(FindObjectsSortMode.None))
        {
            Collider c = z.GetComponent<Collider>();
            if (c == null)
                continue;

            float distance = Vector3.Distance(c.ClosestPoint(position), position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = z;
            }
        }

        if (best != null)
            Debug.Log("WaterEnvironmentVisuals: '" + name + "' belongs to zone '" + best.zoneName + "'.", this);

        return best;
    }

    private Color GetColor(Color normal, Color cold, Color hot, Color polluted)
    {
        Color color = Color.Lerp(Color.Lerp(normal, cold, coldAmount), hot, hotAmount);
        color = Color.Lerp(color, polluted, pollutionAmount);

        color = Color.Lerp(color, iceColor, iceAmount);

        // Artistic approximation: follows changes in the existing light,
        // but does not add real reflections, directional shading, or shadows.
        if (sunLight != null && initialSunIntensity > 0.0001f)
        {
            float intensityRatio = sunLight.isActiveAndEnabled
                ? Mathf.Clamp(sunLight.intensity / initialSunIntensity, 0f, 2f) : 0f;
            Color tint = new Color(
                Mathf.Clamp(sunLight.color.r / Mathf.Max(initialSunColor.r, 0.01f), 0f, 2f),
                Mathf.Clamp(sunLight.color.g / Mathf.Max(initialSunColor.g, 0.01f), 0f, 2f),
                Mathf.Clamp(sunLight.color.b / Mathf.Max(initialSunColor.b, 0.01f), 0f, 2f), 1f);
            Color lit = color * tint * intensityRatio;
            color = Color.Lerp(color, lit, sunInfluence);
        }
        color.a = normal.a;
        return color;
    }

    private void OnDisable()
    {
        if (runtimeMaterial == null) return;
        if (waterRenderer != null && waterRenderer.sharedMaterial == runtimeMaterial)
            waterRenderer.sharedMaterial = originalMaterial;
        transform.localPosition = fullPosition;
        if (waterRenderer != null) waterRenderer.enabled = originalRendererEnabled;
        Destroy(runtimeMaterial);
        runtimeMaterial = null;
    }
}