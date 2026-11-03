using UnityEngine;

/// <summary>
/// Lets an Animation Event on the bamboo's Grow animation start the
/// growing sound on an exact frame.
///
/// Put this on the bamboo prefab, on the SAME object as its Animator.
/// Then add an Animation Event to the Grow clip that calls PlayGrowSound.
/// </summary>
public class BambooGrowSoundEvent : MonoBehaviour
{
    [Tooltip("Write to the Console what happens (turn off when it works).")]
    [SerializeField] private bool debugLog = true;

    private void Start()
    {
        if (!debugLog)
            return;

        Animator animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogWarning(
                "BambooGrowSoundEvent on '" + name + "': there is NO Animator on this object. " +
                "Move this component to the object that has the Animator.", this);
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("BambooGrowSoundEvent on '" + name + "': the Animator has no controller.", this);
            return;
        }

        // Which clips does this Animator use, and do they have the event?
        bool found = false;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
                continue;

            foreach (AnimationEvent e in clip.events)
            {
                if (e.functionName == nameof(PlayGrowSound))
                {
                    found = true;
                    Debug.Log(
                        "BambooGrowSoundEvent on '" + name + "': OK, clip '" + clip.name +
                        "' calls PlayGrowSound at " + e.time.ToString("F2") + " s.", this);
                }
            }
        }

        if (!found)
        {
            Debug.LogWarning(
                "BambooGrowSoundEvent on '" + name + "': NONE of this Animator's clips has a " +
                "PlayGrowSound event. Check that the Grow state's Motion is the clip WITH the event " +
                "(your copy, not the read-only one).", this);
        }
    }

    // Called by the Animation Event.
    public void PlayGrowSound()
    {
        if (debugLog)
            Debug.Log("BambooGrowSoundEvent: PlayGrowSound event FIRED on '" + name + "'.", this);

        if (BambooManager.Instance == null)
        {
            Debug.LogWarning("BambooGrowSoundEvent: no BambooManager in the scene.", this);
            return;
        }

        BambooManager.Instance.PlayGrowSoundFromAnimation(transform, debugLog);
    }
}