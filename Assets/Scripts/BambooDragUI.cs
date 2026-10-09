
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
    private bool dragAllowed;

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragAllowed = false;

        // Check for real bamboo before allowing the drag.
        if (BambooManager.Instance == null)
        {
            Debug.LogWarning("Cannot drag bamboo: BambooManager is missing.");
            return;
        }

        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone == null)
        {
            Debug.Log("Cannot drag bamboo: The panda is not in an ecosystem zone.");
            return;
        }

        if (!BambooManager.Instance.HasAvailableBambooInCurrentZone())
        {
            Debug.Log("Cannot drag bamboo: No available bamboo in this zone.");
            return;
        }

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