using UnityEngine;

public class BambooPlant : MonoBehaviour
{
    [Tooltip("How far into the Grow animation (0-1) the bamboo already counts as " +
             "grown and can be eaten. 0.8 = when 80% of the animation has played. " +
             "The animation still plays to the end.")]
    [SerializeField, Range(0.1f, 1f)] private float countAsGrownAt = 0.8f;

    [Tooltip("Name of the grow state in the Animator.")]
    [SerializeField] private string growStateName = "Grow";

    [Tooltip("Seconds to blend from the end of Grow into Idle/Windy, so it doesn't jump.")]
    [SerializeField, Min(0f)] private float blendToIdleTime = 0.3f;

    private Animator animator;

    private float growTime = 10f;
    private float progress;
    private bool fullyGrown;          // counts as bamboo / can be eaten
    private bool animationFinished;   // Grow animation done, now Idle/Windy
    private bool initializedForGrowth;

    public bool IsFullyGrown => fullyGrown;

    public void Init(float time)
    {
        growTime = Mathf.Max(0.1f, time);
        initializedForGrowth = true;

        progress = 0f;
        fullyGrown = false;
        animationFinished = false;

        FindAnimator();

        if (HasAnimatorController())
        {
            animator.speed = 1f;
            animator.Play(growStateName, 0, 0f);
        }
    }

    private void Start()
    {
        FindAnimator();

        // Bamboo already present in the scene starts in Idle.
        if (!initializedForGrowth)
        {
            fullyGrown = true;
            animationFinished = true;

            if (HasAnimatorController())
            {
                animator.speed = 1f;
                animator.Play("Idle", 0, 0f);
            }
        }
    }

    private void FindAnimator()
    {
        animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            Debug.LogWarning(
                "BambooPlant: No Animator found on " + gameObject.name,
                this
            );
        }
    }

    private bool HasAnimatorController()
    {
        return animator != null &&
               animator.runtimeAnimatorController != null;
    }

    // How much of the Grow animation has played (0-1). -1 = not in the Grow state.
    private float GrowAnimationProgress()
    {
        if (!HasAnimatorController())
            return -1f;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (!state.IsName(growStateName))
            return -1f;

        return Mathf.Clamp01(state.normalizedTime);
    }

    private void Update()
    {
        if (animationFinished)
            return;

        float temperatureFactor =
            EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.TemperatureGrowthFactor
                : 1f;

        temperatureFactor = Mathf.Max(0f, temperatureFactor);

        progress += Time.deltaTime * temperatureFactor / growTime;

        if (HasAnimatorController())
            animator.speed = temperatureFactor;

        // Use whichever is further along: the grow timer or the animation itself.
        float animation = GrowAnimationProgress();
        float grown = Mathf.Max(progress, animation);

        // 1. Counts as bamboo a bit before the animation is completely done.
        if (!fullyGrown && grown >= countAsGrownAt)
        {
            fullyGrown = true;

            // Now it can be eaten: add it to the bamboo count.
            if (BambooManager.Instance != null)
                BambooManager.Instance.RefreshZoneStatistics();
        }

        // 2. The animation (or the timer) is finished: switch to Idle/Windy.
        if (grown >= 1f)
        {
            animationFinished = true;
            fullyGrown = true;

            if (HasAnimatorController())
            {
                animator.speed = 1f;

                // Only take over if the Animator hasn't already moved on by itself
                // (a hard jump here is what caused the jitter).
                bool stillInGrow = animator.GetCurrentAnimatorStateInfo(0).IsName(growStateName) &&
                                   !animator.IsInTransition(0);

                if (stillInGrow)
                {
                    // Windy if it's cold, otherwise Idle - blended smoothly.
                    bool windy = animator.GetBool("IsWindy");
                    animator.CrossFadeInFixedTime(windy ? "Windy" : "Idle", blendToIdleTime, 0);
                }
            }

            if (BambooManager.Instance != null)
                BambooManager.Instance.RefreshZoneStatistics();
        }
    }
}