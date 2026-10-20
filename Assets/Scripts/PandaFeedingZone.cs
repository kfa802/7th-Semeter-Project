using System.Collections;
using UnityEngine;

public class PandaFeedingZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pandaAnimator;
    [SerializeField] private PandaSystem pandaSystem;

    [Tooltip("Feeding is blocked while the panda is travelling.")]
    [SerializeField] private PandaPathMover pathMover;

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

    [Tooltip("Name of the eating state in the Animator (or give the state the tag 'Eat').")]
    [SerializeField] private string eatStateName = "Eat";

    [SerializeField] private float eatingCooldown = 3f;

    private bool eatingAnimationPhase;
    private bool isEating;
    private float nextAllowedEatingTime;
    private GameObject spawnedBamboo;
    private Coroutine eatingCoroutine;

    public bool IsEating => isEating;

    /// <summary>
    /// True when FeedPanda() would succeed right now
    /// (same checks, but nothing is consumed).
    /// </summary>
    public bool CanFeed
    {
        get
        {
            if (pathMover != null &&
                (pathMover.IsTravelling || pathMover.HasPendingTravel))
                return false;

            if (isEating || Time.time < nextAllowedEatingTime)
                return false;

            if (pandaSystem == null || BambooManager.Instance == null)
                return false;

            if (EnvironmentSystem.Instance == null ||
                EnvironmentSystem.Instance.ActiveZone == null)
                return false;

            return BambooManager.Instance.HasAvailableBambooInCurrentZone();
        }
    }

    /// <summary>
    /// True while the panda is visibly eating: from the moment it is fed
    /// until the bamboo is gone AND the Animator has left the eat state.
    /// (The feeding cooldown afterwards does not count.)
    /// </summary>
    public bool IsEatingAnimationPlaying
    {
        get
        {
            if (eatingAnimationPhase)
                return true;

            if (pandaAnimator == null)
                return false;

            if (IsEatState(pandaAnimator.GetCurrentAnimatorStateInfo(0)))
                return true;

            if (pandaAnimator.IsInTransition(0) &&
                IsEatState(pandaAnimator.GetNextAnimatorStateInfo(0)))
                return true;

            return false;
        }
    }

    private bool IsEatState(AnimatorStateInfo state)
    {
        return state.IsTag("Eat") ||
               (!string.IsNullOrEmpty(eatStateName) &&
                state.IsName(eatStateName));
    }

    public void FeedPanda()
    {
        // Don't start eating while the panda is walking to a zone
        // or the camera is still returning.
        if (pathMover != null &&
            (pathMover.IsTravelling || pathMover.HasPendingTravel))
        {
            Debug.Log("Cannot feed: Panda is travelling.");
            return;
        }

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
        eatingAnimationPhase = true;
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

    /// <summary>
    /// Stops eating immediately: clears the Eat trigger, stops the
    /// bamboo sequence and removes the bamboo. Called when travel starts.
    /// </summary>
    public void CancelEating()
    {
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }

        if (spawnedBamboo != null)
        {
            Destroy(spawnedBamboo);
            spawnedBamboo = null;
        }

        if (pandaAnimator != null && !string.IsNullOrEmpty(eatTrigger))
            pandaAnimator.ResetTrigger(eatTrigger);

        if (isEating)
            Debug.Log("PandaFeedingZone: Eating cancelled.");

        isEating = false;
        eatingAnimationPhase = false;
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

        // The visible eating is over (the Animator check in
        // IsEatingAnimationPlaying covers any remaining eat animation).
        eatingAnimationPhase = false;

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
        CancelEating();
    }
}