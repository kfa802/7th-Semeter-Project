
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PandaPathMover : MonoBehaviour
{
    [Serializable]
    public class ZoneDestination
    {
        public string zoneName;

        [Tooltip("The panda's destination in this zone.")]
        public Transform destination;

        [Tooltip("Camera positions used when travelling to this zone.")]
        public Transform[] cameraPoints;
    }

    [Header("NavMesh Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform destination;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameter = "IsWalking";

    [Header("Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera cinematicCamera;

    [Header("Camera Movement")]
    [SerializeField] private float cameraMoveSpeed = 3f;
    [SerializeField] private float cameraRotationSpeed = 4f;
    [SerializeField] private float cameraLookHeight = 1.5f;

    [Header("Camera Return")]
    [SerializeField] private float cameraReturnDuration = 1.5f;

    [Header("Zone Destinations")]
    [SerializeField] private List<ZoneDestination> zoneDestinations =
        new List<ZoneDestination>();

    private Transform[] cameraPoints;
    private int currentCameraPoint;

    private bool isMoving;
    private bool returningCamera;

    private float cameraReturnTimer;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;

    public bool IsMoving => isMoving;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        Debug.Log(
            $"PandaPathMover: Initialized. " +
            $"Agent assigned = {agent != null}, " +
            $"On NavMesh = {(agent != null && agent.isOnNavMesh)}, " +
            $"Zone entries = {zoneDestinations.Count}."
        );

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(false);

        if (mainCamera == null)
            Debug.LogWarning("PandaPathMover: Main Camera is not assigned.");

        if (cinematicCamera == null)
            Debug.LogWarning(
                "PandaPathMover: Cinematic Camera is not assigned."
            );
    }

    private void Update()
    {
        if (isMoving)
        {
            CheckPandaArrival();
            MoveCinematicCamera();
        }

        if (returningCamera)
            ReturnCameraToMain();
    }

    public void StartMovement()
    {
        Debug.Log(
            $"PandaPathMover: StartMovement entered. " +
            $"isMoving={isMoving}, returningCamera={returningCamera}"
        );

        if (isMoving || returningCamera)
        {
            Debug.LogWarning(
                "PandaPathMover: Movement refused because another " +
                "journey or camera return is in progress."
            );
            return;
        }

        if (agent == null)
        {
            Debug.LogError(
                "PandaPathMover: No NavMeshAgent assigned or found."
            );
            return;
        }

        if (destination == null)
        {
            Debug.LogError(
                "PandaPathMover: Destination is missing."
            );
            return;
        }

        if (mainCamera == null || cinematicCamera == null)
        {
            Debug.LogError(
                "PandaPathMover: Main Camera or Cinematic Camera is missing."
            );
            return;
        }

        if (cameraPoints == null || cameraPoints.Length == 0)
        {
            Debug.LogError(
                "PandaPathMover: No camera points assigned for this zone."
            );
            return;
        }

        if (cameraPoints[0] == null)
        {
            Debug.LogError(
                "PandaPathMover: The first camera point is missing."
            );
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "PandaPathMover: Panda is not on a NavMesh."
            );
            return;
        }

        Debug.Log(
            $"PandaPathMover: About to move. " +
            $"Destination={destination.position}, " +
            $"Agent position={agent.transform.position}, " +
            $"Speed={agent.speed}, " +
            $"Stopping distance={agent.stoppingDistance}"
        );

        currentCameraPoint = 0;
        isMoving = true;
        returningCamera = false;

        agent.isStopped = false;

        bool destinationAccepted =
            agent.SetDestination(destination.position);

        Debug.Log(
            $"PandaPathMover: SetDestination returned " +
            $"{destinationAccepted}. Path status={agent.pathStatus}"
        );

        if (!destinationAccepted)
        {
            isMoving = false;

            Debug.LogError(
                "PandaPathMover: NavMeshAgent rejected the destination."
            );
            return;
        }

        SetWalkingAnimation(true);

        mainCamera.gameObject.SetActive(false);
        cinematicCamera.gameObject.SetActive(true);

        cinematicCamera.transform.position =
            cameraPoints[0].position;

        cinematicCamera.transform.rotation =
            cameraPoints[0].rotation;

        Debug.Log(
            "PandaPathMover: Movement started successfully."
        );
    }

    private void CheckPandaArrival()
    {
        if (agent == null)
            return;

        if (agent.pathPending)
            return;

        if (!agent.hasPath)
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogError(
                    "PandaPathMover: The agent has an invalid path."
                );
            }

            return;
        }

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError(
                "PandaPathMover: Path is invalid. Check the NavMesh " +
                "and destination position."
            );
            return;
        }

        if (agent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            Debug.LogWarning(
                "PandaPathMover: Only a partial path to the destination " +
                "is available."
            );
        }

        if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            Debug.Log(
                "PandaPathMover: Panda reached its destination."
            );

            FinishMovement();
        }
    }

    private void MoveCinematicCamera()
    {
        if (cinematicCamera == null ||
            cameraPoints == null ||
            currentCameraPoint >= cameraPoints.Length)
            return;

        Transform targetPoint = cameraPoints[currentCameraPoint];

        if (targetPoint == null)
        {
            Debug.LogWarning(
                $"PandaPathMover: Camera point {currentCameraPoint} " +
                "is missing."
            );

            currentCameraPoint++;
            return;
        }

        cinematicCamera.transform.position = Vector3.MoveTowards(
            cinematicCamera.transform.position,
            targetPoint.position,
            cameraMoveSpeed * Time.deltaTime
        );

        Vector3 lookTarget =
            transform.position + Vector3.up * cameraLookHeight;

        Vector3 direction =
            lookTarget - cinematicCamera.transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            cinematicCamera.transform.rotation = Quaternion.Slerp(
                cinematicCamera.transform.rotation,
                targetRotation,
                cameraRotationSpeed * Time.deltaTime
            );
        }

        float distance = Vector3.Distance(
            cinematicCamera.transform.position,
            targetPoint.position
        );

        if (distance <= 0.05f &&
            currentCameraPoint < cameraPoints.Length - 1)
        {
            currentCameraPoint++;

            Debug.Log(
                "PandaPathMover: Moving camera to point " +
                currentCameraPoint + "."
            );
        }
    }

    private void FinishMovement()
    {
        isMoving = false;

        if (agent != null)
            agent.isStopped = true;

        SetWalkingAnimation(false);

        Debug.Log(
            "PandaPathMover: Travel finished. Returning camera to main view."
        );

        StartCameraReturn();
    }

    private void StartCameraReturn()
    {
        if (mainCamera == null || cinematicCamera == null)
            return;

        returningCamera = true;
        cameraReturnTimer = 0f;

        cameraStartPosition = cinematicCamera.transform.position;
        cameraStartRotation = cinematicCamera.transform.rotation;
    }

    private void ReturnCameraToMain()
    {
        if (mainCamera == null || cinematicCamera == null)
            return;

        cameraReturnTimer +=
            Time.deltaTime / Mathf.Max(cameraReturnDuration, 0.01f);

        float t = Mathf.Clamp01(cameraReturnTimer);
        float smoothT = t * t * (3f - 2f * t);

        cinematicCamera.transform.position = Vector3.Lerp(
            cameraStartPosition,
            mainCamera.transform.position,
            smoothT
        );

        cinematicCamera.transform.rotation = Quaternion.Slerp(
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

            Debug.Log(
                "PandaPathMover: Main camera restored."
            );
        }
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (animator == null || string.IsNullOrEmpty(walkingParameter))
            return;

        animator.SetBool(walkingParameter, walking);
    }

    public void TravelToZone(string zoneName)
    {
        Debug.Log(
            $"PandaPathMover: TravelToZone RECEIVED. " +
            $"Zone name = '{zoneName}'"
        );

        if (string.IsNullOrWhiteSpace(zoneName))
        {
            Debug.LogError(
                "PandaPathMover: Received an EMPTY zone name."
            );
            return;
        }

        if (isMoving || returningCamera)
        {
            Debug.LogWarning(
                $"PandaPathMover: Cannot start travel to '{zoneName}'. " +
                $"isMoving={isMoving}, returningCamera={returningCamera}"
            );
            return;
        }

        Debug.Log(
            $"PandaPathMover: Checking {zoneDestinations.Count} " +
            "configured zone destinations."
        );

        foreach (ZoneDestination zone in zoneDestinations)
        {
            if (zone == null)
            {
                Debug.LogWarning(
                    "PandaPathMover: Found an empty destination entry."
                );
                continue;
            }

            Debug.Log(
                $"PandaPathMover: Checking entry '{zone.zoneName}'."
            );

            if (!string.Equals(
                zone.zoneName?.Trim(),
                zoneName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Debug.Log(
                $"PandaPathMover: MATCH FOUND for '{zone.zoneName}'."
            );

            if (zone.destination == null)
            {
                Debug.LogError(
                    $"PandaPathMover: Destination Transform is missing " +
                    $"for '{zone.zoneName}'."
                );
                return;
            }

            if (zone.cameraPoints == null ||
                zone.cameraPoints.Length == 0)
            {
                Debug.LogError(
                    $"PandaPathMover: No camera points assigned " +
                    $"for '{zone.zoneName}'."
                );
                return;
            }

            destination = zone.destination;
            cameraPoints = zone.cameraPoints;

            Debug.Log(
                $"PandaPathMover: Destination position = " +
                $"{zone.destination.position}; " +
                $"camera points = {zone.cameraPoints.Length}."
            );

            StartMovement();

            Debug.Log(
                $"PandaPathMover: StartMovement called for '{zone.zoneName}'."
            );
            return;
        }

        Debug.LogError(
            $"PandaPathMover: NO MATCH FOUND for zone '{zoneName}'. " +
            "Check the Zone Destinations list in the Inspector."
        );
    }
}