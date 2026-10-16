using UnityEngine;

/// <summary>
/// Turns the windy bamboo animation on when the temperature is low.
/// Put this on each bamboo object, or on the bamboo prefab, so bamboo that
/// grows or spawns during play also reacts to the wind.
/// </summary>
public class BambooWindController : MonoBehaviour
{
    [Header("Temperature")]
    [Tooltip("The bamboo becomes windy when the temperature drops below this (°C).")]
    [SerializeField] private float windyBelowTemperature = 5f;

    [Tooltip("The temperature must rise this much above the limit before the wind stops. " +
             "Stops the bamboo flickering on and off right at the limit.")]
    [SerializeField] private float margin = 1f;

    [Header("Animation")]
    [Tooltip("Bool parameter in the bamboo Animator Controller.")]
    [SerializeField] private string windyParameter = "IsWindy";

    [Tooltip("Leave empty to find the Animator on this object or its children.")]
    [SerializeField] private Animator animator;

    [Tooltip("Start at a random point in the animation so bamboo doesn't sway in sync. " +
             "Leave OFF if the controller also plays Grow, or it will cut the Grow animation short.")]
    [SerializeField] private bool randomizeStart = false;

    [Header("Debug (read only)")]
    [SerializeField] private bool isWindy;

    private int windyHash;

    private void Start()
    {
        windyHash = Animator.StringToHash(windyParameter);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "BambooWindController: No Animator with a controller found on this bamboo.",
                this
            );
            enabled = false;
            return;
        }

        if (randomizeStart)
            animator.Play(0, 0, Random.value);

        // Set the correct state straight away, so new bamboo spawned
        // in the cold starts windy immediately.
        isWindy = GetTemperature() < windyBelowTemperature;
        animator.SetBool(windyHash, isWindy);
    }

    private void Update()
    {
        if (EnvironmentSystem.Instance == null)
            return;

        float temperature = GetTemperature();
        bool shouldBeWindy = isWindy;

        if (!isWindy && temperature < windyBelowTemperature)
            shouldBeWindy = true;
        else if (isWindy && temperature > windyBelowTemperature + margin)
            shouldBeWindy = false;

        if (shouldBeWindy != isWindy)
        {
            isWindy = shouldBeWindy;
            animator.SetBool(windyHash, isWindy);
        }
    }

    private float GetTemperature()
    {
        return EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.Temperature
            : float.MaxValue;
    }
}