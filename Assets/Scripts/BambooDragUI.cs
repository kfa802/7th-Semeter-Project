using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BambooDragUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerDownHandler
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GameObject bambooPrefab;

    [Header("Panda")]
    [SerializeField] private LayerMask pandaLayer;
    [SerializeField] private float pandaDetectionRadius = 1f;
    [SerializeField] private PandaFeedingZone pandaFeedingZone;

    [Header("Drag Settings")]
    [SerializeField] private float dragHeight = 0.5f;

    [Header("Available Animation")]
    [Tooltip("Animator that plays the 'you can feed' animation.")]
    [SerializeField] private Animator availableAnimator;

    [Header("Click Sound")]
    [Tooltip("Played when the box is clicked/pressed and the panda can be fed.")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("Optional: played instead when the box is pressed but feeding isn't " +
             "possible (no bamboo, panda busy). Empty = no sound then.")]
    [SerializeField] private AudioClip cantFeedSound;

    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.8f;

    [Tooltip("Optional: send the click through a group in your Audio Mixer.")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup clickMixerGroup;

    private AudioSource clickSource;

    [Header("Hunger Attention")]
    [Tooltip("The panda. Found automatically if empty.")]
    [SerializeField] private PandaSystem panda;

    [Tooltip("The box that glows and pulses. Empty = this object.")]
    [SerializeField] private RectTransform attentionTarget;

    [Tooltip("Only draw attention when the panda can actually be fed right now.")]
    [SerializeField] private bool onlyWhenFeedingPossible = true;

    [Tooltip("Strength (0-1) as soon as the panda becomes hungry.")]
    [SerializeField, Range(0f, 1f)] private float hungryStart = 0.35f;

    [Tooltip("Strength (0-1) it builds up to while hungry (not yet very hungry).")]
    [SerializeField, Range(0f, 1f)] private float hungryMax = 0.65f;

    [Tooltip("Seconds to build up from Hungry Start to Hungry Max.")]
    [SerializeField, Min(0.1f)] private float hungryBuildUpTime = 10f;

    [Tooltip("Strength when VERY hungry.")]
    [SerializeField, Range(0f, 1f)] private float veryHungryStrength = 1f;

    [Tooltip("How quickly the effect follows changes (higher = faster).")]
    [SerializeField, Min(0.1f)] private float strengthChangeSpeed = 1f;

    [Header("Glow")]
    [Tooltip("Optional: your own glow sprite. Empty = a soft glow is generated.")]
    [SerializeField] private Sprite glowSprite;

    [SerializeField] private Color glowColorMild = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color glowColorIntense = new Color(1f, 0.45f, 0.15f, 1f);

    [Tooltip("Glow size compared to the box at full strength (1.4 = 40% bigger).")]
    [SerializeField, Range(1f, 2.5f)] private float glowSize = 1.45f;

    [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.9f;

    [Header("Pulse")]
    [Tooltip("How much the box grows on each pulse at full strength (0.1 = 10%).")]
    [SerializeField, Range(0f, 0.3f)] private float pulseAmount = 0.1f;

    [Tooltip("Pulses per second: from mild to full strength.")]
    [SerializeField, Min(0.1f)] private float pulseSpeedMild = 0.8f;
    [SerializeField, Min(0.1f)] private float pulseSpeedIntense = 1.8f;

    [Header("Wiggle (very hungry)")]
    [Tooltip("Above this strength the box also wiggles now and then.")]
    [SerializeField, Range(0f, 1f)] private float wiggleFrom = 0.9f;

    [Tooltip("Degrees it tilts while wiggling.")]
    [SerializeField, Range(0f, 20f)] private float wiggleAngle = 6f;

    [Tooltip("Seconds between wiggles.")]
    [SerializeField, Min(0.5f)] private float wiggleEvery = 2.5f;

    [Header("Blink (very hungry)")]
    [Tooltip("Above this strength the whole box blinks.")]
    [SerializeField, Range(0f, 1f)] private float blinkFrom = 0.9f;

    [Tooltip("The box's Image. Empty = the Image on the Attention Target.")]
    [SerializeField] private Image boxImage;

    [Tooltip("Colour the box flashes to.")]
    [SerializeField] private Color blinkColor = new Color(1f, 0.6f, 0.3f, 1f);

    [Tooltip("How far it flashes towards the blink colour (1 = fully).")]
    [SerializeField, Range(0f, 1f)] private float blinkAmount = 0.7f;

    [Tooltip("Blinks per second.")]
    [SerializeField, Min(0.1f)] private float blinkSpeed = 2f;

    private float blinkStrength;
    private Color boxBaseColor;
    private Color lastWrittenColor;

    private float attentionStrength;
    private float hungrySince = -1f;
    private float pulsePhase;
    private float wiggleTimer;

    private RectTransform glowRect;
    private Image glowImage;

    // Remember what WE wrote, so an Animator on the box can still animate it.
    private Vector3 baseScale;
    private Vector3 lastWrittenScale;
    private float baseAngle;
    private float lastWrittenAngle;

    private static Sprite generatedGlow;

    private GameObject draggedBamboo;
    private Plane fallbackPlane;
    private bool overPanda;
    private bool dragAllowed;
    private bool lastCanFeed;

    // =========================================================
    // CAN FEED CHECK + ANIMATION
    // =========================================================

    private bool CanStartDrag()
    {
        return pandaFeedingZone != null && pandaFeedingZone.CanFeed;
    }

    private void Start()
    {
        if (pandaFeedingZone == null)
            Debug.LogWarning("BambooDragUI: Panda Feeding Zone is not assigned.", this);

        if (availableAnimator == null)
            Debug.LogWarning("BambooDragUI: Available Animator is not assigned.", this);

        lastCanFeed = CanStartDrag();
        SetAnimationActive(lastCanFeed);

        if (panda == null)
            panda = FindFirstObjectByType<PandaSystem>();

        if (attentionTarget == null)
            attentionTarget = transform as RectTransform;

        if (attentionTarget != null)
        {
            baseScale = lastWrittenScale = attentionTarget.localScale;
            baseAngle = lastWrittenAngle = attentionTarget.localEulerAngles.z;
            CreateGlow();

            if (boxImage == null)
                boxImage = attentionTarget.GetComponent<Image>();

            if (boxImage != null)
                boxBaseColor = lastWrittenColor = boxImage.color;
        }
    }

    // =========================================================
    // HUNGER ATTENTION
    // =========================================================

    private float GetTargetStrength()
    {
        if (panda == null)
            return 0f;

        bool isDragging = dragAllowed && draggedBamboo != null;

        if (isDragging || (onlyWhenFeedingPossible && !CanStartDrag()))
            return 0f;

        if (!panda.IsHungry)
        {
            hungrySince = -1f;
            return 0f;
        }

        if (hungrySince < 0f)
            hungrySince = Time.time;

        if (panda.IsVeryHungry)
            return veryHungryStrength;

        // Builds up the longer the panda stays hungry.
        float t = Mathf.Clamp01((Time.time - hungrySince) / hungryBuildUpTime);
        return Mathf.Lerp(hungryStart, hungryMax, t);
    }

    private void LateUpdate()
    {
        if (attentionTarget == null)
            return;

        attentionStrength = Mathf.MoveTowards(
            attentionStrength, GetTargetStrength(), strengthChangeSpeed * Time.deltaTime);

        float s = attentionStrength;

        // Pulse: faster and bigger the hungrier the panda is.
        pulsePhase += Time.deltaTime * Mathf.Lerp(pulseSpeedMild, pulseSpeedIntense, s) * Mathf.PI * 2f;
        float wave = 0.5f - 0.5f * Mathf.Cos(pulsePhase);

        // Something else (e.g. the Animator) changed the box: use that as the base.
        if (attentionTarget.localScale != lastWrittenScale)
            baseScale = attentionTarget.localScale;

        if (!Mathf.Approximately(attentionTarget.localEulerAngles.z, lastWrittenAngle))
            baseAngle = attentionTarget.localEulerAngles.z;

        attentionTarget.localScale = baseScale * (1f + pulseAmount * wave * s);
        lastWrittenScale = attentionTarget.localScale;

        // Wiggle now and then when very hungry.
        float angle = 0f;

        if (s >= wiggleFrom && wiggleAngle > 0f)
        {
            wiggleTimer += Time.deltaTime;

            if (wiggleTimer >= wiggleEvery)
                wiggleTimer = 0f;

            const float wiggleDuration = 0.5f;

            if (wiggleTimer < wiggleDuration)
            {
                float w = wiggleTimer / wiggleDuration;
                angle = Mathf.Sin(w * Mathf.PI * 6f) * (1f - w) * wiggleAngle;
            }
        }
        else
        {
            wiggleTimer = 0f;
        }

        Vector3 euler = attentionTarget.localEulerAngles;
        euler.z = baseAngle + angle;
        attentionTarget.localEulerAngles = euler;
        lastWrittenAngle = attentionTarget.localEulerAngles.z;

        UpdateGlow(wave, s);
        UpdateBlink(s);
    }

    private void UpdateBlink(float s)
    {
        if (boxImage == null)
            return;

        // Something else changed the colour: use that as the normal colour.
        if (boxImage.color != lastWrittenColor)
            boxBaseColor = boxImage.color;

        // Fade the blink in/out smoothly instead of switching it on at once.
        blinkStrength = Mathf.MoveTowards(
            blinkStrength, s >= blinkFrom ? 1f : 0f, 2f * Time.deltaTime);

        // Sharper than a sine, so it reads as a blink rather than a glow.
        float wave = Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f);
        float flash = Mathf.Clamp01(wave * 1.5f) * blinkAmount * blinkStrength;

        Color c = Color.Lerp(boxBaseColor, blinkColor, flash);
        c.a = boxBaseColor.a;

        boxImage.color = c;
        lastWrittenColor = c;
    }

    private void UpdateGlow(float wave, float s)
    {
        if (glowRect == null)
            return;

        glowRect.position = attentionTarget.TransformPoint(attentionTarget.rect.center);
        glowRect.rotation = attentionTarget.rotation;

        Vector3 scale = attentionTarget.lossyScale;
        Vector3 parentScale = glowRect.parent.lossyScale;

        float width = attentionTarget.rect.width * Mathf.Abs(scale.x) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float height = attentionTarget.rect.height * Mathf.Abs(scale.y) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));

        float grow = Mathf.Lerp(1.1f, glowSize, s) * (1f + 0.08f * wave);
        float extra = Mathf.Min(width, height) * (grow - 1f);
        glowRect.sizeDelta = new Vector2(width + extra, height + extra);

        Color c = Color.Lerp(glowColorMild, glowColorIntense, s);
        c.a = glowMaxAlpha * Mathf.Lerp(0.35f, 1f, wave) * s;
        glowImage.color = c;
        glowImage.enabled = s > 0.001f;
    }

    private void CreateGlow()
    {
        if (attentionTarget.parent == null)
            return;

        GameObject glow = new GameObject(
            attentionTarget.name + " Hunger Glow (runtime)",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        glowRect = (RectTransform)glow.transform;

        // Placed just BEFORE the box in the hierarchy, so it is drawn behind it.
        glowRect.SetParent(attentionTarget.parent, false);
        glowRect.SetSiblingIndex(attentionTarget.GetSiblingIndex());
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);

        glowImage = glow.GetComponent<Image>();
        glowImage.sprite = glowSprite != null ? glowSprite : GetGeneratedGlow();
        glowImage.type = Image.Type.Sliced;
        glowImage.raycastTarget = false;
        glowImage.enabled = false;
    }

    private void OnDestroy()
    {
        if (glowRect != null)
            Destroy(glowRect.gameObject);
    }

    // A soft rounded glow (9-sliced, so it fits the box's shape).
    private static Sprite GetGeneratedGlow()
    {
        if (generatedGlow != null)
            return generatedGlow;

        const int size = 64;
        const float border = 24f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(0f, border - Mathf.Min(x, size - 1 - x));
            float dy = Mathf.Max(0f, border - Mathf.Min(y, size - 1 - y));
            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / border);
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f));
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedGlow = Sprite.Create(
            texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));

        generatedGlow.name = "Generated Hunger Glow";
        return generatedGlow;
    }

    private void Update()
    {
        bool isDragging = dragAllowed && draggedBamboo != null;
        bool canFeed = CanStartDrag() && !isDragging;

        if (canFeed != lastCanFeed)
        {
            lastCanFeed = canFeed;
            SetAnimationActive(canFeed);
        }
    }

    private void SetAnimationActive(bool active)
    {
        Debug.Log("BambooDragUI: Feed animation " + (active ? "ON" : "OFF"), this);

        if (availableAnimator == null)
            return;

        if (active)
        {
            // Start the animation from the beginning.
            availableAnimator.enabled = true;
            availableAnimator.Rebind();
            availableAnimator.Update(0f);
        }
        else
        {
            // Reset to the normal pose, then stop animating.
            availableAnimator.Rebind();
            availableAnimator.Update(0f);
            availableAnimator.enabled = false;
        }
    }

    // =========================================================
    // CLICK SOUND
    // =========================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        AudioClip clip = CanStartDrag() ? clickSound : cantFeedSound;

        if (clip == null)
            return;

        if (clickSource == null)
        {
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 0f;   // UI sound: same volume everywhere
            clickSource.outputAudioMixerGroup = clickMixerGroup;
        }

        clickSource.pitch = Random.Range(0.97f, 1.03f);
        clickSource.PlayOneShot(clip, clickVolume);
    }

    // =========================================================
    // DRAGGING
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragAllowed = false;

        if (!CanStartDrag())
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null || bambooPrefab == null)
        {
            Debug.LogWarning("BambooDragUI: Camera or bamboo prefab is missing.");
            return;
        }

        dragAllowed = true;

        draggedBamboo = Instantiate(bambooPrefab);

        fallbackPlane = new Plane(
            Vector3.up,
            new Vector3(0f, dragHeight, 0f)
        );

        UpdateBambooPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragAllowed && draggedBamboo != null)
            UpdateBambooPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragAllowed || draggedBamboo == null)
        {
            dragAllowed = false;
            return;
        }

        UpdateBambooPosition(eventData);

        if (overPanda)
        {
            Collider[] colliders = Physics.OverlapSphere(
                draggedBamboo.transform.position,
                pandaDetectionRadius,
                pandaLayer,
                QueryTriggerInteraction.Collide
            );

            foreach (Collider collider in colliders)
            {
                PandaFeedingZone feedingZone =
                    collider.GetComponent<PandaFeedingZone>();

                if (feedingZone == null)
                    feedingZone = collider.GetComponentInParent<PandaFeedingZone>();

                if (feedingZone == null)
                    feedingZone = collider.GetComponentInChildren<PandaFeedingZone>();

                if (feedingZone != null)
                {
                    feedingZone.FeedPanda();
                    break;
                }
            }
        }

        Destroy(draggedBamboo);
        draggedBamboo = null;
        dragAllowed = false;
    }

    private void UpdateBambooPosition(PointerEventData eventData)
    {
        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        Vector3 worldPosition;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            1000f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            worldPosition = hit.point;
            worldPosition.y += dragHeight;
        }
        else if (fallbackPlane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
        }
        else
        {
            return;
        }

        draggedBamboo.transform.position = worldPosition;

        Collider[] colliders = Physics.OverlapSphere(
            worldPosition,
            pandaDetectionRadius,
            pandaLayer,
            QueryTriggerInteraction.Collide
        );

        overPanda = colliders.Length > 0;
    }
}