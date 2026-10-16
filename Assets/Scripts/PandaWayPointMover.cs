using UnityEngine;

/// <summary>
/// Moves the panda along a path made of empty GameObjects (waypoints),
/// in order, without using a NavMesh.
/// </summary>
public class PandaWaypointMover : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Empty GameObjects the panda walks to, in order.")]
    [SerializeField] private Transform[] waypoints;

    [Tooltip("Start walking automatically when the scene starts.")]
    [SerializeField] private bool startOnPlay = false;

    [Tooltip("Go back to the first waypoint and keep walking forever.")]
    [SerializeField] private bool loop = false;

    [Tooltip("Only let the Travel button start the walk once.")]
    [SerializeField] private bool onlyTravelOnce = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeed = 8f;

    [Tooltip("How close the panda must get before moving to the next waypoint.")]
    [SerializeField] private float reachDistance = 0.1f;

    [Tooltip("Keep the panda at its current height instead of following waypoint heights.")]
    [SerializeField] private bool keepCurrentHeight = true;

    [Header("Facing Direction")]
    [Tooltip("The visual model to rotate. Leave empty to rotate this object.")]
    [SerializeField] private Transform pandaModel;

    [Tooltip("Extra rotation if the model faces the wrong way (try 0 or 180).")]
    [SerializeField] private float facingCorrection = 0f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameter = "IsWalking";
    [SerializeField] private string sitDownTrigger = "SitDown";

    private int currentWaypoint;
    private bool isMoving;
    private bool hasTravelled;

    public bool IsMoving => isMoving;

    private void Start()
    {
        if (pandaModel == null)
            pandaModel = transform;

        if (startOnPlay)
            StartPath();
    }

    /// <summary>Starts walking from the first waypoint. Can be called from a UI button.</summary>
    public void StartPath()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError("PandaWaypointMover: No waypoints assigned.");
            return;
        }

        if (isMoving)
        {
            Debug.Log("PandaWaypointMover: Already walking; ignoring the button press.");
            return;
        }

        if (onlyTravelOnce && hasTravelled)
        {
            Debug.Log("PandaWaypointMover: Path already completed; ignoring the button press.");
            return;
        }

        currentWaypoint = 0;
        isMoving = true;
        hasTravelled = true;
        SetWalking(true);

        Debug.Log("PandaWaypointMover: Travel started.");
    }

    /// <summary>
    /// Same as StartPath, for buttons or scripts that still send a zone name
    /// (e.g. code that used to call PandaPathMover.TravelToZone).
    /// </summary>
    public void TravelToZone(string zoneName)
    {
        Debug.Log($"PandaWaypointMover: Travel requested for '{zoneName}'.");
        StartPath();
    }

    /// <summary>Stops the panda where it is.</summary>
    public void StopPath()
    {
        isMoving = false;
        SetWalking(false);
    }

    private void Update()
    {
        if (!isMoving)
            return;

        Transform target = waypoints[currentWaypoint];

        if (target == null)
        {
            Debug.LogWarning($"PandaWaypointMover: Waypoint {currentWaypoint} is missing; skipping it.");
            NextWaypoint();
            return;
        }

        Vector3 targetPosition = target.position;

        if (keepCurrentHeight)
            targetPosition.y = transform.position.y;

        // Move in a straight line towards the waypoint.
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        // Turn to face the waypoint.
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction) *
                Quaternion.Euler(0f, facingCorrection, 0f);

            pandaModel.rotation = Quaternion.Slerp(
                pandaModel.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        if (Vector3.Distance(transform.position, targetPosition) <= reachDistance)
            NextWaypoint();
    }

    private void NextWaypoint()
    {
        currentWaypoint++;

        if (currentWaypoint < waypoints.Length)
            return;

        if (loop)
        {
            currentWaypoint = 0;
            return;
        }

        // Reached the last waypoint.
        isMoving = false;
        SetWalking(false);

        if (animator != null && !string.IsNullOrEmpty(sitDownTrigger))
            animator.SetTrigger(sitDownTrigger);

        Debug.Log("PandaWaypointMover: Reached the end of the path.");
    }

    private void SetWalking(bool walking)
    {
        if (animator != null && !string.IsNullOrEmpty(walkingParameter))
            animator.SetBool(walkingParameter, walking);
    }

    // Draws the path in the Scene view so you can see it while editing.
    private void OnDrawGizmos()
    {
        if (waypoints == null)
            return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.15f);

            if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
}