
using System.Collections;
using UnityEngine;

public class OtherPandaBambooConsumer : MonoBehaviour
{
    // =========================================================
    // MIDWAY ZONE
    // =========================================================

    [Header("Midway Zone")]
    [SerializeField] private EcosystemZone midwayZone;

    // =========================================================
    // EATING SETTINGS
    // =========================================================

    [Header("Eating Settings")]
    [Min(0.5f)]
    [SerializeField] private float secondsBetweenMeals = 5f;

    [Min(1)]
    [SerializeField] private int bambooPerMeal = 2;

    // =========================================================
    // EATING ANIMATION
    // =========================================================

    [Header("Eating Animation")]
    [SerializeField] private Animator pandaAnimator;

    [SerializeField] private string eatTrigger = "Eat";

    // =========================================================
    // BAMBOO VISUAL
    // =========================================================

    [Header("Eating Bamboo Visual")]
    [SerializeField] private GameObject eatingBambooPrefab;

    [Tooltip("Transform attached to the moving paw bone.")]
    [SerializeField] private Transform bambooSpawnPoint;

    [Tooltip("Transform on the ground where bamboo first appears.")]
    [SerializeField] private Transform bambooGroundPoint;

    [Tooltip("Used if no ground point is assigned.")]
    [SerializeField] private Vector3 groundSpawnOffset =
        new Vector3(0f, 0.05f, 0.8f);

    [Tooltip("Time taken for bamboo to move from the ground to the paw.")]
    [Min(0f)]
    [SerializeField] private float bambooMoveDuration = 0.6f;

    [Tooltip("Total time the bamboo remains visible.")]
    [Min(0f)]
    [SerializeField] private float bambooVisibleDuration = 2f;

    // =========================================================
    // BAMBOO REGROWTH
    // =========================================================

    [Header("Bamboo Regrowth")]
    [Min(1)]
    [SerializeField] private int mealsBeforePooping = 3;

    // =========================================================
    // PRIVATE VARIABLES
    // =========================================================

    private float mealTimer;
    private int successfulMeals;

    private bool isEating;
    private GameObject spawnedBamboo;
    private Coroutine eatingCoroutine;

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (midwayZone == null ||
            EnvironmentSystem.Instance == null)
        {
            return;
        }

        // Only eat while the player's panda is in Midway.
        if (EnvironmentSystem.Instance.ActiveZone != midwayZone)
        {
            mealTimer = 0f;
            return;
        }

        // Do not start another meal while the eating sequence runs.
        if (isEating)
            return;

        mealTimer += Time.deltaTime;

        if (mealTimer < secondsBetweenMeals)
            return;

        mealTimer = 0f;

        EatBamboo();
    }

    // =========================================================
    // EAT BAMBOO
    // =========================================================

    private void EatBamboo()
    {
        if (BambooManager.Instance == null)
            return;

        int eaten = 0;

        // Consume bamboo from the Midway zone.
        for (int i = 0; i < bambooPerMeal; i++)
        {
            if (!BambooManager.Instance.ConsumeBambooInZone(midwayZone))
                break;

            eaten++;
        }

        // Do nothing if no bamboo was available.
        if (eaten == 0)
            return;

        Debug.Log("Other panda ate " + eaten + " bamboo.");

        successfulMeals++;

        // Trigger the eating animation and bamboo visual.
        isEating = true;

        if (pandaAnimator != null)
        {
            pandaAnimator.ResetTrigger(eatTrigger);
            pandaAnimator.SetTrigger(eatTrigger);
        }
        else
        {
            Debug.LogWarning(
                "OtherPandaBambooConsumer: Panda Animator is not assigned.",
                this
            );
        }

        if (eatingBambooPrefab == null)
        {
            Debug.LogWarning(
                "OtherPandaBambooConsumer: Eating Bamboo Prefab is not assigned.",
                this
            );

            isEating = false;
        }
        else
        {
            eatingCoroutine = StartCoroutine(EatingSequence(eaten));
        }

        // Regrow bamboo after the configured number of successful meals.
        if (successfulMeals >= mealsBeforePooping)
        {
            bool grewBamboo =
                BambooManager.Instance.OtherPandaPooped(midwayZone);

            if (grewBamboo)
            {
                successfulMeals = 0;

                Debug.Log("Other panda helped regrow bamboo!");
            }
            else
            {
                Debug.Log(
                    "Other panda cannot grow bamboo: no free spot."
                );
            }
        }
    }

    // =========================================================
    // EATING SEQUENCE
    // =========================================================

    private IEnumerator EatingSequence(int bambooEaten)
    {
        for (int i = 0; i < bambooEaten; i++)
        {
            // Spawn one bamboo visual on the ground.
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

            // Preserve the prefab's original world scale.
            spawnedBamboo.transform.localScale =
                eatingBambooPrefab.transform.lossyScale;

            // Move the bamboo from the ground to the paw.
            if (bambooSpawnPoint != null)
            {
                Vector3 startPosition =
                    spawnedBamboo.transform.position;

                Quaternion startRotation =
                    spawnedBamboo.transform.rotation;

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
                    spawnedBamboo.transform.SetParent(
                        bambooSpawnPoint,
                        true
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "OtherPandaBambooConsumer: Bamboo Spawn Point is not assigned. " +
                    "Bamboo will remain on the ground.",
                    this
                );
            }

            // Keep the bamboo visible before removing it.
            float remainingVisibleTime = Mathf.Max(
                0f,
                bambooVisibleDuration - bambooMoveDuration
            );

            if (remainingVisibleTime > 0f)
                yield return new WaitForSeconds(remainingVisibleTime);

            if (spawnedBamboo != null)
            {
                Destroy(spawnedBamboo);
                spawnedBamboo = null;
            }
        }

        isEating = false;
        eatingCoroutine = null;
    }

    // =========================================================
    // CLEANUP
    // =========================================================

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
