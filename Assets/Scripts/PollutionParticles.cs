using UnityEngine;

/// <summary>
/// Pollution smog for one zone. Fades in smoothly when the panda enters the zone
/// (and the zone is polluted), and fades out when the panda leaves.
///
/// Two ways to detect the panda:
///  - Default: uses environment.ActiveZone (your existing system).
///  - Use Trigger = on: put a Collider with "Is Trigger" on this object (covering the zone),
///    and give the panda the tag below + a Rigidbody/CharacterController.
/// </summary>
public class PollutionParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private EcosystemZone thisZone;
    [SerializeField] private ParticleSystem pollutionParticles;

    [Header("Panda detection")]
    [Tooltip("Off = use environment.ActiveZone. On = use a trigger collider on this object.")]
    [SerializeField] private bool useTrigger = false;
    [SerializeField] private string pandaTag = "Player";

    [Header("Pollution Settings")]
    [SerializeField, Range(0f, 1f)]
    private float pollutionThreshold = 0.01f;

    [Tooltip("Particles per second at 100% pollution.")]
    [SerializeField, Min(0f)]
    private float maximumEmission = 30f;

    [Tooltip("Hard cap on how many particles can exist at once.")]
    [SerializeField, Min(1)]
    private int maximumParticles = 300;

    [Header("Strength (how visible the smog is)")]
    [SerializeField, Range(0f, 1f)] private float minStrength = 0.15f;
    [Tooltip("Opacity at 100% pollution. Lower this if high pollution is too thick.")]
    [SerializeField, Range(0f, 1f)] private float maxStrength = 0.45f;
    [SerializeField]
    private AnimationCurve strengthCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("Fade Settings")]
    [Tooltip("Seconds to fade in after the panda enters the zone.")]
    [SerializeField, Min(0.1f)] private float fadeInDuration = 3f;
    [Tooltip("Seconds to fade out after the panda leaves the zone.")]
    [SerializeField, Min(0.1f)] private float fadeOutDuration = 2f;
    [Tooltip("How fast the smog follows pollution changes inside the zone (seconds).")]
    [SerializeField, Min(0.01f)] private float pollutionSmoothing = 1.5f;

    private ParticleSystem.EmissionModule emission;
    private ParticleSystemRenderer psRenderer;
    private MaterialPropertyBlock block;
    private Color baseColor = Color.white;
    private int colorId;

    private float currentFade = 0f;      // 0 = invisible, 1 = fully faded in
    private float smoothPollution = 0f;
    private bool pandaInside = false;    // used in trigger mode

    private void Awake()
    {
        if (pollutionParticles == null)
            pollutionParticles = GetComponent<ParticleSystem>();
        if (pollutionParticles == null)
            return;

        emission = pollutionParticles.emission;
        emission.rateOverTime = 0f;

        var main = pollutionParticles.main;
        main.maxParticles = maximumParticles;
        main.playOnAwake = false;

        psRenderer = pollutionParticles.GetComponent<ParticleSystemRenderer>();
        block = new MaterialPropertyBlock();

        Material mat = psRenderer.sharedMaterial;
        colorId = (mat != null && mat.HasProperty("_BaseColor"))
            ? Shader.PropertyToID("_BaseColor")   // URP
            : Shader.PropertyToID("_Color");      // Built-in
        if (mat != null && mat.HasProperty(colorId))
            baseColor = mat.GetColor(colorId);

        ApplyAlpha(0f);
        pollutionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool PandaIsInZone()
    {
        if (useTrigger) return pandaInside;
        return environment != null && thisZone != null && environment.ActiveZone == thisZone;
    }

    private void Update()
    {
        if (pollutionParticles == null || environment == null)
            return;

        bool inZone = PandaIsInZone();
        float rawPollution = inZone ? Mathf.Clamp01(environment.Pollution) : 0f;
        bool shouldShow = inZone && rawPollution > pollutionThreshold;

        // Start the system the moment the panda enters. It is still invisible
        // (alpha 0), so Prewarm can fill the zone without a pop, then it fades in.
        if (shouldShow && !pollutionParticles.isPlaying)
        {
            currentFade = 0f;
            smoothPollution = rawPollution;
            pollutionParticles.Play(true);
        }

        // Fade in/out
        float target = shouldShow ? 1f : 0f;
        float duration = target > currentFade ? fadeInDuration : fadeOutDuration;
        currentFade = Mathf.MoveTowards(currentFade, target, Time.deltaTime / duration);
        float eased = Mathf.SmoothStep(0f, 1f, currentFade); // soft start and end

        // Follow pollution changes gently
        if (shouldShow)
            smoothPollution = Mathf.Lerp(smoothPollution, rawPollution, 1f - Mathf.Exp(-Time.deltaTime / pollutionSmoothing));

        float intensity = Mathf.InverseLerp(pollutionThreshold, 1f, smoothPollution);
        float shaped = Mathf.Clamp01(strengthCurve.Evaluate(intensity));

        emission.rateOverTime = shouldShow ? maximumEmission * shaped : 0f;
        ApplyAlpha(eased * Mathf.Lerp(minStrength, maxStrength, shaped));

        // Clear only after it is completely faded out
        if (!shouldShow && currentFade <= 0f && pollutionParticles.isPlaying)
            pollutionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(pandaTag)) pandaInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(pandaTag)) pandaInside = false;
    }

    private void ApplyAlpha(float alpha)
    {
        psRenderer.GetPropertyBlock(block);
        Color c = baseColor;
        c.a = baseColor.a * alpha;
        block.SetColor(colorId, c);
        psRenderer.SetPropertyBlock(block);
    }
}