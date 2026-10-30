using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a slider handle look interactable: it gently pulses and has a
/// soft glow behind it. The pulse pauses while the player uses the slider
/// and comes back after a few idle seconds. It hides when the slider is
/// locked (e.g. while travelling or during a dilemma).
///
/// Put this on the slider's Handle object.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SliderHandlePulse : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The slider this handle belongs to. Found automatically if empty.")]
    [SerializeField] private Slider slider;

    [Tooltip("Optional: your own glow sprite. If empty, a soft circle is generated.")]
    [SerializeField] private Sprite glowSprite;

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Glow size compared to the handle (1.8 = 80% bigger).")]
    [SerializeField, Min(1f)] private float glowSize = 1.9f;

    [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.85f;

    [Header("Pulse")]
    [Tooltip("Pulses per second.")]
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1f;

    [Tooltip("How much the handle grows on each pulse (0.08 = 8%).")]
    [SerializeField, Range(0f, 0.3f)] private float scaleAmount = 0.08f;

    [Tooltip("How much the glow grows on each pulse.")]
    [SerializeField, Range(0f, 0.5f)] private float glowScaleAmount = 0.15f;

    [Header("Behaviour")]
    [Tooltip("Seconds without input before the pulse comes back after the player used the slider.")]
    [SerializeField, Min(0f)] private float idleDelay = 4f;

    [Tooltip("Stop pulsing for good after the player has used the slider once.")]
    [SerializeField] private bool stopAfterFirstUse = false;

    [Tooltip("Hide the effect while the slider is not interactable.")]
    [SerializeField] private bool onlyWhenInteractable = true;

    [Tooltip("How fast the effect fades in and out.")]
    [SerializeField, Min(0.01f)] private float fadeSpeed = 3f;

    private RectTransform handle;
    private RectTransform glowRect;
    private Image glowImage;

    private Vector3 baseScale;
    private float intensity;       // 0 = hidden, 1 = fully visible
    private float lastUseTime = -999f;
    private bool usedOnce;

    private static Sprite generatedGlow;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Awake()
    {
        handle = (RectTransform)transform;
        baseScale = handle.localScale;

        if (slider == null)
            slider = GetComponentInParent<Slider>();

        CreateGlow();
    }

    private void OnEnable()
    {
        // onValueChanged only fires from the player here, because
        // EnvironmentUI updates the slider with SetValueWithoutNotify.
        if (slider != null)
            slider.onValueChanged.AddListener(OnSliderUsed);

        if (glowRect != null)
            glowRect.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderUsed);

        if (handle != null)
            handle.localScale = baseScale;

        if (glowRect != null)
            glowRect.gameObject.SetActive(false);

        intensity = 0f;
    }

    private void OnDestroy()
    {
        if (glowRect != null)
            Destroy(glowRect.gameObject);
    }

    private void OnSliderUsed(float value)
    {
        lastUseTime = Time.unscaledTime;
        usedOnce = true;
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void LateUpdate()
    {
        float now = Time.unscaledTime;

        bool locked =
            onlyWhenInteractable &&
            slider != null &&
            !slider.IsInteractable();

        bool recentlyUsed = now - lastUseTime < idleDelay;
        bool finished = stopAfterFirstUse && usedOnce;

        bool show = !locked && !recentlyUsed && !finished;

        intensity = Mathf.MoveTowards(
            intensity,
            show ? 1f : 0f,
            fadeSpeed * Time.unscaledDeltaTime
        );

        // Smooth 0..1 wave.
        float wave = 0.5f - 0.5f * Mathf.Cos(now * pulseSpeed * Mathf.PI * 2f);

        // Handle pulse.
        handle.localScale = baseScale * (1f + scaleAmount * wave * intensity);

        // Glow follows the handle and pulses.
        UpdateGlow(wave);
    }

    // =====================================================
    // GLOW
    // =====================================================

    private void CreateGlow()
    {
        if (handle.parent == null)
            return;

        GameObject glow = new GameObject(
            "HandleGlow (runtime)",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        glowRect = (RectTransform)glow.transform;

        // Sibling of the handle, placed just BEHIND it.
        glowRect.SetParent(handle.parent, false);
        glowRect.SetSiblingIndex(handle.GetSiblingIndex());

        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);

        glowImage = glow.GetComponent<Image>();
        glowImage.sprite = glowSprite != null ? glowSprite : GetGeneratedGlow();
        glowImage.raycastTarget = false;    // never block the slider
        glowImage.preserveAspect = true;
        glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
    }

    private void UpdateGlow(float wave)
    {
        if (glowRect == null)
            return;

        // Center on the handle, wherever the slider has moved it.
        glowRect.position = handle.TransformPoint(handle.rect.center);

        // Size from the handle's visible size (works with scaled handles).
        Vector3 scale = handle.lossyScale;
        Vector3 parentScale = glowRect.parent.lossyScale;

        float visible = Mathf.Max(
            handle.rect.width * Mathf.Abs(scale.x) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            handle.rect.height * Mathf.Abs(scale.y) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y))
        );

        float size = visible * glowSize * (1f + glowScaleAmount * wave);
        glowRect.sizeDelta = new Vector2(size, size);

        Color c = glowColor;
        c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, wave) * intensity * glowColor.a;
        glowImage.color = c;
    }

    // A soft round glow made in code, so no extra asset is needed.
    private static Sprite GetGeneratedGlow()
    {
        if (generatedGlow != null)
            return generatedGlow;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));

            // Bright centre, soft fade to the edge.
            float a = Mathf.Pow(1f - d, 2f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedGlow = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f)
        );

        generatedGlow.name = "Generated Handle Glow";
        return generatedGlow;
    }
}