using UnityEngine;
using UnityEngine.EventSystems;

public class BambooDragUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
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