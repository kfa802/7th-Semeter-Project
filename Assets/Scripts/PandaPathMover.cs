using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PandaPathMover : MonoBehaviour
{
    [Serializable]
    public class CameraShot
    {
        [Tooltip("Where the camera goes for this shot.")]
        public Transform point;

        [Tooltip("Seconds to move from the previous shot (or the main camera, for the first shot) to this one.")]
        public float moveDuration = 2f;

        [Tooltip("How the move speeds up and slows down. Flat at the start = eases in, flat at the end = eases out, straight line = constant speed.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Seconds the camera stays at this point before moving on.")]
        public float holdDuration = 0f;

        [Tooltip("On: the camera keeps looking at the panda. Off: the camera uses this point's own rotation.")]
        public bool lookAtPanda = true;

        [Tooltip("Field of view at this shot. 0 = the cinematic camera's default.")]
        public float fieldOfView = 0f;
    }

    [Serializable]
    public class ZoneDestination
    {
        [Tooltip("The REAL zone name (must match the EcosystemZone), e.g. 'Midway'.")]
        public string zoneName;

        [Tooltip("Optional unique name for this route, e.g. 'Midway2'. " +
                 "Put this name in a GameEvent's Target Zone Name to use exactly this route. " +
                 "Leave empty for the normal route to this zone.")]
        public string routeName;

        [Tooltip("The panda's destination in this zone.")]
        public Transform destination;

        [Tooltip("Camera shots for the journey to this zone, in order.")]
        public List<CameraShot> cameraShots = new List<CameraShot>();

        [Tooltip("Old setup: only used if Camera Shots is empty.")]
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
    [Tooltip("Exact name of the stand-up state in the Animator (or give that state the tag 'StandUp').")]
    [SerializeField] private string standUpStateName = "StandUp";

    [Tooltip("How far through the stand-up clip (0-1) before walking begins.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float standUpFinishedAt = 0.95f;

    [Tooltip("Safety limit: start walking anyway if the stand-up hasn't finished after this many seconds.")]
    [SerializeField] private float maxStandUpWait = 4f;

    [Tooltip("Exact name of the walk state in the Animator. Used to force walking if the Animator gets stuck.")]
    [SerializeField] private string walkStateName = "PandaRig|Panda_Walk";

    [Tooltip("Seconds to wait for the StandUp trigger to work before forcing the stand-up animation.")]
    [SerializeField] private float forceStandUpAfter = 0.3f;

    private bool standUpStarted;
    private bool forcedStandUp;
    private float movementStartTime;
    private bool cinematicStarted;
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
    [Tooltip("On: the camera curves smoothly through the points. Off: straight lines between points.")]
    [SerializeField] private bool curvedCameraPath = true;

    [Tooltip("Height above the panda that the camera looks at.")]
    [SerializeField] private float cameraLookHeight = 1.5f;

    [Tooltip("How softly the camera follows the panda with its gaze. Higher = lazier, removes jitter.")]
    [SerializeField] private float lookTargetSmoothTime = 0.2f;

    [Tooltip("Move duration used for zones still using the old Camera Points list.")]
    [SerializeField] private float defaultShotDuration = 2f;

    [Header("Camera Return")]
    [Tooltip("Seconds to glide back to the main camera after the panda arrives.")]
    [SerializeField] private float cameraReturnDuration = 1.5f;

    [Tooltip("How the return speeds up and slows down.")]
    [SerializeField] private AnimationCurve cameraReturnEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Shot sequence state.
    private List<CameraShot> activeShots;
    private int currentShot;
    private float shotTimer;
    private bool holdingShot;
    private float holdTimer;
    private bool shotSequenceDone;

    private Vector3 shotStartPosition;
    private Quaternion shotStartRotation;
    private float shotStartFov;

    private Vector3 smoothedLookTarget;
    private Vector3 lookTargetVelocity;

    // Return state.
    private float cameraReturnTimer;
    private Vector3 returnStartPosition;
    private Quaternion returnStartRotation;
    private float returnStartFov;

    private Vector3 PandaLookPoint =>
        transform.position + Vector3.up * cameraLookHeight;

    [Header("Zone Destinations")]
    [SerializeField] private List<ZoneDestination> zoneDestinations =
        new List<ZoneDestination>();

    [Header("Facing Direction")]
    [Tooltip("The panda's visual model. If empty, the panda object itself is rotated.")]
    [SerializeField] private Transform pandaModel;

    [Tooltip("Extra Y rotation if the model doesn't face forward. Try 0, 180, 90 or -90.")]
    [SerializeField] private float facingCorrection = 180f;

    [Tooltip("How softly the panda turns while walking. Higher = gentler, wider turns.")]
    [SerializeField] private float walkTurnSmoothTime = 0.35f;

    [Tooltip("Fastest the panda may turn, in degrees per second.")]
    [SerializeField] private float maxTurnSpeed = 180f;

    [Header("Starting To Walk")]
    [Tooltip("The panda walks at full speed once it faces within this angle of the path.")]
    [SerializeField] private float fullSpeedAngle = 25f;

    [Tooltip("When facing more than this angle away from the path, the panda turns before moving forward. In between, it walks slower while it turns, so it curves onto the path.")]
    [SerializeField] private float noMoveAngle = 100f;

    [Header("Arriving")]
    [Tooltip("Over the last this-many units, the panda gradually turns toward its sitting direction, so it arrives already facing the camera. 0 = off.")]
    [SerializeField] private float arrivalBlendDistance = 1.5f;

    [Tooltip("How softly the panda finishes its last bit of turning while sitting down.")]
    [SerializeField] private float sitTurnSmoothTime = 0.3f;

    [Tooltip("On: the panda always sits square to the main camera's view, so it looks straight into the shot. Ignores every Sit Facing Target. Off: the panda faces the Sit Facing Target.")]
    [SerializeField] private bool sitSquareToCamera = true;

    [Tooltip("What the panda looks at when it sits. Empty = the main camera. Only used when Sit Square To Camera is off.")]
    [SerializeField] private Transform sitFacingTarget;

    [Tooltip("Extra angle when sitting, so the panda sits slightly turned (e.g. 20 or -20). 0 = looks straight at the target.")]
    [SerializeField] private float sitAngleOffset = 0f;

    private Transform currentSitFacingTarget;
    private bool turningToSit;
    private float yawVelocity;
    private float baseAgentSpeed;

    // Near the end, the path direction becomes unreliable (the steering
    // target is almost on top of the panda), so it is frozen here.
    private bool pathDirectionLocked;
    private Quaternion lockedPathRotation;

    private bool isMoving;
    private bool returningCamera;

    public bool IsMoving => isMoving;
    public bool IsTravelling => isMoving || returningCamera;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            // This script handles the rotation itself.
            agent.updateRotation = false;

            // Remember the normal height so we can return to it when not walking.
            originalBaseOffset = agent.baseOffset;

            // Remember the normal speed; it is lowered while the panda turns.
            baseAgentSpeed = agent.speed;
        }

        // Root motion makes the animation move the panda on its own,
        // which fights the NavMeshAgent and causes sideways drifting.
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            Debug.Log("PandaPathMover: Disabled Apply Root Motion on the Animator.");
        }

        // Keep animating even when a camera shot isn't looking at the panda.
        // Otherwise Unity freezes the pose and the panda slides.
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
            // Remember the cinematic camera's own zoom as the default.
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
            if (turningToSit)
            {
                // Arrived and sitting down: finish the small remaining turn.
                HoldAgent();

                Quaternion sitRotation = GetSitFacingRotation();

                if (TurnModelToward(sitRotation, sitTurnSmoothTime) < 1f)
                {
                    SetModelYaw(sitRotation.eulerAngles.y);
                    turningToSit = false;
                    FinishMovement();
                }
            }
            else
            {
                // Turn IsWalking on once the stand-up animation is done.
                UpdateWalkingState();

                if (IsWalkingActive())
                {
                    if (!cinematicStarted)
                        StartCinematicCamera();

                    WalkAlongPath();
                    CheckPandaArrival();
                }
                else
                {
                    // Eating or standing up: hold the panda still.
                    HoldAgent();
                }
            }
        }

        UpdateBaseOffset();
    }

    // The camera runs in LateUpdate, after the NavMeshAgent has moved
    // the panda this frame, so it never aims at last frame's position.
    private void LateUpdate()
    {
        if (isMoving && cinematicStarted)
            UpdateCinematicCamera();

        if (returningCamera)
            ReturnCameraToMain();
    }

    // Lowers (or raises) the panda while walking so its feet touch the
    // ground, and blends back to the normal height when it stops.
    private void UpdateBaseOffset()
    {
        if (agent == null)
            return;

        bool walking = isMoving && !turningToSit && IsWalkingActive();

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

        if (activeShots == null || activeShots.Count == 0)
        {
            Debug.LogError(
                "PandaPathMover: No camera shots assigned for this zone."
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
            $"Speed={baseAgentSpeed}, " +
            $"Stopping distance={agent.stoppingDistance}"
        );

        isMoving = true;
        returningCamera = false;
        cinematicStarted = false;
        standUpStarted = false;
        forcedStandUp = false;
        movementStartTime = Time.time;

        turningToSit = false;
        pathDirectionLocked = false;
        yawVelocity = 0f;

        // Calculate the path now, but don't move until IsWalking is true.
        agent.speed = baseAgentSpeed;
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

    // =========================================================
    // CINEMATIC CAMERA: SHOT SEQUENCE
    // =========================================================

    private void StartCinematicCamera()
    {
        cinematicStarted = true;

        Transform main = mainCamera.transform;

        // Start the cinematic camera exactly where the main camera is,
        // so the switch itself is invisible.
        cinematicCamera.transform.position = main.position;
        cinematicCamera.transform.rotation = main.rotation;
        cinematicCamera.fieldOfView = mainCamera.fieldOfView;

        shotStartPosition = main.position;
        shotStartRotation = main.rotation;
        shotStartFov = mainCamera.fieldOfView;

        currentShot = 0;
        shotTimer = 0f;
        holdingShot = false;
        holdTimer = 0f;
        shotSequenceDone = false;

        smoothedLookTarget = PandaLookPoint;
        lookTargetVelocity = Vector3.zero;

        cinematicCamera.gameObject.SetActive(true);
        mainCamera.gameObject.SetActive(false);

        Debug.Log("PandaPathMover: Cinematic camera started.");
    }

    private void UpdateCinematicCamera()
    {
        if (cinematicCamera == null ||
            activeShots == null ||
            activeShots.Count == 0)
            return;

        UpdateLookTarget();

        Transform cam = cinematicCamera.transform;

        // All shots done: stay on the last one until the panda arrives.
        if (shotSequenceDone)
        {
            CameraShot last = activeShots[activeShots.Count - 1];

            cam.position = last.point.position;
            cam.rotation = GetShotRotation(last, cam.position);
            cinematicCamera.fieldOfView = GetShotFov(last);
            return;
        }

        CameraShot shot = activeShots[currentShot];

        // Holding at a point.
        if (holdingShot)
        {
            holdTimer += Time.deltaTime;

            cam.position = shot.point.position;
            cam.rotation = GetShotRotation(shot, cam.position);
            cinematicCamera.fieldOfView = GetShotFov(shot);

            if (holdTimer >= shot.holdDuration)
            {
                holdingShot = false;
                AdvanceShot();
            }

            return;
        }

        // Moving toward this shot.
        shotTimer += Time.deltaTime;

        float t = Mathf.Clamp01(shotTimer / GetShotDuration(shot));
        float eased = Ease(shot.ease, t);

        // Path index 0 is where the camera started,
        // path index i is shot i-1.
        Vector3 p1 = GetPathPoint(currentShot);
        Vector3 p2 = GetPathPoint(currentShot + 1);

        if (curvedCameraPath)
        {
            Vector3 p0 = GetPathPoint(currentShot - 1);
            Vector3 p3 = GetPathPoint(currentShot + 2);
            cam.position = CatmullRom(p0, p1, p2, p3, eased);
        }
        else
        {
            cam.position = Vector3.Lerp(p1, p2, eased);
        }

        Quaternion fromRotation = currentShot == 0
            ? shotStartRotation
            : GetShotRotation(activeShots[currentShot - 1], cam.position);

        Quaternion toRotation = GetShotRotation(shot, cam.position);

        cam.rotation = Quaternion.Slerp(fromRotation, toRotation, eased);

        float fromFov = currentShot == 0
            ? shotStartFov
            : GetShotFov(activeShots[currentShot - 1]);

        cinematicCamera.fieldOfView =
            Mathf.Lerp(fromFov, GetShotFov(shot), eased);

        if (t >= 1f)
        {
            if (shot.holdDuration > 0f)
            {
                holdingShot = true;
                holdTimer = 0f;
            }
            else
            {
                AdvanceShot();
            }
        }
    }

    private void AdvanceShot()
    {
        Debug.Log($"PandaPathMover: Camera reached shot {currentShot}.");

        currentShot++;
        shotTimer = 0f;

        if (currentShot >= activeShots.Count)
        {
            currentShot = activeShots.Count - 1;
            shotSequenceDone = true;
        }
    }

    private Vector3 GetPathPoint(int index)
    {
        index = Mathf.Clamp(index, 0, activeShots.Count);

        return index == 0
            ? shotStartPosition
            : activeShots[index - 1].point.position;
    }

    private Quaternion GetShotRotation(CameraShot shot, Vector3 cameraPosition)
    {
        if (!shot.lookAtPanda)
            return shot.point.rotation;

        Vector3 direction = smoothedLookTarget - cameraPosition;

        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : shot.point.rotation;
    }

    private float GetShotFov(CameraShot shot)
    {
        return shot.fieldOfView > 0f ? shot.fieldOfView : cinematicFieldOfView;
    }

    private float GetShotDuration(CameraShot shot)
    {
        float duration = shot.moveDuration > 0f
            ? shot.moveDuration
            : defaultShotDuration;

        return Mathf.Max(duration, 0.01f);
    }

    // Follows the panda's look point with a little lag, so tiny
    // movements of the panda don't shake the camera.
    private void UpdateLookTarget()
    {
        smoothedLookTarget = Vector3.SmoothDamp(
            smoothedLookTarget,
            PandaLookPoint,
            ref lookTargetVelocity,
            Mathf.Max(lookTargetSmoothTime, 0.01f)
        );
    }

    private static float Ease(AnimationCurve curve, float t)
    {
        // An empty curve would freeze the camera, so fall back to smoothstep.
        if (curve == null || curve.length < 2)
            return t * t * (3f - 2f * t);

        return curve.Evaluate(t);
    }

    // Smooth curve through p1 -> p2, shaped by the points before and after.
    private static Vector3 CatmullRom(
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // Builds the shot list for a zone. Uses Camera Shots if set,
    // otherwise converts the old Camera Points list.
    private List<CameraShot> BuildShots(ZoneDestination zone)
    {
        List<CameraShot> shots = new List<CameraShot>();

        if (zone.cameraShots != null && zone.cameraShots.Count > 0)
        {
            foreach (CameraShot shot in zone.cameraShots)
            {
                if (shot != null && shot.point != null)
                    shots.Add(shot);
            }

            return shots;
        }

        if (zone.cameraPoints == null)
            return shots;

        List<Transform> points = new List<Transform>();

        foreach (Transform point in zone.cameraPoints)
        {
            if (point != null)
                points.Add(point);
        }

        for (int i = 0; i < points.Count; i++)
        {
            bool first = i == 0;
            bool last = i == points.Count - 1;

            // Ease in at the start, ease out at the end,
            // constant speed in between so the camera flows.
            AnimationCurve curve;

            if (first && last)
                curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            else if (first)
                curve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
            else if (last)
                curve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            else
                curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            shots.Add(new CameraShot
            {
                point = points[i],
                moveDuration = defaultShotDuration,
                ease = curve
            });
        }

        return shots;
    }

    // =========================================================
    // WALKING / ARRIVAL
    // =========================================================

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

    // Sets IsWalking to true once the stand-up animation has finished.
    // If the StandUp trigger had no effect (the Animator was in a state
    // with no StandUp transition), the stand-up is played directly.
    private void UpdateWalkingState()
    {
        if (animator == null || animator.GetBool(walkingParameter))
            return;

        float elapsed = Time.time - movementStartTime;
        bool timedOut = elapsed >= maxStandUpWait;
        bool eating = feedingZone != null && feedingZone.IsEatingAnimationPlaying;

        bool enteringStandUp =
            animator.IsInTransition(0) &&
            IsStandUpState(animator.GetNextAnimatorStateInfo(0));

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inStandUp = IsStandUpState(current);

        if (enteringStandUp || inStandUp)
            standUpStarted = true;

        // The trigger didn't start the stand-up: force it.
        if (!standUpStarted && !forcedStandUp && !eating &&
            elapsed >= forceStandUpAfter)
        {
            forcedStandUp = true;
            animator.ResetTrigger(standUpTrigger);

            if (CrossFadeToState(standUpStateName, "StandUp", "Stand Up", "Stand_Up"))
            {
                Debug.LogWarning(
                    "PandaPathMover: StandUp trigger had no effect. " +
                    "Forcing the stand-up animation."
                );
            }
            else
            {
                Debug.LogWarning(
                    "PandaPathMover: Could not find the stand-up state. " +
                    "Check 'Stand Up State Name' in the Inspector."
                );
            }

            return;
        }

        if (!timedOut)
        {
            // Still blending into or playing the stand-up.
            if (enteringStandUp)
                return;

            if (inStandUp && current.normalizedTime < standUpFinishedAt)
                return;

            // Stand-up hasn't begun yet.
            if (!standUpStarted)
                return;

            // Never walk while the eat animation is still playing.
            if (eating)
                return;
        }
        else
        {
            Debug.LogWarning(
                "PandaPathMover: Stand-up didn't finish in time. Walking anyway."
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

        // Normally StandUp -> Walk happens through IsWalking. If the
        // Animator is anywhere else, go straight to the walk state so
        // the panda never slides in a sitting pose.
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        bool goingToWalk =
            animator.IsInTransition(0) &&
            animator.GetNextAnimatorStateInfo(0).IsName(walkStateName);

        if (!IsStandUpState(current) &&
            !current.IsName(walkStateName) &&
            !goingToWalk)
        {
            if (CrossFadeToState(walkStateName))
            {
                Debug.LogWarning(
                    "PandaPathMover: Animator was not in StandUp. " +
                    "Forcing the walk animation."
                );
            }
            else
            {
                Debug.LogWarning(
                    "PandaPathMover: Could not find the walk state '" +
                    walkStateName + "'. Check 'Walk State Name' in the Inspector."
                );
            }
        }

        Debug.Log(
            $"PandaPathMover: StartWalkingAnimation called. " +
            $"IsWalking = {animator.GetBool(walkingParameter)}"
        );
    }

    // Blends directly into the first state name that exists in the Animator.
    private bool CrossFadeToState(params string[] stateNames)
    {
        if (animator == null)
            return false;

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrEmpty(stateName))
                continue;

            int hash = Animator.StringToHash(stateName);

            if (animator.HasState(0, hash))
            {
                animator.CrossFadeInFixedTime(hash, 0.2f, 0);
                return true;
            }
        }

        return false;
    }

    // Turns the panda toward where it should face and sets the walking
    // speed: slow (or zero) while facing away from the path, full speed
    // once lined up. This makes the panda curve onto the path instead of
    // spinning in place and then lurching forward.
    private void WalkAlongPath()
    {
        if (agent == null)
            return;

        if (agent.pathPending || !agent.hasPath)
        {
            HoldAgent();
            return;
        }

        float angleOff = TurnModelToward(
            GetWalkFacingRotation(),
            walkTurnSmoothTime
        );

        float speedFactor =
            1f - Mathf.InverseLerp(fullSpeedAngle, noMoveAngle, angleOff);

        agent.speed = baseAgentSpeed * speedFactor;

        if (agent.isStopped)
            agent.isStopped = false;
    }

    // Normally faces along the path. In the last stretch it blends toward
    // the sitting direction, so the panda arrives already facing the camera
    // instead of spinning around after it sits down.
    private Quaternion GetWalkFacingRotation()
    {
        float remaining = agent.remainingDistance;
        float lockDistance = Mathf.Max(arrivalBlendDistance, 0.3f);

        bool nearEnd =
            !float.IsInfinity(remaining) && remaining <= lockDistance;

        if (!nearEnd)
            return GetPathFacingRotation();

        if (!pathDirectionLocked)
        {
            lockedPathRotation = GetPathFacingRotation();
            pathDirectionLocked = true;
        }

        if (arrivalBlendDistance <= 0f)
            return lockedPathRotation;

        float span = Mathf.Max(
            arrivalBlendDistance - agent.stoppingDistance, 0.01f);

        float t = 1f - Mathf.Clamp01(
            (remaining - agent.stoppingDistance) / span);

        t = t * t * (3f - 2f * t);

        return Quaternion.Slerp(lockedPathRotation, GetSitFacingRotation(), t);
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
                $"PandaPathMover: Panda reached its destination. " +
                $"Sitting down with " +
                $"{Quaternion.Angle(GetModel().rotation, GetSitFacingRotation()):F0}° " +
                $"left to turn to face " +
                (sitSquareToCamera ? "straight into the main camera's view." :
                 currentSitFacingTarget != null ? $"the zone target '{currentSitFacingTarget.name}'." :
                 sitFacingTarget != null ? $"the default target '{sitFacingTarget.name}'." :
                 "the main camera's position.")
            );

            HoldAgent();
            StartSitDownAnimation();

            // Finish whatever little turning is left while sitting.
            turningToSit = true;
        }
    }

    private void StartSitDownAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(walkingParameter, false);
        animator.ResetTrigger(standUpTrigger);
        animator.SetTrigger(sitDownTrigger);

        Debug.Log("PandaPathMover: Sit-down trigger activated.");
    }

    private void FinishMovement()
    {
        isMoving = false;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.speed = baseAgentSpeed;
        }

        // The sit-down was already started on arrival.
        if (animator != null)
            animator.SetBool(walkingParameter, false);

        Debug.Log(
            "PandaPathMover: Travel finished. Returning camera to main view."
        );

        StartCameraReturn();
    }

    // =========================================================
    // CAMERA RETURN
    // =========================================================

    private void StartCameraReturn()
    {
        if (mainCamera == null || cinematicCamera == null)
            return;

        returningCamera = true;
        cameraReturnTimer = 0f;

        returnStartPosition = cinematicCamera.transform.position;
        returnStartRotation = cinematicCamera.transform.rotation;
        returnStartFov = cinematicCamera.fieldOfView;
    }

    private void ReturnCameraToMain()
    {
        if (mainCamera == null || cinematicCamera == null)
            return;

        cameraReturnTimer +=
            Time.deltaTime / Mathf.Max(cameraReturnDuration, 0.01f);

        float t = Mathf.Clamp01(cameraReturnTimer);
        float eased = Ease(cameraReturnEase, t);

        Transform cam = cinematicCamera.transform;
        Transform main = mainCamera.transform;

        cam.position = Vector3.Lerp(returnStartPosition, main.position, eased);
        cam.rotation = Quaternion.Slerp(returnStartRotation, main.rotation, eased);

        cinematicCamera.fieldOfView =
            Mathf.Lerp(returnStartFov, mainCamera.fieldOfView, eased);

        if (t >= 1f)
        {
            returningCamera = false;

            // Restore the cinematic camera's own zoom for next time.
            cinematicCamera.fieldOfView = cinematicFieldOfView;

            cam.position = main.position;
            cam.rotation = main.rotation;

            cinematicCamera.gameObject.SetActive(false);
            mainCamera.gameObject.SetActive(true);

            Debug.Log(
                "PandaPathMover: Main camera restored."
            );
        }
    }

    // =========================================================
    // TRAVEL
    // =========================================================

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

    private void BeginTravel(string requestedName)
    {
        ZoneDestination zone = FindRoute(requestedName);

        if (zone == null)
        {
            Debug.LogError(
                $"PandaPathMover: NO ROUTE FOUND for '{requestedName}'. " +
                "Check Route Name / Zone Name in the Zone Destinations list."
            );
            return;
        }

        string routeLabel = string.IsNullOrWhiteSpace(zone.routeName)
            ? zone.zoneName
            : zone.routeName;

        Debug.Log(
            $"PandaPathMover: Using route '{routeLabel}' to zone '{zone.zoneName}'."
        );

        if (zone.destination == null)
        {
            Debug.LogError(
                $"PandaPathMover: Destination Transform is missing for route '{routeLabel}'."
            );
            return;
        }

        List<CameraShot> shots = BuildShots(zone);

        if (shots.Count == 0)
        {
            Debug.LogError(
                $"PandaPathMover: No camera shots (or camera points) assigned for route '{routeLabel}'."
            );
            return;
        }

        destination = zone.destination;
        activeShots = shots;
        currentSitFacingTarget = zone.sitFacingTarget;

        StartMovement();

        // The environment always uses the REAL zone name, never the route name.
        if (isMoving && EnvironmentSystem.Instance != null)
            EnvironmentSystem.Instance.BeginTravelToZone(zone.zoneName);
    }

    // 1. An entry whose Route Name matches exactly (e.g. 'Midway2').
    // 2. Otherwise the entry whose Zone Name matches and has NO Route Name
    //    (the normal route). Entries with a Route Name are only used when
    //    asked for by that name.
    private ZoneDestination FindRoute(string requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        string name = requestedName.Trim();

        foreach (ZoneDestination zone in zoneDestinations)
        {
            if (zone != null &&
                !string.IsNullOrWhiteSpace(zone.routeName) &&
                string.Equals(zone.routeName.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return zone;
            }
        }

        foreach (ZoneDestination zone in zoneDestinations)
        {
            if (zone != null &&
                string.IsNullOrWhiteSpace(zone.routeName) &&
                string.Equals(zone.zoneName?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return zone;
            }
        }

        return null;
    }

    // ---------- Turning helpers ----------

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

    // Smoothly turns the model's yaw toward the target. It speeds up and
    // slows down naturally, and copes with a target that keeps moving
    // (no jumps when the path direction changes mid-turn).
    // Returns how many degrees are still left to turn.
    private float TurnModelToward(Quaternion target, float smoothTime)
    {
        Transform model = GetModel();
        float currentYaw = model.eulerAngles.y;
        float targetYaw = target.eulerAngles.y;

        float yaw = Mathf.SmoothDampAngle(
            currentYaw,
            targetYaw,
            ref yawVelocity,
            Mathf.Max(smoothTime, 0.01f),
            Mathf.Max(maxTurnSpeed, 1f)
        );

        SetModelYaw(yaw);

        return Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw));
    }

    private void SetModelYaw(float yaw)
    {
        Transform model = GetModel();
        Vector3 euler = model.eulerAngles;
        model.rotation = Quaternion.Euler(euler.x, yaw, euler.z);
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
        // Face back along the camera's view direction, so the panda
        // looks straight into the shot wherever it is in the frame.
        if (sitSquareToCamera && mainCamera != null)
            return FacingRotation(-mainCamera.transform.forward, sitAngleOffset);

        Transform target = currentSitFacingTarget != null
            ? currentSitFacingTarget
            : sitFacingTarget != null
                ? sitFacingTarget
                : mainCamera != null ? mainCamera.transform : null;

        if (target == null)
            return GetModel().rotation;

        return FacingRotation(target.position - transform.position, sitAngleOffset);
    }
}