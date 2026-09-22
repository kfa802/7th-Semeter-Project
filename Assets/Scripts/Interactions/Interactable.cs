using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera interactionCamera;

    [Header("Choice UI")]
    [SerializeField] private GameObject choicePanel;

    [Header("Camera Priority")]
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int interactionPriority = 20;

    private Transform player;
    private Camera mainCamera;

    private void Start()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;

        mainCamera = Camera.main;

        // Make sure the normal player camera starts active
        if (playerCamera != null)
            playerCamera.Priority = normalPriority;

        // Interaction camera starts inactive
        if (interactionCamera != null)
            interactionCamera.Priority = 0;

        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    private void Update()
    {
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("MOUSE CLICK DETECTED");

            TryInteract(Mouse.current.position.ReadValue());
        }
    }

    private void CheckForInteraction()
    {
        // PC
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryInteract(Mouse.current.position.ReadValue());
        }

        // iPad
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 touchPosition =
                Touchscreen.current.primaryTouch.position.ReadValue();

            TryInteract(touchPosition);
        }
    }

    private void TryInteract(Vector2 screenPosition)
    {
        if (mainCamera == null)
            return;

        Ray ray =
            mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Interactable interactable =
                hit.collider.GetComponentInParent<Interactable>();

            if (interactable == this)
            {
                OpenInteraction();
            }
        }
    }

    private void OpenInteraction()
    {
        // Switch to interaction camera
        if (interactionCamera != null)
        {
            interactionCamera.Priority = interactionPriority;
        }

        // Show choices
        if (choicePanel != null)
        {
            choicePanel.SetActive(true);
        }
    }

    public void ChooseStayHome()
    {
        Debug.Log("Choice: Stay Home");
    }

    public void ChooseGoToWork()
    {
        Debug.Log("Choice: Go to Work");
    }

    public void CloseInteraction()
    {
        // Return to player camera
        if (interactionCamera != null)
        {
            interactionCamera.Priority = 0;
        }

        if (playerCamera != null)
        {
            playerCamera.Priority = normalPriority;
        }

        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }
    }
}