using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Looping background sounds that blend with the temperature:
/// winter wind when cold, birds in the comfortable middle, and a
/// deadly-heat sound when hot. All three loop all the time; only their
/// volumes change, so the blend between them is smooth.
///
/// Put this on an empty GameObject (e.g. "Ambience").
/// </summary>
public class TemperatureAmbience : MonoBehaviour
{
    [Header("Clips (looping)")]
    [SerializeField] private AudioClip winterWind;
    [SerializeField] private AudioClip birds;
    [SerializeField] private AudioClip deadlyHeat;

    [Header("Cold (winter wind)")]
    [Tooltip("At or below this temperature the wind is at full volume.")]
    [SerializeField] private float coldFullAt = -5f;

    [Tooltip("At or above this temperature the wind is silent. " +
             "Between the two values the wind and birds blend.")]
    [SerializeField] private float coldGoneAt = 8f;

    [Header("Heat (deadly heat)")]
    [Tooltip("At or below this temperature the heat sound is silent. " +
             "Between the two values the birds and heat blend.")]
    [SerializeField] private float heatStartsAt = 28f;

    [Tooltip("At or above this temperature the heat sound is at full volume.")]
    [SerializeField] private float heatFullAt = 36f;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float birdsVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float heatVolume = 0.7f;

    [Tooltip("How fast the volumes follow the temperature (volume per second). " +
             "Lower = slower, softer changes when the slider is dragged.")]
    [SerializeField, Min(0.01f)] private float blendSpeed = 0.6f;

    [Tooltip("Seconds to fade everything in when the game starts.")]
    [SerializeField, Min(0f)] private float startFadeIn = 2f;

    [Tooltip("Optional: send the ambience through a group in your Audio Mixer.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("While Travelling")]
    [Tooltip("Optional: the panda's PandaPathMover. If empty, 'travelling' means " +
             "the panda is between zones.")]
    [SerializeField] private PandaPathMover pandaPathMover;

    [Tooltip("Volume of all ambience while the panda travels (1 = unchanged, " +
             "0.5 = half, 0 = silent).")]
    [SerializeField, Range(0f, 1.5f)] private float travellingVolume = 0.5f;

    [Tooltip("Seconds to fade between the normal and the travelling volume.")]
    [SerializeField, Min(0.01f)] private float travellingFadeTime = 1.5f;

    [Header("Pollution Silences The Birds")]
    [Tooltip("When pollution (0-1) is above this, the birds fade out completely. " +
             "0 = any pollution at all silences them.")]
    [SerializeField, Range(0f, 1f)] private float birdsOffAbovePollution = 0f;

    [Tooltip("Seconds the birds take to fade out (and back in if pollution goes away).")]
    [SerializeField, Min(0.01f)] private float birdsFadeOutTime = 5f;

    [Header("Live Values (read only)")]
    [SerializeField] private float currentTemperature;
    [SerializeField] private bool travelling;
    [SerializeField, Range(0f, 1f)] private float currentPollution;
    [SerializeField, Range(0f, 1f)] private float windLevel;
    [SerializeField, Range(0f, 1f)] private float birdsLevel;
    [SerializeField, Range(0f, 1f)] private float heatLevel;

    private AudioSource windSource;
    private AudioSource birdsSource;
    private AudioSource heatSource;

    private float startTime;
    private float travelMultiplier = 1f;
    private float birdsPollutionMultiplier = 1f;

    // =========================================================
    // SETUP
    // =========================================================

    private void Start()
    {
        windSource = CreateLoop("WinterWind", winterWind);
        birdsSource = CreateLoop("Birds", birds);
        heatSource = CreateLoop("DeadlyHeat", deadlyHeat);

        startTime = Time.time;

        // Start at the right mix (silent), so it fades in correctly.
        CalculateTargets(out windLevel, out birdsLevel, out heatLevel);
    }

    private AudioSource CreateLoop(string name, AudioClip clip)
    {
        if (clip == null)
            return null;

        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // background: same volume everywhere
        source.volume = 0f;
        source.outputAudioMixerGroup = mixerGroup;

        // Random start point so the loops don't line up the same way every time.
        source.time = Random.Range(0f, clip.length * 0.9f);
        source.Play();

        return source;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        CalculateTargets(out float windTarget, out float birdsTarget, out float heatTarget);

        float step = blendSpeed * Time.deltaTime;

        windLevel = Mathf.MoveTowards(windLevel, windTarget, step);
        birdsLevel = Mathf.MoveTowards(birdsLevel, birdsTarget, step);
        heatLevel = Mathf.MoveTowards(heatLevel, heatTarget, step);

        float fadeIn = startFadeIn > 0f
            ? Mathf.Clamp01((Time.time - startTime) / startFadeIn)
            : 1f;

        UpdateTravelling();
        UpdatePollution();

        float overall = fadeIn * travelMultiplier;

        // Square root = "equal power" blend: the total loudness stays
        // even while two sounds cross-fade (no dip in the middle).
        SetVolume(windSource, Mathf.Sqrt(windLevel) * windVolume * overall);
        SetVolume(birdsSource, Mathf.Sqrt(birdsLevel) * birdsVolume * Smooth(birdsPollutionMultiplier) * overall);
        SetVolume(heatSource, Mathf.Sqrt(heatLevel) * heatVolume * overall);
    }

    private void UpdateTravelling()
    {
        travelling = pandaPathMover != null
            ? pandaPathMover.IsTravelling
            : EnvironmentSystem.Instance != null &&
              EnvironmentSystem.Instance.ActiveZone == null;

        float target = travelling ? travellingVolume : 1f;

        travelMultiplier = Mathf.MoveTowards(
            travelMultiplier,
            target,
            Mathf.Max(Mathf.Abs(1f - travellingVolume), 0.01f) / travellingFadeTime * Time.deltaTime
        );
    }

    private void UpdatePollution()
    {
        currentPollution = EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.Pollution
            : 0f;

        // Polluted: birds off. Clean: birds on.
        float target = currentPollution > birdsOffAbovePollution ? 0f : 1f;

        // Fade slowly, so the birds die away as the city appears.
        birdsPollutionMultiplier = Mathf.MoveTowards(
            birdsPollutionMultiplier,
            target,
            Time.deltaTime / birdsFadeOutTime
        );
    }

    private void CalculateTargets(out float wind, out float birdsAmount, out float heat)
    {
        currentTemperature = EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.Temperature
            : 20f;

        // 1 at coldFullAt, 0 at coldGoneAt.
        wind = Smooth(Mathf.InverseLerp(coldGoneAt, coldFullAt, currentTemperature));

        // 0 at heatStartsAt, 1 at heatFullAt.
        heat = Smooth(Mathf.InverseLerp(heatStartsAt, heatFullAt, currentTemperature));

        // Birds fill whatever the wind and heat don't.
        birdsAmount = 1f - Mathf.Max(wind, heat);
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static void SetVolume(AudioSource source, float volume)
    {
        if (source == null)
            return;

        source.volume = volume;

        // Pause silent loops to save a little work; resume when needed.
        if (volume <= 0.001f && source.isPlaying)
            source.Pause();
        else if (volume > 0.001f && !source.isPlaying)
            source.UnPause();
    }

    private void OnValidate()
    {
        // Keep the ranges in order.
        if (coldGoneAt < coldFullAt)
            coldGoneAt = coldFullAt;

        if (heatFullAt < heatStartsAt)
            heatFullAt = heatStartsAt;
    }
}