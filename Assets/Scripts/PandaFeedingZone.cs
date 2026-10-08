using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private PandaSystem pandaSystem;

    public void FeedPanda()
    {
        Debug.Log("PandaFeedingZone: FeedPanda() was called!");

        // Panda animation
        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }

        // Give health immediately
        if (pandaSystem != null)
        {
            pandaSystem.FeedPanda();
        }
        else
        {
            Debug.LogError(
                "PandaFeedingZone: PandaSystem is NOT assigned!"
            );
        }

        // Trigger poop / bamboo growth
        if (BambooManager.Instance != null)
        {
            BambooManager.Instance.PandaPooped();
        }
        else
        {
            Debug.LogError(
                "PandaFeedingZone: BambooManager.Instance is NOT available!"
            );
        }
    }
}