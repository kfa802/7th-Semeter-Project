using UnityEngine;

public class NavigationPoint : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Transform destination;

    public Vector3 GetDestination()
    {
        if (destination != null)
            return destination.position;

        return transform.position;
    }
}