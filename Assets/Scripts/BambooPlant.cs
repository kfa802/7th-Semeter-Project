using System.Collections;
using UnityEngine;

public class BambooPlant : MonoBehaviour
{
    [Header("Growth")]
    [SerializeField] private float growthDuration = 2f;

    [Header("Bamboo Size")]
    [SerializeField] private float minSize = 1.5f;
    [SerializeField] private float maxSize = 2.5f;

    private Vector3 targetScale;
    private bool growing;

    private void Awake()
    {
        // Remember the scale the prefab actually has
        Vector3 originalScale = transform.localScale;

        // Pick a random size
        float randomSize = Random.Range(minSize, maxSize);

        // This is the final size this bamboo will grow to
        targetScale = originalScale * randomSize;

        Debug.Log(
            "Bamboo target scale: " + targetScale +
            " | Random size: " + randomSize
        );
    }

    public void Grow()
    {
        if (growing)
            return;

        StartCoroutine(GrowAnimation());
    }

    private IEnumerator GrowAnimation()
    {
        growing = true;

        // Start tiny
        transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < growthDuration)
        {
            timer += Time.deltaTime;

            float progress =
                Mathf.Clamp01(timer / growthDuration);

            float smoothProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    targetScale,
                    smoothProgress
                );

            yield return null;
        }

        // IMPORTANT:
        // Force the final size
        transform.localScale = targetScale;

        Debug.Log(
            "Bamboo finished growing at scale: " +
            transform.localScale
        );

        growing = false;
    }
}