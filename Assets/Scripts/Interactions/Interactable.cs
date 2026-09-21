using UnityEngine;

public class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Interaction Camera")]
    [SerializeField] private Camera interactionCamera;

    [Header("Choice UI")]
    [SerializeField] private GameObject choicePanel;

    private Transform player;
    private Camera playerCamera;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerCamera = Camera.main;

        if (interactionCamera != null)
            interactionCamera.gameObject.SetActive(false);

        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(player.position, transform.position);

        if (distance <= interactionDistance)
        {
            CheckForInteraction();
        }
    }

    private void CheckForInteraction()
    {
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryInteract(Mouse.current.position.ReadValue());
        }

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
        Ray ray = playerCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.GetComponentInParent<Interactable>() == this)
            {
                OpenInteraction();
            }
        }
    }

    private void OpenInteraction()
    {
        if (interactionCamera != null)
            interactionCamera.gameObject.SetActive(true);

        if (choicePanel != null)
            choicePanel.SetActive(true);
    }

    public void ChooseStayHome()
    {
        Debug.Log("Choice: Stay Home");

        // Branching narrative can be added here later.
    }

    public void ChooseGoToWork()
    {
        Debug.Log("Choice: Go to Work");

        // Branching narrative can be added here later.
    }
}