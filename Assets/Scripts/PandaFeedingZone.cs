
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
        Debug.LogWarning(
            "Cannot feed: Missing PandaSystem or BambooManager."
        );
        return;
    }

    if (EnvironmentSystem.Instance == null ||
        EnvironmentSystem.Instance.ActiveZone == null)
    {
        Debug.Log(
            "Cannot feed: Panda is not inside an ecosystem zone."
        );
        return;
    }

    // Only continue if bamboo can actually be consumed.
    if (!BambooManager.Instance.ConsumeBamboo())
    {
        Debug.Log(
            "Cannot feed: No bamboo available in this zone."
        );
        return;
    }

    // Apply the feeding logic.
    pandaSystem.FeedPanda();

    // Trigger the eating animation.
    if (pandaAnimator != null)
    {
        pandaAnimator.ResetTrigger("Eat");
        pandaAnimator.SetTrigger("Eat");

        Debug.Log("PandaFeedingZone: Eating animation triggered.");
    }

    // Bamboo is consumed, so the panda can poop.
    BambooManager.Instance.PandaPooped();
}
}