
using System.Collections;
using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private PandaSystem pandaSystem;

    [Header("Eating Bamboo Visual")]
    [SerializeField] private GameObject eatingBambooPrefab;

    [Tooltip("Transform attached to the moving paw bone.")]
    [SerializeField] private Transform bambooSpawnPoint;

    [Tooltip("Transform on the ground where bamboo first appears.")]
    [SerializeField] private Transform bambooGroundPoint;

    [Tooltip("If no ground point is assigned, use this offset from the panda.")]
    [SerializeField] private Vector3 groundSpawnOffset =
        new Vector3(0f, 0.05f, 0.8f);

    [Tooltip("Time taken for bamboo to move from ground to paw.")]
    [SerializeField] private float bambooMoveDuration = 0.6f;

    [Tooltip("How long the bamboo remains visible in total.")]
    [SerializeField] private float bambooVisibleDuration = 2f;

    [Header("Eating Animation")]
    [SerializeField] private string eatTrigger = "Eat";

    [SerializeField] private float eatingCooldown = 3f;

    private bool isEating;
    private float nextAllowedEatingTime;
    private GameObject spawnedBamboo;
    private Coroutine eatingCoroutine;

    public bool IsEating => isEating;

    public void FeedPanda()
    {
        if (isEating || Time.time < nextAllowedEatingTime)
        {
            Debug.Log("Panda is still eating. Please wait.");
            return;
        }

        if (pandaSystem == null || BambooManager.Instance == null)
        {
            Debug.LogWarning(
                "Cannot feed: Missing PandaSystem or BambooManager."
            );
            return;
        }

        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone == null)
        {
            Debug.Log(
                "Cannot feed: Panda is not inside an ecosystem zone."
            );
            return;
        }

        if (!BambooManager.Instance.ConsumeBamboo())
        {
            Debug.Log(
                "Cannot feed: No available bamboo in this zone."
            );
            return;
        }

        isEating = true;
        nextAllowedEatingTime = Time.time + eatingCooldown;

        pandaSystem.FeedPanda();

        if (pandaAnimator != null)
        {
            pandaAnimator.ResetTrigger(eatTrigger);
            pandaAnimator.SetTrigger(eatTrigger);
        }
        else
        {
            Debug.LogWarning(
                "PandaFeedingZone: Panda Animator is not assigned."
            );
        }

        BambooManager.Instance.PandaPooped();

        eatingCoroutine = StartCoroutine(EatingSequence());
    }

    private IEnumerator EatingSequence()
    {
        // Spawn the bamboo on the ground first.
        if (eatingBambooPrefab != null)
        {
            Vector3 groundPosition;
            Quaternion groundRotation;

            if (bambooGroundPoint != null)
            {
                groundPosition = bambooGroundPoint.position;
                groundRotation = bambooGroundPoint.rotation;
            }
            else
            {
                Transform pandaTransform = pandaAnimator != null
                    ? pandaAnimator.transform
                    : transform;

                groundPosition =
                    pandaTransform.position + groundSpawnOffset;

                groundRotation = Quaternion.identity;
            }

            spawnedBamboo = Instantiate(
                eatingBambooPrefab,
                groundPosition,
                groundRotation
            );

            // Preserve the prefab's original world scale while
            // it is on the ground.
            Vector3 originalScale =
                eatingBambooPrefab.transform.lossyScale;

            spawnedBamboo.transform.localScale = originalScale;

            // Move the bamboo from the ground to the paw.
            if (bambooSpawnPoint != null)
            {
                Vector3 startPosition = spawnedBamboo.transform.position;
                Quaternion startRotation = spawnedBamboo.transform.rotation;

                float duration = Mathf.Max(0.01f, bambooMoveDuration);
                float elapsed = 0f;

                while (elapsed < duration && spawnedBamboo != null)
                {
                    elapsed += Time.deltaTime;

                    float t = Mathf.Clamp01(elapsed / duration);
                    // Smooth start and stop.
                    float smoothT = t * t * (3f - 2f * t);

                    spawnedBamboo.transform.position = Vector3.Lerp(
                        startPosition,
                        bambooSpawnPoint.position,
                        smoothT
                    );

                    spawnedBamboo.transform.rotation = Quaternion.Slerp(
                        startRotation,
                        bambooSpawnPoint.rotation,
                        smoothT
                    );

                    yield return null;
                }

                if (spawnedBamboo != null)
                {
                    // Attach it to the paw after the movement.
                    spawnedBamboo.transform.SetParent(
                        bambooSpawnPoint,
                        true
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "PandaFeedingZone: Bamboo Spawn Point is not assigned. " +
                    "Bamboo will remain on the ground."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "PandaFeedingZone: Eating Bamboo Prefab is not assigned."
            );
        }

        // Keep the bamboo visible for the configured total duration.
        float remainingVisibleTime = Mathf.Max(
            0f,
            bambooVisibleDuration - Mathf.Max(0f, bambooMoveDuration)
        );

        yield return new WaitForSeconds(remainingVisibleTime);

        if (spawnedBamboo != null)
        {
            Destroy(spawnedBamboo);
            spawnedBamboo = null;
        }

        // Keep the feeding lock active until the cooldown finishes.
        float remainingCooldown = nextAllowedEatingTime - Time.time;

        if (remainingCooldown > 0f)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        isEating = false;
        eatingCoroutine = null;

        Debug.Log("PandaFeedingZone: Panda can eat again.");
    }

    private void OnDisable()
    {
        if (spawnedBamboo != null)
        {
            Destroy(spawnedBamboo);
            spawnedBamboo = null;
        }

        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }

        isEating = false;
    }
}