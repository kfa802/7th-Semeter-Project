using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A blinking, glowing arrow that hints the player can drag a slider.
///
/// Put this on the arrow Image (a child of the Canvas, placed next to the
/// temperature slider). Use one arrow pointing up and one pointing down,
/// or a single up/down arrow.
/// </summary>
[RequireComponent(typeof(Image))]
public class SliderHintArrow : MonoBehaviour
{
    [Header("Slider")]
    [Tooltip("The temperature slider.")]
    [SerializeField] private Slider slider;

    [Header("Colour")]
    [Tooltip("Colour of the arrow (alpha is controlled by the blink below).")]
    [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("Colour of the soft glow around the arrow.")]
    [SerializeField] private Color glowColor = new Color(1f, 0.7f, 0.1f, 1f);

    [Header("Blink")]
    [Tooltip("Lowest transparency while blinking (0 = invisible, 1 = solid).")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;

    [Tooltip("Highest transparency while blinking. Below 1 = always a bit see-through.")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;

    [Tooltip("Blinks per second.")]
    [SerializeField, Min(0.05f)] private float blinkSpeed = 1f;

    [Header("Glow")]
    [SerializeField] private bool useGlow = true;

    [Tooltip("How much bigger the glow is than the arrow (1.4 = 40% bigger).")]
    [SerializeField, Range(1f, 2.5f)] private float glowSize = 1.4f;

    [Tooltip("Strength of the glow compared to the arrow.")]
    [SerializeField, Range(0f, 1f)] private float glowStrength = 0.6f;

    [Header("Movement (optional)")]
    [Tooltip("Pixels the arrow moves back and forth. 0 = no movement.")]
    [SerializeField] private float bobDistance = 6f;

    [Tooltip("Direction it moves: (0,1) = up, (0,-1) = down.")]
    [SerializeField] private Vector2 bobDirection = Vector2.up;

    [Tooltip("Tick on ONE of the two arrows: it moves the opposite way of the other.")]
    [SerializeField] private bool reverseDirection = false;

    [Header("When To Show")]
    [Tooltip("Hide while the slider is locked (travelling, dilemma open).")]
    [SerializeField] private bool hideWhenSliderLocked = true;

    [Tooltip("On: the arrow disappears for good once the player has used the slider.")]
    [SerializeField] private bool hideAfterFirstUse = false;

    [Tooltip("Hide while the player is dragging the slider. It comes back after " +
             "'Show Again After' seconds (unless Hide After First Use is on).")]
    [SerializeField] private bool hideWhileUsing = true;

    [Tooltip("Seconds after the player lets go before the arrow comes back.")]
    [SerializeField, Min(0f)] private float showAgainAfter = 5f;

    [Tooltip("Seconds to fade in/out when showing or hiding.")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.4f;

    private Image arrow;
    private Image glow;
    private RectTransform glowRect;
    private RectTransform rect;
    private Vector2 startPosition;

    private float visibility;          // 0 hidden, 1 shown
    private float lastUsedTime = -999f;
    private bool used;
    private bool dragging;

    private void Awake()
    {
        arrow = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        startPosition = rect.anchoredPosition;

        // The arrow should never block clicks on the slider.
        arrow.raycastTarget = false;

        if (useGlow)
            CreateGlow();
    }

    private void Start()
    {
        if (slider != null)
        {
            // Notice when the player presses/releases the slider.
            EventTrigger trigger = slider.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = slider.gameObject.AddComponent<EventTrigger>();

            AddEvent(trigger, EventTriggerType.PointerDown, () => { dragging = true; MarkUsed(); });
            AddEvent(trigger, EventTriggerType.PointerUp, () => { dragging = false; MarkUsed(); });
        }

        visibility = ShouldShow() ? 1f : 0f;
    }

    private void CreateGlow()
    {
        // The glow is a copy of the arrow placed just BEFORE it in the
        // hierarchy, so it is drawn behind the arrow.
        GameObject glowObject = new GameObject(name + " Glow", typeof(RectTransform), typeof(Image));
        glowObject.transform.SetParent(transform.parent, false);
        glowObject.transform.SetSiblingIndex(transform.GetSiblingIndex());

        glowRect = glowObject.GetComponent<RectTransform>();

        glow = glowObject.GetComponent<Image>();
        glow.sprite = arrow.sprite;
        glow.preserveAspect = arrow.preserveAspect;
        glow.raycastTarget = false;

        SyncGlowRect();
    }

    private void SyncGlowRect()
    {
        if (glowRect == null)
            return;

        glowRect.anchorMin = rect.anchorMin;
        glowRect.anchorMax = rect.anchorMax;
        glowRect.pivot = rect.pivot;
        glowRect.sizeDelta = rect.sizeDelta;
        glowRect.anchoredPosition = rect.anchoredPosition;
        glowRect.localRotation = rect.localRotation;
        glowRect.localScale = rect.localScale * glowSize;
    }

    private void OnDisable()
    {
        if (glow != null)
            glow.enabled = false;
    }

    private void OnEnable()
    {
        if (glow != null)
            glow.enabled = true;
    }

    private void OnDestroy()
    {
        if (glow != null)
            Destroy(glow.gameObject);
    }

    private void Update()
    {
        // Fade in/out.
        float target = ShouldShow() ? 1f : 0f;
        visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime / fadeTime);

        // Blink: smooth wave between 0 and 1.
        float wave = (Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave) * visibility;

        Color c = arrowColor;
        c.a = alpha;
        arrow.color = c;

        if (glow != null)
        {
            Color g = glowColor;
            g.a = alpha * glowStrength;
            glow.color = g;
        }

        // Gentle movement in time with the blink.
        if (bobDistance != 0f)
        {
            Vector2 direction = bobDirection.normalized * (reverseDirection ? -1f : 1f);
            rect.anchoredPosition = startPosition + direction * bobDistance * wave;
        }

        SyncGlowRect();
    }

    private bool ShouldShow()
    {
        if (slider == null)
            return true;

        if (hideWhenSliderLocked && !slider.interactable)
            return false;

        if (hideAfterFirstUse && used)
            return false;

        if (hideWhileUsing &&
            (dragging || Time.unscaledTime - lastUsedTime < showAgainAfter))
            return false;

        return true;
    }

    private void MarkUsed()
    {
        // Ignore changes the game makes itself (e.g. while travelling).
        if (!slider.interactable)
            return;

        used = true;
        lastUsedTime = Time.unscaledTime;
    }

    private static void AddEvent(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}