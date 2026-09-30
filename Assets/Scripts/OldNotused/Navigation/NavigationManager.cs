using UnityEngine;
using UnityEngine.InputSystem;

public class NavigationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerNavigation player;

    [Header("Navigation Points")]
    [SerializeField] private LayerMask navigationPointLayer;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            Debug.LogError("NavigationManager: No Main Camera found!");

        if (player == null)
            Debug.LogError("NavigationManager: Player is not assigned!");
    }

    private void Update()
    {
        HandleMouseClick();
    }

    private void HandleMouseClick()
    {
        if (Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (playerCamera == null || player == null)
            return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        Ray ray = playerCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, navigationPointLayer))
        {
            NavigationPoint point =
                hit.collider.GetComponentInParent<NavigationPoint>();

            if (point == null)
                return;

            if (!player.IsNavigationPointRegistered(point))
            {
                Debug.Log("Navigation point is not registered: " + point.name);
                return;
            }

            Debug.Log("Clicked navigation point: " + point.name);

            player.MoveToPoint(point);
        }
    }
}