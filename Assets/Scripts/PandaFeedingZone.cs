
using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private PandaSystem pandaSystem;

    

public void FeedPanda()
{
    if (pandaSystem == null || BambooManager.Instance == null)
    {
        Debug.LogWarning("Cannot feed: Missing PandaSystem or BambooManager.");
        return;
    }

    if (EnvironmentSystem.Instance == null ||
        EnvironmentSystem.Instance.ActiveZone == null)
    {
        Debug.Log("Cannot feed: Panda is not inside an ecosystem zone.");
        return;
    }

    // Feeding is only possible if real bamboo is consumed
    // from the panda's current zone.
    if (!BambooManager.Instance.ConsumeBamboo())
    {
        Debug.Log("Cannot feed: No bamboo available in this zone.");
        return;
    }

    if (pandaAnimator != null)
        pandaAnimator.SetTrigger("Eat");

    pandaSystem.FeedPanda();

    BambooManager.Instance.PandaPooped();
}
}