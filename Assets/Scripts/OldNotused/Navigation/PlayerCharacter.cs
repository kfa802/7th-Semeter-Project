using UnityEngine;
using UnityEngine.AI;

public class CharacterMovement : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;

    private NavMeshAgent playerAgent;
    private float verticalVelocity;

    private void Start()
    {
        playerAgent = player.GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        // Follow player's horizontal position
        Vector3 targetPosition = player.position;

        transform.position = new Vector3(
            targetPosition.x,
            transform.position.y,
            targetPosition.z
        );

        // Gravity
        if (Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            out RaycastHit hit,
            2f))
        {
            float distanceToGround = hit.distance - 0.1f;

            if (distanceToGround > 0.01f)
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
                transform.position += Vector3.up * verticalVelocity * Time.deltaTime;
            }
            else
            {
                verticalVelocity = 0f;
                transform.position = new Vector3(
                    transform.position.x,
                    hit.point.y,
                    transform.position.z
                );
            }
        }

        // Face movement direction
        if (playerAgent != null && playerAgent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = playerAgent.velocity;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // Walking animation
        bool isWalking =
            playerAgent != null &&
            playerAgent.velocity.sqrMagnitude > 0.01f;

        animator.SetBool("IsWalking", isWalking);
    }
}