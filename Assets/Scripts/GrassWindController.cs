using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns the windy grass animation on when the temperature is low.
/// Put this on a parent object that holds all your grass (or on a single grass object).
/// It finds every Animator in its children and sets a bool parameter on them.
/// </summary>
public class GrassWindController : MonoBehaviour
{
    [Header("Temperature")]
    [Tooltip("The grass becomes windy when the temperature drops below this (°C).")]
    [SerializeField] private float windyBelowTemperature = 5f;

    [Tooltip("The temperature must rise this much above the limit before the wind stops. " +
             "Stops the grass flickering on and off right at the limit.")]
    [SerializeField] private float margin = 1f;

    [Header("Animation")]
    [Tooltip("Bool parameter in the grass Animator Controller.")]
    [SerializeField] private string windyParameter = "IsWindy";

    [Tooltip("Leave empty to find all Animators in this object's children automatically.")]
    [SerializeField] private List<Animator> grassAnimators = new List<Animator>();

    [Tooltip("Start each grass at a random point in its animation so they don't sway in sync.")]
    [SerializeField] private bool randomizeStart = true;

    [Header("Debug (read only)")]
    [SerializeField] private bool isWindy;

    private int windyHash;

    private void Start()
    {
        windyHash = Animator.StringToHash(windyParameter);

        if (grassAnimators.Count == 0)
            grassAnimators.AddRange(GetComponentsInChildren<Animator>());

        if (grassAnimators.Count == 0)
            Debug.LogWarning("GrassWindController: No grass Animators found.", this);

        if (EnvironmentSystem.Instance == null)
            Debug.LogWarning("GrassWindController: No EnvironmentSystem in the scene.", this);

        if (randomizeStart)
        {
            foreach (Animator grass in grassAnimators)
            {
                if (grass != null)
                    grass.Play(0, 0, Random.value);
            }
        }

        // Set the correct state straight away.
        isWindy = GetTemperature() < windyBelowTemperature;
        ApplyToAll(isWindy);
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
            ApplyToAll(isWindy);

            Debug.Log(
                $"GrassWindController: Temperature {temperature:F1}°C → " +
                (isWindy ? "windy." : "calm.")
            );
        }
    }

    private float GetTemperature()
    {
        return EnvironmentSystem.Instance != null
            ? EnvironmentSystem.Instance.Temperature
            : float.MaxValue;
    }

    private void ApplyToAll(bool windy)
    {
        foreach (Animator grass in grassAnimators)
        {
            if (grass != null && grass.runtimeAnimatorController != null)
                grass.SetBool(windyHash, windy);
        }
    }
}