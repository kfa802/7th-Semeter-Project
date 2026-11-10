using UnityEngine;

// Drives a car along a list of waypoints.
// Put this on the PARENT object of the car model (see setup steps).
public class WaypointCar : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("Drag your waypoint objects in here, in driving order.")]
    public Transform[] waypoints;
    [Tooltip("Start over at the first waypoint after the last one.")]
    public bool loop = true;

    [Header("Driving")]
    public float speed = 5f;            // metres per second
    public float turnSpeed = 4f;        // how quickly the car turns toward the next point
    public float reachDistance = 0.5f;  // how close counts as "arrived" at a waypoint

    [Header("Wheels (optional)")]
    [Tooltip("Wheel meshes to spin. Leave empty if the model has no separate wheels.")]
    public Transform[] wheels;
    public float wheelRadius = 0.3f;

    private int currentIndex = 0;
    private bool finished = false;

    // Runs every time the car is switched on (e.g. when the road appears),
    // so the car always starts the route from the beginning.
    void OnEnable()
    {
        currentIndex = 0;
        finished = false;

        // Place the car on the first waypoint, facing the second
        if (waypoints != null && waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            if (waypoints.Length > 1)
            {
                transform.LookAt(waypoints[1].position);
                currentIndex = 1;
            }
        }
    }

    void Update()
    {
        if (finished || waypoints == null || waypoints.Length == 0) return;

        Vector3 target = waypoints[currentIndex].position;
        Vector3 direction = target - transform.position;
        direction.y = 0f; // keep the car level

        // Turn smoothly toward the waypoint
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // Drive forward the way the car is facing (gives nice curved turns)
        float step = speed * Time.deltaTime;
        transform.position += transform.forward * step;
        // Follow the height of the waypoints (for hills)
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, target.y, 5f * Time.deltaTime);
        transform.position = pos;

        SpinWheels(step);

        // Arrived? Go to the next waypoint
        if (direction.magnitude < reachDistance)
        {
            currentIndex++;
            if (currentIndex >= waypoints.Length)
            {
                if (loop) currentIndex = 0;
                else finished = true;
            }
        }
    }

    void SpinWheels(float distance)
    {
        if (wheels == null || wheelRadius <= 0f) return;
        float degrees = distance / (2f * Mathf.PI * wheelRadius) * 360f;
        foreach (Transform w in wheels)
        {
            if (w != null) w.Rotate(degrees, 0f, 0f, Space.Self);
        }
    }

    // Draws the route as yellow lines in the Scene view
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] && waypoints[i + 1])
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
        if (loop && waypoints[0] && waypoints[waypoints.Length - 1])
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        foreach (Transform w in waypoints)
            if (w) Gizmos.DrawSphere(w.position, 0.3f);
    }
}