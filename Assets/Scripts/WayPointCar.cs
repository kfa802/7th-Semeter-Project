using System.Collections;
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
    [Tooltip("When looping: hide the car at the last waypoint, wait, then let it appear at the first waypoint again.")]
    public bool hideAndRestart = true;
    [Tooltip("Seconds the car stays hidden before it drives again.")]
    public float restartDelay = 3f;

    [Header("Driving")]
    public float speed = 5f;            // metres per second
    public float turnSpeed = 4f;        // how quickly the car turns toward the next point
    public float reachDistance = 0.5f;  // how close counts as "arrived" at a waypoint
    [Tooltip("Tick this if the game is paused (Time.timeScale = 0) while the car should drive, e.g. during a choice menu.")]
    public bool ignorePause = false;

    [Header("Wheels (optional)")]
    [Tooltip("Wheel meshes to spin. Leave empty if the model has no separate wheels.")]
    public Transform[] wheels;
    public float wheelRadius = 0.3f;

    private int currentIndex = 0;
    private bool finished = false;
    private bool waiting = false;

    // Runs every time the car is switched on (e.g. when the road appears),
    // so the car always starts the route from the beginning.
    void OnEnable()
    {
        finished = false;
        waiting = false;
        SetVisible(true);

        if (waypoints == null || waypoints.Length == 0)
            Debug.LogWarning(name + ": WaypointCar has no waypoints, so the car can't drive.", this);
        if (Time.timeScale == 0f && !ignorePause)
            Debug.LogWarning(name + ": the game is paused (Time.timeScale = 0). Tick 'Ignore Pause' if the car should drive anyway.", this);

        PlaceAtStart();
    }

    // Puts the car on the first waypoint, facing the second
    void PlaceAtStart()
    {
        currentIndex = 0;
        if (waypoints == null || waypoints.Length == 0) return;

        transform.position = waypoints[0].position;
        if (waypoints.Length > 1)
        {
            Vector3 dir = waypoints[1].position - waypoints[0].position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            currentIndex = 1;
        }
    }

    // Hide the car, wait, then put it back at the start
    IEnumerator HideAndRestart()
    {
        waiting = true;
        SetVisible(false);

        if (ignorePause) yield return new WaitForSecondsRealtime(restartDelay);
        else yield return new WaitForSeconds(restartDelay);

        PlaceAtStart();
        SetVisible(true);
        waiting = false;
    }

    // Shows or hides all meshes on the car (the script keeps running)
    void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    void Update()
    {
        if (finished || waiting || waypoints == null || waypoints.Length == 0) return;

        float dt = ignorePause ? Time.unscaledDeltaTime : Time.deltaTime;

        Vector3 target = waypoints[currentIndex].position;
        Vector3 direction = target - transform.position;
        direction.y = 0f; // keep the car level

        // Turn smoothly toward the waypoint
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * dt);
        }

        // Drive forward the way the car is facing (gives nice curved turns)
        float step = speed * dt;
        transform.position += transform.forward * step;
        // Follow the height of the waypoints (for hills)
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, target.y, 5f * dt);
        transform.position = pos;

        SpinWheels(step);

        // Arrived? Go to the next waypoint
        if (direction.magnitude < reachDistance)
        {
            currentIndex++;
            if (currentIndex >= waypoints.Length)
            {
                if (loop && hideAndRestart) StartCoroutine(HideAndRestart());
                else if (loop) currentIndex = 0;
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
        if (loop && !hideAndRestart && waypoints[0] && waypoints[waypoints.Length - 1])
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        foreach (Transform w in waypoints)
            if (w) Gizmos.DrawSphere(w.position, 0.3f);
    }
}