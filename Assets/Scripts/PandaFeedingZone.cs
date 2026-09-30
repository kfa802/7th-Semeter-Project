using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private BambooGrowthManager bambooGrowthManager;

    public void FeedPanda()
    {
        Debug.Log("PandaFeedingZone: Panda was fed!");

        // Play eating animation
        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }

        // Tell bamboo system that panda pooped
        if (bambooGrowthManager != null)
        {
            Debug.Log("PandaFeedingZone: Calling BambooGrowthManager.");
            bambooGrowthManager.PandaPooped();
        }
        else
        {
            Debug.LogError(
                "PandaFeedingZone: BambooGrowthManager is NOT assigned!"
            );
        }
    }
}