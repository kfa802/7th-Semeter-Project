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

        [Tooltip("What the panda looks at when it sits down here. Empty = the default Sit Facing Target, or the main camera.")]
        public Transform sitFacingTarget;
    }

    [Header("NavMesh Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform destination;

    [Header("Walking Height")]
    [Tooltip("How much to raise (+) or lower (-) the panda while walking, added to the agent's Base Offset. Use a negative value if the panda floats.")]
    [SerializeField] private float walkingBaseOffset = -0.1f;

    [Tooltip("How fast (units per second) the panda blends to and from the walking height, so there is no sudden pop.")]
    [SerializeField] private float baseOffsetBlendSpeed = 1f;

    private float originalBaseOffset;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameter = "IsWalking";
    [SerializeField] private string standUpTrigger = "StandUp";
    [SerializeField] private string sitDownTrigger = "SitDown";
    [SerializeField] private string eatTrigger = "Eat";

    [Header("Stand Up")]
    [Tooltip("Name of the stand-up state in the Animator (or give that state the tag 'StandUp').")]
    [SerializeField] private string standUpStateName = "Stand Up";

    [Tooltip("How far through the stand-up clip (0-1) before walking begins.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float standUpFinishedAt = 0.95f;

    [Tooltip("Safety limit: start walking anyway if the stand-up hasn't finished after this many seconds.")]
    [SerializeField] private float maxStandUpWait = 4f;

    private bool standUpStarted;
    private float movementStartTime;
    private bool cinematicStarted;

    private bool enteringCamera;
    private float cameraEnterTimer;
    private Vector3 cameraEnterStartPosition;
    private Quaternion cameraEnterStartRotation;
    private float cinematicFieldOfView;

    [Header("Feeding")]
    [Tooltip("Travel waits until the eating animation has finished.")]
    [SerializeField] private PandaFeedingZone feedingZone;

    [Tooltip("Safety limit: start travelling anyway after waiting this many seconds.")]
    [SerializeField] private float maxWaitForEating = 10f;

    private string pendingZoneName;
    private float pendingTravelStartTime;

    public bool HasPendingTravel => pendingZoneName != null;

    [Header("Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera cinematicCamera;

    [Header("Camera Movement")]
    [SerializeField] private float cameraMoveSpeed = 3f;
    [SerializeField] private float cameraRotationSpeed = 4f;
    [SerializeField] private float cameraLookHeight = 1.5f;

    [Header("Camera Enter")]
    [Tooltip("Seconds to glide from the main camera view to the first camera point.")]
    [SerializeField] private float cameraEnterDuration = 1.5f;

    [Header("Camera Return")]
    [SerializeField] private float cameraReturnDuration = 1.5f;

    [Header("Zone Destinations")]
    [SerializeField] private List<ZoneDestination> zoneDestinations =
        new List<ZoneDestination>();

    [Header("Facing Direction")]
    [Tooltip("The panda's visual model. If empty, the panda object itself is rotated.")]
    [SerializeField] private Transform pandaModel;

    [Tooltip("Extra Y rotation if the model doesn't face forward. Try 0, 180, 90 or -90.")]
    [SerializeField] private float facingCorrection = 180f;

    [Tooltip("How quickly the panda turns to follow its path while walking.")]
    [SerializeField] private float turnSpeed = 6f;

    [Header("Smooth Turning")]
    [Tooltip("Seconds the panda spends turning from its sitting angle toward the path before it starts walking forward.")]
    [SerializeField] private float startTurnDuration = 0.7f;

    [Tooltip("Seconds the panda spends turning to face the camera before it sits down.")]
    [SerializeField] private float arrivalTurnDuration = 0.9f;

    [Tooltip("What the panda looks at when it sits. Empty = the main camera.")]
    [SerializeField] private Transform sitFacingTarget;

    [Tooltip("Extra angle when sitting, so the panda sits slightly turned (e.g. 20 or -20). 0 = looks straight at the target.")]
    [SerializeField] private float sitAngleOffset = 0f;

    private enum TurnPhase { None, TowardPath, TowardCamera }
    private TurnPhase turnPhase = TurnPhase.None;
    private float turnTimer;
    private Quaternion turnStartRotation;
    private Transform currentSitFacingTarget;

    private Transform[] cameraPoints;
    private int currentCameraPoint;

    private bool isMoving;
    private bool returningCamera;

    private float cameraReturnTimer;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;

    public bool IsMoving => isMoving;
    public bool IsTravelling => isMoving || returningCamera;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            // This script handles the rotation itself (see CorrectPandaFacingDirection).
            agent.updateRotation = false;

            // Remember the normal height so we can return to it when not walking.
            originalBaseOffset = agent.baseOffset;
        }

        // Root motion makes the animation move the panda on its own,
        // which fights the NavMeshAgent and causes sideways drifting.
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            Debug.Log("PandaPathMover: Disabled Apply Root Motion on the Animator.");
        }

        Debug.Log(
            $"PandaPathMover: Initialized. " +
            $"Agent assigned = {agent != null}, " +
            $"On NavMesh = {(agent != null && agent.isOnNavMesh)}, " +
            $"Zone entries = {zoneDestinations.Count}."
        );

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        if (cinematicCamera != null)
        {
            // Remember the cinematic camera's own zoom so we can blend to it.
            cinematicFieldOfView = cinematicCamera.fieldOfView;
            cinematicCamera.gameObject.SetActive(false);
        }

        if (mainCamera == null)
            Debug.LogWarning("PandaPathMover: Main Camera is not assigned.");

        if (cinematicCamera == null)
            Debug.LogWarning(
                "PandaPathMover: Cinematic Camera is not assigned."
            );
    }

    private void Update()
    {
        // Start a queued journey once the panda has finished eating.
        if (pendingZoneName != null)
        {
            bool stillEating =
                feedingZone != null && feedingZone.IsEatingAnimationPlaying;

            bool waitedTooLong =
                Time.time - pendingTravelStartTime >= maxWaitForEating;

            if (!stillEating || waitedTooLong)
            {
                if (stillEating)
                {
                    Debug.LogWarning(
                        "PandaPathMover: Waited too long for eating to " +
                        "finish. Starting travel anyway. Check that the " +
                        "Eat state has an exit transition in the Animator."
                    );
                }

                string zoneToTravel = pendingZoneName;
                pendingZoneName = null;

                Debug.Log(
                    $"PandaPathMover: Eating finished. " +
                    $"Starting queued travel to '{zoneToTravel}'."
                );

                BeginTravel(zoneToTravel);
            }
        }

        if (isMoving)
        {
            // Turn IsWalking on once the stand-up animation is done.
            UpdateWalkingState();

            if (IsWalkingActive())
            {
                if (!cinematicStarted)
                    StartCinematicCamera();

                if (turnPhase == TurnPhase.TowardPath)
                {
                    // Turn gently from the sitting angle toward the path,
                    // stepping in place, before walking forward.
                    HoldAgent();

                    if (UpdateTurn(GetPathFacingRotation(), startTurnDuration))
                    {
                        turnPhase = TurnPhase.None;
                        agent.isStopped = false;
                        Debug.Log("PandaPathMover: Facing the path. Panda starts moving.");
                    }
                }
                else if (turnPhase == TurnPhase.TowardCamera)
                {
                    // Arrived: turn to face the camera, then sit down.
                    HoldAgent();

                    if (UpdateTurn(GetSitFacingRotation(), arrivalTurnDuration))
                    {
                        turnPhase = TurnPhase.None;
                        FinishMovement();
                    }
                }
                else
                {
                    // Walking along the path.
                    if (agent.isStopped)
                        agent.isStopped = false;

                    CorrectPandaFacingDirection();
                    CheckPandaArrival();
                }

                // First glide smoothly from the main view, then follow the camera points.
                if (enteringCamera)
                    EnterCinematicCamera();
                else
                    MoveCinematicCamera();
            }
            else
            {
                // Eating or standing up: hold the panda and camera still.
                HoldAgent();
            }
        }

        if (returningCamera)
            ReturnCameraToMain();

        UpdateBaseOffset();
    }

    // Lowers (or raises) the panda while walking so its feet touch the
    // ground, and blends back to the normal height when it stops.
    private void UpdateBaseOffset()
    {
        if (agent == null)
            return;

        bool walking = isMoving && IsWalkingActive();

        float targetOffset = walking
            ? originalBaseOffset + walkingBaseOffset
            : originalBaseOffset;

        agent.baseOffset = Mathf.MoveTowards(
            agent.baseOffset,
            targetOffset,
            Mathf.Max(baseOffsetBlendSpeed, 0.01f) * Time.deltaTime
        );
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
        cinematicStarted = false;
        standUpStarted = false;
        movementStartTime = Time.time;

        // When walking begins, first turn smoothly toward the path.
        BeginTurn(startTurnDuration > 0f ? TurnPhase.TowardPath : TurnPhase.None);

        // Calculate the path now, but don't move until IsWalking is true.
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

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

        // Begin the stand-up animation.
        if (animator != null)
        {
            // Clear a pending Eat trigger from a recent feed.
            if (!string.IsNullOrEmpty(eatTrigger))
                animator.ResetTrigger(eatTrigger);

            animator.SetBool(walkingParameter, false);
            animator.ResetTrigger(standUpTrigger);
            animator.ResetTrigger(sitDownTrigger);
            animator.SetTrigger(standUpTrigger);

            Debug.Log("PandaPathMover: Stand-up trigger activated.");
        }
        else
        {
            Debug.LogError(
                "PandaPathMover: Animator is not assigned."
            );
        }

        // The cinematic camera starts later, when IsWalking becomes true.
        Debug.Log(
            "PandaPathMover: Journey prepared. Waiting for the stand-up " +
            "animation to finish before walking."
        );
    }

    private void StartCinematicCamera()
    {
        cinematicStarted = true;

        // Start the cinematic camera exactly where the main camera is,
        // so the switch itself is invisible.
        cinematicCamera.transform.position = mainCamera.transform.position;
        cinematicCamera.transform.rotation = mainCamera.transform.rotation;
        cinematicCamera.fieldOfView = mainCamera.fieldOfView;

        cameraEnterStartPosition = mainCamera.transform.position;
        cameraEnterStartRotation = mainCamera.transform.rotation;
        cameraEnterTimer = 0f;
        enteringCamera = true;

        cinematicCamera.gameObject.SetActive(true);
        mainCamera.gameObject.SetActive(false);

        Debug.Log("PandaPathMover: Cinematic camera gliding in from main view.");
    }

    // Glides from the main camera view to the first camera point,
    // turning to look at the panda along the way.
    private void EnterCinematicCamera()
    {
        Transform firstPoint = cameraPoints[0];

        cameraEnterTimer +=
            Time.deltaTime / Mathf.Max(cameraEnterDuration, 0.01f);

        float t = Mathf.Clamp01(cameraEnterTimer);
        float smoothT = t * t * (3f - 2f * t);

        cinematicCamera.transform.position = Vector3.Lerp(
            cameraEnterStartPosition,
            firstPoint.position,
            smoothT
        );

        // Aim at the panda, same as MoveCinematicCamera does.
        Vector3 lookTarget = transform.position + Vector3.up * cameraLookHeight;
        Vector3 direction = lookTarget - cinematicCamera.transform.position;

        Quaternion endRotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : firstPoint.rotation;

        cinematicCamera.transform.rotation = Quaternion.Slerp(
            cameraEnterStartRotation,
            endRotation,
            smoothT
        );

        cinematicCamera.fieldOfView = Mathf.Lerp(
            mainCamera.fieldOfView,
            cinematicFieldOfView,
            smoothT
        );

        if (t >= 1f)
        {
            enteringCamera = false;
            Debug.Log("PandaPathMover: Cinematic camera in position.");
        }
    }

    private bool IsWalkingActive()
    {
        if (animator == null || string.IsNullOrEmpty(walkingParameter))
            return true;

        return animator.GetBool(walkingParameter);
    }

    private bool IsStandUpState(AnimatorStateInfo state)
    {
        return state.IsTag("StandUp") ||
               (!string.IsNullOrEmpty(standUpStateName) &&
                state.IsName(standUpStateName)) ||
               state.IsName("Stand Up") ||
               state.IsName("StandUp") ||
               state.IsName("Stand_Up");
    }

    // Sets IsWalking to true once the stand-up animation has finished
    // (and the panda is not eating). An animation event calling
    // StartWalkingAnimation() does the same thing, if you have one.
    private void UpdateWalkingState()
    {
        if (animator == null || animator.GetBool(walkingParameter))
            return;

        bool timedOut = Time.time - movementStartTime >= maxStandUpWait;

        // Blending into the stand-up animation: keep waiting.
        if (animator.IsInTransition(0) &&
            IsStandUpState(animator.GetNextAnimatorStateInfo(0)))
        {
            standUpStarted = true;

            if (!timedOut)
                return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (IsStandUpState(current))
        {
            standUpStarted = true;

            // Still standing up.
            if (current.normalizedTime < standUpFinishedAt && !timedOut)
                return;
        }
        else if (!standUpStarted && !timedOut)
        {
            // The stand-up animation hasn't begun yet.
            return;
        }

        // Never walk while the eat animation is still playing.
        if (feedingZone != null &&
            feedingZone.IsEatingAnimationPlaying &&
            !timedOut)
            return;

        if (timedOut)
        {
            Debug.LogWarning(
                "PandaPathMover: Stand-up didn't finish in time. Walking " +
                "anyway. Check 'Stand Up State Name' matches your Animator."
            );
        }

        StartWalkingAnimation();
    }

    public void StartWalkingAnimation()
    {
        if (!isMoving || animator == null)
            return;

        animator.ResetTrigger(standUpTrigger);
        animator.ResetTrigger(sitDownTrigger);
        animator.SetBool(walkingParameter, true);
        standUpStarted = true;

        Debug.Log(
            $"PandaPathMover: StartWalkingAnimation called. " +
            $"IsWalking = {animator.GetBool(walkingParameter)}"
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
                "PandaPathMover: Panda reached its destination. " +
                "Turning to face the camera before sitting."
            );

            HoldAgent();

            if (arrivalTurnDuration > 0f)
                BeginTurn(TurnPhase.TowardCamera);
            else
            {
                GetModel().rotation = GetSitFacingRotation();
                FinishMovement();
            }
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

        // Stop walking and begin sitting down.
        if (animator != null)
        {
            animator.SetBool(walkingParameter, false);
            animator.ResetTrigger(standUpTrigger);
            animator.SetTrigger(sitDownTrigger);

            Debug.Log("PandaPathMover: Sit-down trigger activated.");
        }

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
        enteringCamera = false;
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

        cinematicCamera.fieldOfView = Mathf.Lerp(
            cinematicFieldOfView,
            mainCamera.fieldOfView,
            smoothT
        );

        if (t >= 1f)
        {
            returningCamera = false;

            // Restore the cinematic camera's own zoom for next time.
            cinematicCamera.fieldOfView = cinematicFieldOfView;

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

        // If the panda is eating, wait. Update() starts the journey
        // (walking and camera) as soon as the eating animation ends.
        if (feedingZone != null && feedingZone.IsEatingAnimationPlaying)
        {
            pendingZoneName = zoneName;
            pendingTravelStartTime = Time.time;

            Debug.Log(
                $"PandaPathMover: Panda is eating. Travel to " +
                $"'{zoneName}' will start when eating finishes."
            );
            return;
        }

        pendingZoneName = null;
        BeginTravel(zoneName);
    }

    private void BeginTravel(string zoneName)
    {
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
            currentSitFacingTarget = zone.sitFacingTarget;

            Debug.Log(
                $"PandaPathMover: Destination position = " +
                $"{zone.destination.position}; " +
                $"camera points = {zone.cameraPoints.Length}."
            );

            StartMovement();

            // Begin the temperature transition only if movement started.
            if (isMoving && EnvironmentSystem.Instance != null)
            {
                EnvironmentSystem.Instance.BeginTravelToZone(zone.zoneName);
            }

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

    // ---------- Smooth turning helpers ----------

    private Transform GetModel()
    {
        // Rotate the model if assigned, otherwise the panda object itself.
        return pandaModel != null ? pandaModel : transform;
    }

    private void HoldAgent()
    {
        if (agent != null && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void BeginTurn(TurnPhase phase)
    {
        turnPhase = phase;
        turnTimer = 0f;
        turnStartRotation = GetModel().rotation;
    }

    // Eases the model from where the turn started to the target.
    // Returns true when the turn is complete.
    private bool UpdateTurn(Quaternion targetRotation, float duration)
    {
        turnTimer += Time.deltaTime / Mathf.Max(duration, 0.01f);

        float t = Mathf.Clamp01(turnTimer);
        float smoothT = t * t * (3f - 2f * t);

        GetModel().rotation = Quaternion.Slerp(
            turnStartRotation,
            targetRotation,
            smoothT
        );

        return t >= 1f;
    }

    private Quaternion FacingRotation(Vector3 direction, float extraAngle)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return GetModel().rotation;

        return Quaternion.LookRotation(direction) *
               Quaternion.Euler(0f, facingCorrection + extraAngle, 0f);
    }

    private Quaternion GetPathFacingRotation()
    {
        Vector3 target;

        if (agent != null && !agent.pathPending && agent.hasPath)
            target = agent.steeringTarget;
        else if (destination != null)
            target = destination.position;
        else
            return GetModel().rotation;

        return FacingRotation(target - transform.position, 0f);
    }

    private Quaternion GetSitFacingRotation()
    {
        Transform target = currentSitFacingTarget != null
            ? currentSitFacingTarget
            : sitFacingTarget != null
                ? sitFacingTarget
                : mainCamera != null ? mainCamera.transform : null;

        if (target == null)
            return GetModel().rotation;

        return FacingRotation(target.position - transform.position, sitAngleOffset);
    }

    // Rotates the panda (or its model) to face the way it is walking.
    private void CorrectPandaFacingDirection()
    {
        if (agent == null)
            return;

        Transform model = GetModel();

        // Use real movement; fall back to the path direction when moving slowly.
        Vector3 movementDirection = agent.velocity;

        if (movementDirection.sqrMagnitude < 0.0025f && agent.hasPath)
            movementDirection = agent.steeringTarget - transform.position;

        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(movementDirection) *
            Quaternion.Euler(0f, facingCorrection, 0f);

        model.rotation = Quaternion.Slerp(
            model.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }
}