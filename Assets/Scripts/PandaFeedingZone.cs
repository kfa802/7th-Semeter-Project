using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;

    public void FeedPanda()
    {
        Debug.Log("PandaFeedingZone: Panda was fed!");

        // Play eating animation
        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }

        // Tell BambooManager that the panda pooped
        if (BambooManager.Instance != null)
        {
            Debug.Log("PandaFeedingZone: Calling BambooManager.");
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