using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PollutionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pollutionPanel;
    [SerializeField] private Slider pollutionSlider;
    [SerializeField] private TMP_Text pollutionText;

    [Header("Gauge Needle (optional)")]
    [Tooltip("The needle image. Its PIVOT must be at the round end it turns around.")]
    [SerializeField] private RectTransform needle;

    [Tooltip("Z rotation of the needle at 0% (pointing at the green end).")]
    [SerializeField] private float angleAtZero = 120f;

    [Tooltip("Z rotation of the needle at 100% (pointing at the other end).")]
    [SerializeField] private float angleAtFull = -120f;

    [Header("Environment")]
    [SerializeField] private EnvironmentSystem environment;

    [Header("Text")]
    [Tooltip("Optional text in front of the percentage. Empty = just the number, e.g. '35%'.")]
    [SerializeField] private string prefix = "";

    [Header("Visibility")]
    [Tooltip("On: always visible, also at 0%. Off: only shown when there is pollution.")]
    [SerializeField] private bool alwaysVisible = true;

    [SerializeField, Range(0f, 1f)]
    private float visibilityThreshold = 0.01f;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float fillSpeed = 0.5f;

    [Header("Red Warning On A Big Jump")]
    [Tooltip("The images that flash red (e.g. the gauge background and the needle). " +
             "Empty = the images inside the Pollution Panel.")]
    [SerializeField] private Graphic[] flashGraphics;

    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("Flash when disturbance jumps up by at least this much at once (0.1 = 10%).")]
    [SerializeField, Range(0.01f, 1f)] private float flashWhenRisingBy = 0.1f;

    [Tooltip("How long it flashes (seconds).")]
    [SerializeField, Min(0.1f)] private float flashDuration = 3f;

    [Tooltip("Flashes per second.")]
    [SerializeField, Min(0.1f)] private float flashSpeed = 2f;

    [Tooltip("The gauge also grows a little while flashing (0 = off).")]
    [SerializeField, Range(0f, 0.3f)] private float flashGrow = 0f;

    [Header("Vibrate On A Big Jump")]
    [Tooltip("The part of the pollution panel that vibrates (e.g. the gauge). Empty = nothing vibrates.")]
    [SerializeField] private RectTransform vibrateTarget;

    [Tooltip("How far it moves while vibrating (pixels).")]
    [SerializeField, Range(0f, 20f)] private float vibrateStrength = 3f;

    [Tooltip("How fast it vibrates.")]
    [SerializeField, Range(5f, 60f)] private float vibrateSpeed = 35f;

    [Header("Needle Rising On A Big Jump")]
    [Tooltip("Seconds the needle takes to climb to the new value after a big jump, " +
             "so the player can SEE it go up.")]
    [SerializeField, Min(0.1f)] private float riseTime = 2.5f;

    [Tooltip("Sound played while the needle goes up after a big jump.")]
    [SerializeField] private AudioClip riseSound;

    [SerializeField, Range(0f, 1f)] private float riseSoundVolume = 0.8f;

    [Tooltip("On: the sound loops while the needle rises and stops when it arrives. " +
             "Off: it plays once when the needle starts rising.")]
    [SerializeField] private bool loopRiseSound = false;

    [SerializeField] private UnityEngine.Audio.AudioMixerGroup riseSoundMixerGroup;

    private float riseSpeed;          // pollution per second while rising
    private bool rising;
    private AudioSource riseSource;
    private Vector2 vibrateStartPosition;

    private Color[] flashOriginalColors;
    private Color textOriginalColor;
    private float flashTimer;
    private float lastTarget = -1f;
    private EcosystemZone lastZone;
    private Vector3 panelScale = Vector3.one;

    private float displayedPollution = 0f;
    private float targetPollution = 0f;

    private void Start()
    {
        if (environment == null)
            environment = EnvironmentSystem.Instance;

        if (pollutionSlider != null)
        {
            pollutionSlider.minValue = 0f;
            pollutionSlider.maxValue = 1f;
            pollutionSlider.interactable = false;
        }

        // Start at the current value instead of animating up from 0.
        displayedPollution = environment != null ? Mathf.Clamp01(environment.Pollution) : 0f;

        if (pollutionSlider != null)
            pollutionSlider.value = displayedPollution;

        UpdateNeedle();
        UpdateText();

        if (pollutionPanel != null)
            pollutionPanel.SetActive(alwaysVisible || displayedPollution > visibilityThreshold);

        // Remember the normal colours so they can come back after a flash.
        if ((flashGraphics == null || flashGraphics.Length == 0) && pollutionPanel != null)
            flashGraphics = pollutionPanel.GetComponentsInChildren<Graphic>(true);

        if (flashGraphics != null)
        {
            flashOriginalColors = new Color[flashGraphics.Length];
            for (int i = 0; i < flashGraphics.Length; i++)
                if (flashGraphics[i] != null)
                    flashOriginalColors[i] = flashGraphics[i].color;
        }

        if (pollutionText != null)
            textOriginalColor = pollutionText.color;

        if (pollutionPanel != null)
            panelScale = pollutionPanel.transform.localScale;

        if (vibrateTarget != null)
            vibrateStartPosition = vibrateTarget.anchoredPosition;

        lastTarget = displayedPollution;
    }

    private void Update()
    {
        if (environment == null)
        {
            environment = EnvironmentSystem.Instance;
            if (environment == null)
                return;
        }

        // Get the actual pollution level.
        targetPollution = Mathf.Clamp01(environment.Pollution);

        if (pollutionPanel != null)
        {
            bool showUI = alwaysVisible || targetPollution > visibilityThreshold;

            if (pollutionPanel.activeSelf != showUI)
                pollutionPanel.SetActive(showUI);
        }

        // A big jump up (e.g. the road choice): flash red.
        // (Not when it changes because the panda walked into another zone.)
        bool sameZone = environment.ActiveZone != null && environment.ActiveZone == lastZone;

        if (sameZone && lastTarget >= 0f && targetPollution - lastTarget >= flashWhenRisingBy)
        {
            flashTimer = flashDuration;

            // Climb visibly over 'Rise Time' seconds.
            rising = true;
            riseSpeed = Mathf.Max(0.001f, (targetPollution - displayedPollution) / riseTime);
            StartRiseSound();
        }

        lastTarget = targetPollution;
        lastZone = environment.ActiveZone;

        // Smoothly animate the displayed value (faster while flashing).
        float speed = rising ? riseSpeed : fillSpeed;

        displayedPollution = Mathf.MoveTowards(
            displayedPollution,
            targetPollution,
            speed * Time.deltaTime
        );

        // Arrived at the new value: stop rising (and the looping sound).
        if (rising && displayedPollution >= targetPollution - 0.0005f)
        {
            rising = false;

            if (loopRiseSound && riseSource != null && riseSource.isPlaying)
                riseSource.Stop();
        }

        UpdateFlash();

        if (pollutionSlider != null)
            pollutionSlider.value = displayedPollution;

        UpdateNeedle();
        UpdateText();
    }

    private void StartRiseSound()
    {
        if (riseSound == null)
            return;

        if (riseSource == null)
        {
            riseSource = gameObject.AddComponent<AudioSource>();
            riseSource.playOnAwake = false;
            riseSource.spatialBlend = 0f;
        }

        riseSource.clip = riseSound;
        riseSource.loop = loopRiseSound;
        riseSource.volume = riseSoundVolume;
        riseSource.outputAudioMixerGroup = riseSoundMixerGroup;
        riseSource.Play();
    }

    private void UpdateFlash()
    {
        if (flashTimer <= 0f)
        {
            if (vibrateTarget != null)
                vibrateTarget.anchoredPosition = vibrateStartPosition;

            return;
        }

        flashTimer -= Time.unscaledDeltaTime;

        // 0..1 pulse; fades out over the last second.
        float pulse = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * flashSpeed * Mathf.PI * 2f);
        float fade = Mathf.Clamp01(flashTimer);
        float amount = flashTimer > 0f ? pulse * fade : 0f;

        if (flashGraphics != null && flashOriginalColors != null)
        {
            for (int i = 0; i < flashGraphics.Length; i++)
            {
                if (flashGraphics[i] == null || flashGraphics[i] == pollutionText)
                    continue;

                Color c = Color.Lerp(flashOriginalColors[i], flashColor, amount);
                c.a = flashOriginalColors[i].a;
                flashGraphics[i].color = c;
            }
        }

        if (pollutionText != null)
            pollutionText.color = Color.Lerp(textOriginalColor, flashColor, amount);

        if (pollutionPanel != null)
            pollutionPanel.transform.localScale = panelScale * (1f + flashGrow * amount);

        // Small, fast vibration that fades out with the flash.
        if (vibrateTarget != null)
        {
            float t = Time.unscaledTime * vibrateSpeed;
            Vector2 offset = new Vector2(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f) * vibrateStrength * fade;

            vibrateTarget.anchoredPosition = vibrateStartPosition + offset;
        }
    }

    private void UpdateNeedle()
    {
        if (needle == null)
            return;

        float angle = Mathf.Lerp(angleAtZero, angleAtFull, displayedPollution);
        needle.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private void UpdateText()
    {
        if (pollutionText != null)
            pollutionText.text = prefix + Mathf.RoundToInt(displayedPollution * 100f) + "%";
    }
}