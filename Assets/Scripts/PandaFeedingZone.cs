using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Animator pandaAnimator;

    [SerializeField]
    private PandaSystem pandaSystem;


    public void FeedPanda()
    {
        Debug.Log(
            "PandaFeedingZone: FeedPanda() was called!"
        );


        // =====================================================
        // CHECK PANDA
        // =====================================================

        if (pandaSystem == null)
        {
            Debug.LogError(
                "PandaFeedingZone: PandaSystem is NOT assigned!"
            );

            return;
        }


        // =====================================================
        // CHECK BAMBOO MANAGER
        // =====================================================

        if (BambooManager.Instance == null)
        {
            Debug.LogError(
                "PandaFeedingZone: BambooManager.Instance is NOT available!"
            );

            return;
        }


        // =====================================================
        // CONSUME BAMBOO
        // =====================================================

        bool bambooConsumed =
            BambooManager.Instance.ConsumeBamboo();

        if (!bambooConsumed)
        {
            Debug.Log(
                "PandaFeedingZone: No bamboo available. " +
                "Panda cannot eat."
            );

            return;
        }


        // =====================================================
        // PLAY EAT ANIMATION
        // =====================================================

        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }


        // =====================================================
        // FEED PANDA
        // =====================================================

        pandaSystem.FeedPanda();


        // =====================================================
        // POOP / BAMBOO GROWTH
        // =====================================================

        BambooManager.Instance.PandaPooped();
    }
}