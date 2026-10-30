using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

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
    // SOUNDS
    // =========================================================

    [Header("Eating Sound")]
    [Tooltip("Chewing sounds. One is picked at random for each bamboo eaten.")]
    [SerializeField] private AudioClip[] eatingSounds;

    [Tooltip("Seconds after the bamboo reaches the paw before the sound starts.")]
    [SerializeField, Min(0f)] private float eatingSoundDelay = 0f;

    [SerializeField, Range(0f, 1f)] private float eatingSoundVolume = 0.7f;

    [Header("Poop Sound")]
    [Tooltip("Sounds for when the other panda poops. One is picked at random.")]
    [SerializeField] private AudioClip[] poopSounds;

    [Tooltip("Chance (0-1) that a poop makes a sound. 1 = every time, 0.3 = about 1 in 3.")]
    [SerializeField, Range(0f, 1f)] private float poopSoundChance = 1f;

    [Tooltip("Seconds after the meal starts before the poop sound plays.")]
    [SerializeField, Min(0f)] private float poopSoundDelay = 1f;

    [SerializeField, Range(0f, 1f)] private float poopSoundVolume = 0.7f;

    [Header("Sound Settings (both)")]
    [Tooltip("Random pitch change so repeated sounds don't sound identical. 0.08 = ±8%.")]
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.08f;

    [Tooltip("0 = same volume everywhere (2D). 1 = fully 3D (quieter further from the camera).")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.5f;

    [Tooltip("Optional: send the sounds through a group in your Audio Mixer.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    private AudioSource eatingAudio;
    private AudioSource poopAudio;

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

                // Only sometimes, depending on the chance setting.
                if (Random.value < poopSoundChance)
                {
                    PlaySound(
                        ref poopAudio,
                        "OtherPandaPoopSound",
                        poopSounds,
                        poopSoundVolume,
                        poopSoundDelay
                    );
                }

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

            // The bamboo is at the paw: start the chewing sound.
            PlaySound(
                ref eatingAudio,
                "OtherPandaEatingSound",
                eatingSounds,
                eatingSoundVolume,
                eatingSoundDelay
            );

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
    // SOUND
    // =========================================================

    private void PlaySound(
        ref AudioSource source,
        string objectName,
        AudioClip[] clips,
        float volume,
        float delay)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null)
            return;

        if (source == null)
        {
            // A sound object on the other panda, so the sound comes from it.
            Transform pandaTransform = pandaAnimator != null
                ? pandaAnimator.transform
                : transform;

            GameObject soundObject = new GameObject(objectName);
            soundObject.transform.SetParent(pandaTransform, false);

            source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
        }

        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.spatialBlend = spatialBlend;
        source.outputAudioMixerGroup = mixerGroup;

        if (delay > 0f)
            source.PlayDelayed(delay);
        else
            source.Play();
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (eatingAudio != null)
            eatingAudio.Stop();

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