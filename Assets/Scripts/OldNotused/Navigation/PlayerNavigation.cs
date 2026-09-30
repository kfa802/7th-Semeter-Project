using UnityEngine;
using UnityEngine.AI;

public class PlayerNavigation : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Navigation Points")]
    [SerializeField] private NavigationPoint[] navigationPoints;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    public void MoveToPoint(NavigationPoint point)
    {
        if (point == null)
            return;

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Player is NOT on the NavMesh!");
            return;
        }

        Vector3 destination = point.GetDestination();

        Debug.Log("Moving to navigation point: " + point.name);

        agent.SetDestination(destination);
    }

    public bool IsNavigationPointRegistered(NavigationPoint point)
    {
        if (navigationPoints == null)
            return false;

        foreach (NavigationPoint registeredPoint in navigationPoints)
        {
            if (registeredPoint == point)
                return true;
        }

        return false;
    }
}