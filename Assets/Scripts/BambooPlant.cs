
using UnityEngine;

public class BambooPlant : MonoBehaviour
{
    private Animator animator;

    private float growTime = 10f;
    private float progress;
    private bool fullyGrown;
    private bool initializedForGrowth;

    public bool IsFullyGrown => fullyGrown;

    public void Init(float time)
    {
        growTime = Mathf.Max(0.1f, time);
        initializedForGrowth = true;

        progress = 0f;
        fullyGrown = false;

        FindAnimator();

        if (HasAnimatorController())
        {
            animator.speed = 1f;
            animator.Play("Grow", 0, 0f);
        }
    }

    private void Start()
    {
        FindAnimator();

        // Bamboo already present in the scene starts in Idle.
        if (!initializedForGrowth)
        {
            fullyGrown = true;

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

    private void Update()
    {
        if (fullyGrown)
            return;

        float temperatureFactor =
            EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.TemperatureGrowthFactor
                : 1f;

        temperatureFactor = Mathf.Max(0f, temperatureFactor);

        progress += Time.deltaTime * temperatureFactor / growTime;

        if (HasAnimatorController())
            animator.speed = temperatureFactor;

if (progress >= 1f)
{
    fullyGrown = true;

    if (HasAnimatorController())
    {
        animator.speed = 1f;
        animator.Play("Idle", 0, 0f);
        animator.Update(0f);
    }
}
    }
}
