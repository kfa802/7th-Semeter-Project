using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BambooGrowthSpot : MonoBehaviour
{
    // =========================================================
    // POOP
    // =========================================================

    [Header("Poop")]
    [SerializeField] private Transform poopPoint;
    [SerializeField] private GameObject poopPrefab;


    // =========================================================
    // BAMBOO
    // =========================================================

    [Header("Bamboo")]
    [SerializeField] private GameObject bambooPrefab;
    [SerializeField] private float minSize = 1.5f;
    [SerializeField] private float maxSize = 2.5f;


    // =========================================================
    // TIMING
    // =========================================================

    [Header("Timing")]
    [SerializeField] private float poopToBambooDelay = 5f;
    [SerializeField] private float growTime = 10f;

    [Tooltip("Random variation of the grow time, so bamboo don't grow " +
             "identically. 0.15 = up to 15% faster or slower.")]
    [SerializeField, Range(0f, 0.5f)]
    private float growTimeVariation = 0.15f;


    // =========================================================
    // STARTING BAMBOO
    // =========================================================

    [Header("Bamboo Already Here At The Start")]
    [SerializeField] private GameObject existingBamboo;
    [SerializeField] private float autoDetectRadius = 1.5f;


    // =========================================================
    // ECOSYSTEM ZONE
    // =========================================================

    [Header("Ecosystem Zone")]
    [SerializeField] private EcosystemZone ecosystemZone;

    public EcosystemZone Zone => ecosystemZone;

    public GameObject ExistingBamboo => existingBamboo;


    // =========================================================
    // STATE
    // =========================================================

    private bool occupied;
    private bool isGrowing;

    private Coroutine growthCoroutine;

    private static readonly HashSet<GameObject> claimed =
        new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claimed.Clear();
    }


    // =========================================================
    // AVAILABILITY
    // =========================================================

    public bool IsAvailable
    {
        get
        {
            // Do not release a spot while bamboo is growing.
            if (occupied &&
                existingBamboo == null &&
                !isGrowing)
            {
                occupied = false;
            }

            return !occupied && !isGrowing;
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Find starting bamboo if none was assigned manually.
        if (existingBamboo == null &&
            autoDetectRadius > 0f)
        {
            existingBamboo = FindNearbyBamboo();
        }

        // Register starting bamboo with this specific spot.
        if (existingBamboo != null)
        {
            occupied = true;
            claimed.Add(existingBamboo);

            if (BambooManager.Instance != null)
            {
                BambooManager.Instance.RegisterBamboo(
                    existingBamboo,
                    this
                );
            }
        }

        // Warn if this spot has no zone.
        if (ecosystemZone == null)
        {
            Debug.LogWarning(
                "BambooGrowthSpot '" + gameObject.name +
                "' has no Ecosystem Zone assigned.",
                this
            );
        }
    }


    // =========================================================
    // FIND STARTING BAMBOO
    // =========================================================

    private GameObject FindNearbyBamboo()
    {
        List<GameObject> candidates =
            new List<GameObject>();

        // Starting bamboo explicitly listed in BambooManager.
        if (BambooManager.Instance != null)
        {
            foreach (
                GameObject bamboo
                in BambooManager.Instance.StartingBamboo)
            {
                if (bamboo != null &&
                    !candidates.Contains(bamboo))
                {
                    candidates.Add(bamboo);
                }
            }
        }

        // BambooPlant objects already in the scene.
        BambooPlant[] plants =
            FindObjectsOfType<BambooPlant>();

        foreach (BambooPlant plant in plants)
        {
            if (plant == null)
                continue;

            GameObject bamboo = plant.gameObject;

            if (!candidates.Contains(bamboo))
            {
                candidates.Add(bamboo);
            }
        }

        GameObject best = null;

        float bestDistance = autoDetectRadius;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
                continue;

            // Do not allow two spots to claim the same plant.
            if (claimed.Contains(candidate))
                continue;

            float distance = Vector3.Distance(
                candidate.transform.position,
                transform.position
            );

            if (distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }


    // =========================================================
    // GROW BAMBOO
    // =========================================================

    /// <param name="extraDelay">
    /// Extra seconds added to the poop-to-bamboo delay, used to
    /// stagger several bamboo from the same poop.
    /// </param>
    public void GrowBamboo(float extraDelay = 0f)
    {
        if (!IsAvailable)
            return;

        // Every spot must belong to a zone.
        if (ecosystemZone == null)
        {
            Debug.LogWarning(
                "Cannot grow bamboo: no zone assigned to " +
                gameObject.name,
                this
            );

            return;
        }

        // Check the zone's maximum bamboo capacity.
        if (BambooManager.Instance != null &&
            !BambooManager.Instance.CanGrowInZone(
                ecosystemZone))
        {
            Debug.Log(
                "Maximum bamboo reached in " +
                ecosystemZone.zoneName
            );

            return;
        }

        // Reserve the spot immediately.
        occupied = true;
        isGrowing = true;

        growthCoroutine = StartCoroutine(
            GrowthSequence(Mathf.Max(0f, extraDelay))
        );
    }


    // =========================================================
    // FREE SPOT
    // =========================================================

    public void Free()
    {
        if (existingBamboo != null)
        {
            claimed.Remove(existingBamboo);
        }

        existingBamboo = null;
        occupied = false;

        // Do not cancel a growth coroutine here.
        // Free() is also called when bamboo is removed.
    }


    // =========================================================
    // GROWTH SEQUENCE
    // =========================================================

    private IEnumerator GrowthSequence(float extraDelay)
    {
        GameObject poop = null;

        if (poopPrefab != null &&
            poopPoint != null)
        {
            poop = Instantiate(
                poopPrefab,
                poopPoint.position,
                poopPoint.rotation
            );
        }

        // Wait before creating bamboo (plus any stagger delay).
        float totalDelay = poopToBambooDelay + extraDelay;
        float waited = 0f;

        while (waited < totalDelay)
        {
            float waterMultiplier =
                BambooManager.Instance != null
                    ? BambooManager.Instance.GetWaterGrowthMultiplier()
                    : 0f;

            float temperatureMultiplier =
                EnvironmentSystem.Instance != null
                    ? EnvironmentSystem.Instance.TemperatureGrowthFactor
                    : 1f;

            // Only progress when water is available.
            if (waterMultiplier > 0f)
            {
                waited += Time.deltaTime
                          * temperatureMultiplier
                          * waterMultiplier;
            }

            yield return null;
        }

        if (poop != null)
        {
            Destroy(poop);
        }

        // Safety check.
        if (bambooPrefab == null)
        {
            occupied = false;
            isGrowing = false;
            growthCoroutine = null;
            yield break;
        }

        // Create the bamboo holder.
        float size = Random.Range(minSize, maxSize);

        GameObject holder = new GameObject("BambooHolder");

        holder.transform.SetPositionAndRotation(
            transform.position,
            bambooPrefab.transform.rotation
        );

        holder.transform.localScale = Vector3.one * size;

        // Create the visible bamboo as a child.
        GameObject bamboo = Instantiate(
            bambooPrefab,
            holder.transform
        );

        bamboo.transform.localPosition = Vector3.zero;
        bamboo.transform.localRotation = Quaternion.identity;

        // Slightly different grow time for every bamboo.
        float variedGrowTime = growTime * Random.Range(
            1f - growTimeVariation,
            1f + growTimeVariation
        );

        // BambooManager tracks the holder.
        BambooPlant plant = holder.AddComponent<BambooPlant>();
        plant.Init(variedGrowTime);

        // Growing sound partway through (only when growth isn't slow),
        // and an optional sound when it has finished growing.
        if (BambooManager.Instance != null)
            StartCoroutine(GrowthSoundRoutine(plant, variedGrowTime));

        // Assign the new bamboo to this spot.
        existingBamboo = holder;

        occupied = true;
        isGrowing = false;
        growthCoroutine = null;

        claimed.Add(holder);

        if (BambooManager.Instance != null)
        {
            BambooManager.Instance.RegisterBamboo(
                holder,
                this
            );

            BambooManager.Instance.RefreshZoneStatistics();
        }
    }


    private IEnumerator GrowthSoundRoutine(BambooPlant plant, float plantGrowTime)
    {
        BambooManager manager = BambooManager.Instance;

        bool soundStarted = false;
        bool loggedSlow = false;
        bool sawGrowState = false;
        float furthestAnimation = 0f;
        AudioSource growingSource = null;

        // Wait one frame so BambooPlant has started its Grow animation.
        yield return null;

        Animator animator = plant != null
            ? plant.GetComponentInChildren<Animator>(true)
            : null;

        bool hasAnimator =
            animator != null && animator.runtimeAnimatorController != null;

        if (manager.DebugSounds)
        {
            Debug.Log(
                "BambooSound: '" + name + "' appeared. " +
                (hasAnimator ? "Following its '" + manager.GrowStateName + "' animation."
                             : "No Animator found: using the growth speed only."),
                this);
        }

        while (plant != null && !plant.IsFullyGrown)
        {
            // How fast it grows right now (BambooPlant also sets the
            // animation speed to this value).
            float speed = EnvironmentSystem.Instance != null
                ? Mathf.Max(0f, EnvironmentSystem.Instance.TemperatureGrowthFactor)
                : 1f;

            bool fastEnough = speed >= manager.MinGrowthSpeedForSound;

            // Is the grow animation playing, and how far is it?
            bool inGrowAnimation;
            float animationProgress;

            if (hasAnimator)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

                inGrowAnimation =
                    state.IsName(manager.GrowStateName) && state.normalizedTime < 1f;

                animationProgress = state.IsName(manager.GrowStateName)
                    ? state.normalizedTime
                    : (sawGrowState ? 1f : 0f);

                if (inGrowAnimation)
                {
                    sawGrowState = true;
                    furthestAnimation = Mathf.Max(furthestAnimation, animationProgress);
                }
            }
            else
            {
                inGrowAnimation = true;
                animationProgress = 0f;
            }

            // Start: the grow animation is playing at a good speed,
            // and we're not too far into it.
            if (!soundStarted &&
                inGrowAnimation &&
                fastEnough &&
                animationProgress >= manager.GrowSoundStartInAnimation &&
                animationProgress <= manager.GrowSoundLatestStartInAnimation)
            {
                soundStarted = true;

                if (manager.DebugSounds)
                {
                    Debug.Log(
                        "BambooSound: '" + name + "' grow animation at " +
                        (animationProgress * 100f).ToString("F0") + "% -> starting growing sound.",
                        this);
                }

                growingSource = manager.PlayBambooGrowingSound(
                    plant.transform.position,
                    ecosystemZone
                );
            }

            if (manager.DebugSounds && !soundStarted && !loggedSlow &&
                inGrowAnimation && !fastEnough)
            {
                loggedSlow = true;
                Debug.Log(
                    "BambooSound: '" + name + "' grows too slowly for the sound " +
                    "(speed " + speed.ToString("F2") + ", needs " +
                    manager.MinGrowthSpeedForSound.ToString("F2") + ").", this);
            }

            // Stop: growth slowed down, or the grow animation has ended.
            if (growingSource != null && growingSource.isPlaying)
            {
                bool slowedDown = manager.StopWhenGrowthSlows && !fastEnough;
                bool animationEnded = manager.StopWhenGrowAnimationEnds &&
                                      hasAnimator && sawGrowState && !inGrowAnimation;

                if (slowedDown || animationEnded)
                {
                    StartCoroutine(FadeOutSound(growingSource, manager.StopFadeDuration));
                    growingSource = null;
                }
            }

            yield return null;
        }

        if (manager.DebugSounds && !soundStarted)
        {
            Debug.Log(
                "BambooSound: '" + name + "' finished growing without a growing sound. " +
                "The grow animation got to " + (furthestAnimation * 100f).ToString("F0") +
                "% (sound starts at " + (manager.GrowSoundStartInAnimation * 100f).ToString("F0") +
                "%). If it never reaches that, the animation is cut short: lower " +
                "'Grow Sound Start In Animation' or raise 'Grow Time' on the growth spots.",
                this);
        }

        if (plant != null && BambooManager.Instance != null)
        {
            BambooManager.Instance.PlayBambooGrownSound(
                plant.transform.position,
                ecosystemZone
            );
        }
    }

    private IEnumerator FadeOutSound(AudioSource source, float duration)
    {
        float startVolume = source != null ? source.volume : 0f;
        float elapsed = 0f;

        while (source != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        if (source != null)
            source.Stop();
    }


    // =========================================================
    // DEBUG GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (autoDetectRadius <= 0f)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            transform.position,
            autoDetectRadius
        );
    }
}