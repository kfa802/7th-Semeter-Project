using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [SerializeField] private Animator pandaAnimator;

    public void FeedPanda()
    {
        if (pandaAnimator != null)
        {
            pandaAnimator.SetTrigger("Eat");
        }
    }
}