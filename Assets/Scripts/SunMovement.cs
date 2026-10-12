
using UnityEngine;

public class SunMovement : MonoBehaviour
{
    [Header("Sun Movement")]
    [SerializeField] private float movementSpeed = 0.15f;
    [SerializeField] private float movementAngle = 8f;

    private Quaternion startingRotation;

    private void Start()
    {
        startingRotation = transform.rotation;
    }

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * movementSpeed)
                      * movementAngle;

        transform.rotation = startingRotation
                           * Quaternion.Euler(angle, 0f, 0f);
    }
}
