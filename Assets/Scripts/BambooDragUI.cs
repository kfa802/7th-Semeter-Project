
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

    [Header("Drag Settings")]
    [SerializeField] private float dragHeight = 0.5f;

    private GameObject draggedBamboo;
    private Plane fallbackPlane;
    private bool overPanda;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
        {
            Debug.LogError("BambooDragUI: No World Camera assigned and no MainCamera found.");
            return;
        }

        if (bambooPrefab == null)
        {
            Debug.LogError("BambooDragUI: Bamboo Prefab is not assigned.");
            return;
        }

        draggedBamboo = Instantiate(bambooPrefab);
        Debug.Log("BambooDragUI: Bamboo spawned.");

        fallbackPlane = new Plane(
            Vector3.up,
            new Vector3(0f, dragHeight, 0f)
        );

        UpdateBambooPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedBamboo != null)
            UpdateBambooPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedBamboo == null)
            return;

        UpdateBambooPosition(eventData);

        if (!overPanda)
        {
            Debug.Log("BambooDragUI: Bamboo dropped, but no panda collider was detected.");
            Destroy(draggedBamboo);
            draggedBamboo = null;
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(
            draggedBamboo.transform.position,
            pandaDetectionRadius,
            pandaLayer,
            QueryTriggerInteraction.Collide
        );

        bool fed = false;

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
                Debug.Log("BambooDragUI: FeedPanda() called successfully.");
                fed = true;
                break;
            }
        }

        if (!fed)
        {
            Debug.LogWarning(
                "BambooDragUI: Panda collider detected, but no PandaFeedingZone was found. " +
                "Check the collider's object and its parents."
            );
        }

        Destroy(draggedBamboo);
        draggedBamboo = null;
    }

    private void UpdateBambooPosition(PointerEventData eventData)
    {
        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        Vector3 worldPosition;

        // Prefer the actual terrain or ground collider.
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