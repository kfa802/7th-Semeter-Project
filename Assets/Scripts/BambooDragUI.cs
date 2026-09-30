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

    [Header("Drag Settings")]
    [SerializeField] private float dragHeight = 0.5f;
    [SerializeField] private float pandaDetectionRadius = 0.5f;

    private GameObject draggedBamboo;

    private Plane dragPlane;

    private bool overPanda;


    // =========================================================
    // START DRAG
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (bambooPrefab == null)
        {
            Debug.LogError(
                "BambooDragUI: Bamboo Prefab is not assigned."
            );

            return;
        }


        draggedBamboo = Instantiate(
            bambooPrefab
        );


        dragPlane = new Plane(
            Vector3.up,
            new Vector3(0f, dragHeight, 0f)
        );


        UpdateBambooPosition(eventData);
    }


    // =========================================================
    // DRAG
    // =========================================================

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedBamboo == null)
            return;

        UpdateBambooPosition(eventData);
    }


    // =========================================================
    // RELEASE
    // =========================================================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedBamboo == null)
            return;


        // -----------------------------------------------------
        // RELEASED ON PANDA
        // -----------------------------------------------------

        if (overPanda)
        {
            FeedPanda();

            Destroy(draggedBamboo);

            draggedBamboo = null;

            return;
        }


        // -----------------------------------------------------
        // RELEASED ANYWHERE ELSE
        // -----------------------------------------------------

        Destroy(draggedBamboo);

        draggedBamboo = null;
    }


    // =========================================================
    // UPDATE BAMBOO POSITION
    // =========================================================

    private void UpdateBambooPosition(
        PointerEventData eventData
    )
    {
        Ray ray =
            worldCamera.ScreenPointToRay(
                eventData.position
            );


        if (!dragPlane.Raycast(
            ray,
            out float distance
        ))
        {
            return;
        }


        Vector3 worldPosition =
            ray.GetPoint(distance);


        worldPosition.y = dragHeight;


        draggedBamboo.transform.position =
            worldPosition;


        CheckPanda(
            worldPosition
        );
    }


    // =========================================================
    // CHECK PANDA
    // =========================================================

    private void CheckPanda(
        Vector3 position
    )
    {
        overPanda = false;


        Collider[] colliders =
            Physics.OverlapSphere(
                position,
                pandaDetectionRadius,
                pandaLayer
            );


        if (colliders.Length > 0)
        {
            overPanda = true;
        }
    }


    // =========================================================
    // FEED PANDA
    // =========================================================

    private void FeedPanda()
    {
        Collider[] colliders =
            Physics.OverlapSphere(
                draggedBamboo.transform.position,
                pandaDetectionRadius,
                pandaLayer
            );


        foreach (Collider collider in colliders)
        {
            PandaFeedingZone feedingZone =
                collider.GetComponent<PandaFeedingZone>();


            if (feedingZone != null)
            {
                feedingZone.FeedPanda();

                Debug.Log("Panda is eating bamboo!");

                return;
            }


            // In case the collider is on the child
            // but PandaFeedingZone is on its parent.
            feedingZone =
                collider.GetComponentInParent<PandaFeedingZone>();


            if (feedingZone != null)
            {
                feedingZone.FeedPanda();

                Debug.Log("Panda is eating bamboo!");

                return;
            }
        }
    }
}