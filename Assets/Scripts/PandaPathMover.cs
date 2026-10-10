using UnityEngine;
using UnityEngine.AI;

public class PandaPathMover : MonoBehaviour
{
    [Header("NavMesh Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform destination;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameter = "IsWalking";

    [Header("Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera cinematicCamera;

    [Header("Camera Path")]
    [SerializeField] private Transform[] cameraPoints;

    [Header("Camera Movement")]
    [SerializeField] private float cameraMoveSpeed = 3f;
    [SerializeField] private float cameraRotationSpeed = 4f;
    [SerializeField] private float cameraLookHeight = 1.5f;

    [Header("Camera Return")]
    [SerializeField] private float cameraReturnDuration = 1.5f;

    private int currentCameraPoint = 0;

    private bool isMoving = false;
    private bool returningCamera = false;

    private float cameraReturnTimer;

    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;

    public bool IsMoving => isMoving;

    [Header("Zone Destinations")]
[SerializeField] private Transform mountainDestination;
[SerializeField] private Transform midwayDestination;


    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(false);
    }


    private void Update()
    {
        if (isMoving)
        {
            CheckPandaArrival();
            MoveCinematicCamera();
        }

        if (returningCamera)
        {
            ReturnCameraToMain();
        }
    }


    public void StartMovement()
    {
        if (isMoving || returningCamera)
            return;

        if (agent == null)
        {
            Debug.LogWarning(
                "PandaPathMover: No NavMeshAgent assigned."
            );
            return;
        }

        if (destination == null)
        {
            Debug.LogWarning(
                "PandaPathMover: No destination assigned."
            );
            return;
        }

        if (mainCamera == null || cinematicCamera == null)
        {
            Debug.LogWarning(
                "PandaPathMover: Main Camera or Cinematic Camera is missing."
            );
            return;
        }

        if (cameraPoints == null || cameraPoints.Length == 0)
        {
            Debug.LogWarning(
                "PandaPathMover: No camera points assigned."
            );
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning(
                "PandaPathMover: Panda is not currently on a NavMesh."
            );
            return;
        }

        currentCameraPoint = 0;

        isMoving = true;
        returningCamera = false;

        agent.isStopped = false;
        agent.SetDestination(destination.position);

        SetWalkingAnimation(true);

        mainCamera.gameObject.SetActive(false);
        cinematicCamera.gameObject.SetActive(true);

        cinematicCamera.transform.position =
            cameraPoints[0].position;

        cinematicCamera.transform.rotation =
            cameraPoints[0].rotation;
    }


    private void CheckPandaArrival()
    {
        if (agent == null)
            return;

        if (agent.pathPending)
            return;

        if (!agent.hasPath)
            return;

        if (agent.remainingDistance <=
            agent.stoppingDistance + 0.05f)
        {
            FinishMovement();
        }
    }


    private void MoveCinematicCamera()
    {
        if (cinematicCamera == null)
            return;

        if (cameraPoints == null ||
            cameraPoints.Length == 0)
            return;

        if (currentCameraPoint >= cameraPoints.Length)
            return;

        Transform targetPoint =
            cameraPoints[currentCameraPoint];

        if (targetPoint == null)
        {
            currentCameraPoint++;
            return;
        }

        cinematicCamera.transform.position =
            Vector3.MoveTowards(
                cinematicCamera.transform.position,
                targetPoint.position,
                cameraMoveSpeed * Time.deltaTime
            );

        Vector3 lookTarget =
            transform.position +
            Vector3.up * cameraLookHeight;

        Vector3 direction =
            lookTarget -
            cinematicCamera.transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            cinematicCamera.transform.rotation =
                Quaternion.Slerp(
                    cinematicCamera.transform.rotation,
                    targetRotation,
                    cameraRotationSpeed * Time.deltaTime
                );
        }

        float distance =
            Vector3.Distance(
                cinematicCamera.transform.position,
                targetPoint.position
            );

        if (distance <= 0.05f)
        {
            if (currentCameraPoint <
                cameraPoints.Length - 1)
            {
                currentCameraPoint++;
            }
        }
    }


    private void FinishMovement()
    {
        isMoving = false;

        agent.isStopped = true;

        SetWalkingAnimation(false);

        StartCameraReturn();
    }


    private void StartCameraReturn()
    {
        if (mainCamera == null ||
            cinematicCamera == null)
            return;

        returningCamera = true;

        cameraReturnTimer = 0f;

        cameraStartPosition =
            cinematicCamera.transform.position;

        cameraStartRotation =
            cinematicCamera.transform.rotation;
    }


    private void ReturnCameraToMain()
    {
        if (mainCamera == null ||
            cinematicCamera == null)
            return;

        cameraReturnTimer +=
            Time.deltaTime /
            Mathf.Max(
                cameraReturnDuration,
                0.01f
            );

        float t =
            Mathf.Clamp01(cameraReturnTimer);

        float smoothT =
            t * t * (3f - 2f * t);

        cinematicCamera.transform.position =
            Vector3.Lerp(
                cameraStartPosition,
                mainCamera.transform.position,
                smoothT
            );

        cinematicCamera.transform.rotation =
            Quaternion.Slerp(
                cameraStartRotation,
                mainCamera.transform.rotation,
                smoothT
            );

        if (t >= 1f)
        {
            returningCamera = false;

            cinematicCamera.transform.position =
                mainCamera.transform.position;

            cinematicCamera.transform.rotation =
                mainCamera.transform.rotation;

            cinematicCamera.gameObject.SetActive(false);

            mainCamera.gameObject.SetActive(true);
        }
    }


    private void SetWalkingAnimation(bool walking)
    {
        if (animator == null)
            return;

        if (string.IsNullOrEmpty(walkingParameter))
            return;

        animator.SetBool(
            walkingParameter,
            walking
        );
    }

    
public void TravelToZone(string zoneName)
{
    Transform target = null;

    if (zoneName == "Mountain")
        target = mountainDestination;
    else if (zoneName == "Midway")
        target = midwayDestination;

    if (target == null)
    {
        Debug.LogWarning(
            "PandaPathMover: No destination assigned for " + zoneName
        );
        return;
    }

    destination = target;
    StartMovement();
}
}