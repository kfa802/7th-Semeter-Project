using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

public class FirstPersonLook : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Walking Bounce")]
    [SerializeField] private float bounceAmount = 0.02f;
    [SerializeField] private float bounceSpeed = 7f;

    private float pitch = 0f;
    private float yaw = 0f;

    private NavMeshAgent playerAgent;
    private float bounceTimer = 0f;
    private Vector3 cameraStartPosition;

    private void Start()
    {
        playerAgent = GetComponent<NavMeshAgent>();

        if (cameraTransform != null)
            cameraStartPosition = cameraTransform.localPosition;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        // LOOK
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * sensitivity;
        pitch -= mouseDelta.y * sensitivity;

        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);


        // WALKING BOUNCE
        bool isWalking =
            playerAgent != null &&
            playerAgent.hasPath &&
            playerAgent.remainingDistance > playerAgent.stoppingDistance &&
            playerAgent.velocity.sqrMagnitude > 0.01f;

        if (isWalking)
        {
            bounceTimer += Time.deltaTime * bounceSpeed;

            float bounceY = Mathf.Sin(bounceTimer) * bounceAmount;

            cameraTransform.localPosition =
                cameraStartPosition + new Vector3(0f, bounceY, 0f);
        }
        else
        {
            bounceTimer = 0f;

            if (cameraTransform != null)
                cameraTransform.localPosition = cameraStartPosition;
        }
    }
}