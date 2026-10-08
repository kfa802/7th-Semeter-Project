using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private PandaSystem pandaSystem;

    public void FeedPanda()
    {
        Debug.Log("PandaFeedingZone: FeedPanda() was called!");

        // =====================================================
        // PANDA ANIMATION
        // =====================================================

        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }

        // =====================================================
        // PANDA HEALTH
        // =====================================================

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

        // =====================================================
        // BAMBOO / POOP
        // =====================================================

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