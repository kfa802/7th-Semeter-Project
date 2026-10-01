using UnityEngine;

public class BambooPlant : MonoBehaviour
{
    private Animator animator;
    private float growTime = 10f;
    private float progress;
    private bool fullyGrown;

    public void Init(float time)
    {
        growTime = Mathf.Max(0.1f, time);
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (fullyGrown)
            return;

        // 1 = normal speed, small number = very slow
        float factor = EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.TemperatureGrowthFactor
            : 1f;

        progress += Time.deltaTime * factor / growTime;

        // Slows the grow animation
        if (animator != null)
            animator.speed = factor;

        if (progress >= 1f)
        {
            fullyGrown = true;

            if (animator != null)
                animator.speed = 1f;
        }
    }
}