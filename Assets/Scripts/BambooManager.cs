using System.Collections.Generic;
using UnityEngine;

public class BambooManager : MonoBehaviour
{
    public static BambooManager Instance { get; private set; }

    // =========================================================
    // GROWTH SPOTS
    // =========================================================

    [Header("Possible Bamboo")]
    [Tooltip("Every growth spot represents one possible bamboo.")]
    [SerializeField]
    private List<BambooGrowthSpot> growthSpots =
        new List<BambooGrowthSpot>();

    [SerializeField]
    private bool autoFindSpots = true;


    // =========================================================
    // STARTING BAMBOO
    // =========================================================

    [Header("Starting Bamboo")]
    [Tooltip("Bamboo that already exists in the scene.")]
    [SerializeField]
    private bool autoFindBamboo = true;

    [SerializeField]
    private string bambooTag = "";

    [SerializeField]
    private List<GameObject> startingBamboo =
        new List<GameObject>();


    // =========================================================
    // NATURAL BAMBOO GROWTH
    // =========================================================

    [Header("Natural Bamboo Growth")]
    [SerializeField, Range(0f, 100f)]
    private float naturalGrowthWaterThreshold = 80f;

    [SerializeField]
    private float minimumNaturalGrowthInterval = 10f;

    [SerializeField]
    private float maximumNaturalGrowthInterval = 20f;

    private float naturalGrowthTimer;
    private float naturalGrowthTargetTime;


    // =========================================================
    // POLLUTION
    // =========================================================

    [Header("Pollution")]
    [SerializeField]
    private EnvironmentSystem environment;

    [SerializeField, Range(0f, 1f)]
    private float rotStartsAbovePollution = 0.3f;

    [SerializeField]
    private float slowestRotInterval = 20f;

    [SerializeField]
    private float fastestRotInterval = 4f;


    // =========================================================
    // ROTTING
    // =========================================================

    [Header("Rotting")]
    [SerializeField]
    private float rotDuration = 8f;

    [SerializeField]
    private float shrinkDuration = 1.5f;

    [SerializeField]
    private Color rottenColor =
        new Color(0.35f, 0.25f, 0.1f);


    // =========================================================
    // ROT STATE
    // =========================================================

    private class RotState
    {
        public GameObject bamboo;

        public float timer;

        public bool shrinking;

        public Vector3 startScale;

        public Renderer[] renderers;

        public int[] colorIds;

        public Color[] originalColors;
    }


    // =========================================================
    // DATA
    // =========================================================

    private readonly List<GameObject> activeBamboo =
        new List<GameObject>();

    private readonly Dictionary<GameObject, BambooGrowthSpot> spotOf =
        new Dictionary<GameObject, BambooGrowthSpot>();

    private readonly List<KeyValuePair<GameObject, BambooGrowthSpot>> keptSpots =
        new List<KeyValuePair<GameObject, BambooGrowthSpot>>();

    private readonly List<RotState> rotting =
        new List<RotState>();

    private readonly HashSet<GameObject> rottingSet =
        new HashSet<GameObject>();


    private MaterialPropertyBlock block;

    private float rotTimer;


    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");


    // =========================================================
    // COUNTS
    // =========================================================

    public int TotalSpawned { get; private set; }

    public int TotalRemoved { get; private set; }

    public int TotalRotted { get; private set; }


    public IReadOnlyList<BambooGrowthSpot> GrowthSpots =>
        growthSpots;


    public IReadOnlyList<GameObject> StartingBamboo =>
        startingBamboo;


    public IReadOnlyList<GameObject> ActiveBamboo
    {
        get
        {
            Prune();

            return activeBamboo;
        }
    }


    // =========================================================
    // CAPACITY
    // =========================================================

    public int Capacity
    {
        get
        {
            return growthSpots.Count;
        }
    }


    // =========================================================
    // ACTIVE BAMBOO COUNT
    // =========================================================

    public int ActiveCount
    {
        get
        {
            Prune();

            return activeBamboo.Count;
        }
    }


    // =========================================================
    // ROTTING COUNT
    // =========================================================

    public int RottingCount
    {
        get
        {
            return rotting.Count;
        }
    }


    // =========================================================
    // HEALTHY BAMBOO
    // =========================================================

    public int HealthyCount
    {
        get
        {
            return Mathf.Max(
                0,
                ActiveCount - rotting.Count
            );
        }
    }


    // =========================================================
    // BAMBOO PERCENTAGE
    // =========================================================

    public float Percent
    {
        get
        {
            if (Capacity <= 0)
                return 0f;

            return Mathf.Clamp01(
                (float)HealthyCount / Capacity
            ) * 100f;
        }
    }


    // =========================================================
    // POLLUTION
    // =========================================================

    public float Pollution
    {
        get
        {
            if (environment == null)
                return 0f;

            return environment.Pollution;
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;

        block =
            new MaterialPropertyBlock();


        // Remove null growth spots
        growthSpots.RemoveAll(
            spot => spot == null
        );


        // Automatically find all growth spots
        if (autoFindSpots &&
            growthSpots.Count == 0)
        {
            growthSpots.AddRange(
                FindObjectsOfType<BambooGrowthSpot>()
            );
        }


        // Register bamboo explicitly assigned
        // in the Inspector.
        foreach (GameObject bamboo in startingBamboo)
        {
            RegisterBamboo(bamboo);
        }
    }


    private void Start()
    {
        // Find bamboo that already exists in the scene.
        ScanForBamboo();

        // Start the natural growth timer.
        SetNaturalGrowthTimer();
    }


    private void Update()
    {
        UpdateRotting();

        UpdatePollutionRotTimer();

        UpdateNaturalBambooGrowth();
    }


    // =========================================================
    // PANDA POOP
    // =========================================================

    public void PandaPooped()
    {
        List<BambooGrowthSpot> availableSpots =
            GetFreeSpots();


        if (availableSpots.Count == 0)
        {
            return;
        }


        BambooGrowthSpot selectedSpot =
            availableSpots[
                Random.Range(
                    0,
                    availableSpots.Count
                )
            ];


        selectedSpot.GrowBamboo();
    }


    // =========================================================
    // NATURAL BAMBOO GROWTH
    // =========================================================

    private void UpdateNaturalBambooGrowth()
    {
        if (environment == null)
            return;


        // Natural bamboo growth only happens
        // when the water supply is high enough.
        if (environment.Water < naturalGrowthWaterThreshold)
        {
            naturalGrowthTimer = 0f;
            return;
        }


        naturalGrowthTimer += Time.deltaTime;


        if (naturalGrowthTimer >= naturalGrowthTargetTime)
        {
            naturalGrowthTimer = 0f;


            List<BambooGrowthSpot> availableSpots =
                GetFreeSpots();


            if (availableSpots.Count > 0)
            {
                BambooGrowthSpot selectedSpot =
                    availableSpots[
                        Random.Range(
                            0,
                            availableSpots.Count
                        )
                    ];


                selectedSpot.GrowBamboo();
            }


            // Choose a new random interval.
            SetNaturalGrowthTimer();
        }
    }


    // =========================================================
    // REGISTER BAMBOO
    // =========================================================

    public void RegisterBamboo(
        GameObject bamboo,
        BambooGrowthSpot spot = null
    )
    {
        if (bamboo == null)
            return;


        // If this bamboo belongs to a growth spot,
        // remember the connection.
        if (spot != null)
        {
            spotOf[bamboo] = spot;
        }


        // Don't register the same bamboo twice.
        if (activeBamboo.Contains(bamboo))
            return;


        activeBamboo.Add(bamboo);

        TotalSpawned++;
    }


    // =========================================================
    // FIND BAMBOO ALREADY IN SCENE
    // =========================================================

    private void ScanForBamboo()
    {
        if (autoFindBamboo)
        {
            BambooPlant[] plants =
                FindObjectsOfType<BambooPlant>();


            foreach (BambooPlant plant in plants)
            {
                if (plant == null)
                    continue;


                GameObject bamboo =
                    plant.gameObject;


                if (rottingSet.Contains(bamboo))
                    continue;


                RegisterBamboo(bamboo);
            }
        }


        // Optional tag-based detection.
        if (!string.IsNullOrEmpty(bambooTag))
        {
            try
            {
                GameObject[] taggedBamboo =
                    GameObject.FindGameObjectsWithTag(
                        bambooTag
                    );


                foreach (GameObject bamboo in taggedBamboo)
                {
                    if (bamboo == null)
                        continue;


                    if (rottingSet.Contains(bamboo))
                        continue;


                    RegisterBamboo(bamboo);
                }
            }
            catch (UnityException)
            {
                // Invalid tag.
                bambooTag = "";
            }
        }
    }


    // =========================================================
    // CONSUME BAMBOO
    // =========================================================

    public bool ConsumeBamboo()
    {
        Prune();


        // Find one healthy bamboo that can be eaten.
        for (int i = 0;
             i < activeBamboo.Count;
             i++)
        {
            GameObject bamboo =
                activeBamboo[i];


            if (bamboo == null)
                continue;


            // Do not eat bamboo that is currently rotting.
            if (rottingSet.Contains(bamboo))
                continue;


            // RemoveBamboo handles:
            // - removing it from activeBamboo
            // - freeing its growth spot
            // - destroying the visual bamboo
            // - updating TotalRemoved
            RemoveBamboo(bamboo);


            Debug.Log(
                "BAMBOO CONSUMED BY PANDA."
            );


            return true;
        }


        Debug.Log(
            "PANDA TRIED TO EAT, BUT NO HEALTHY BAMBOO IS AVAILABLE."
        );


        return false;
    }


    // =========================================================
    // REMOVE BAMBOO
    // =========================================================

    public void RemoveBamboo(
        GameObject bamboo
    )
    {
        if (bamboo == null)
            return;


        // Remove from rotting list if necessary.
        for (int i = rotting.Count - 1;
             i >= 0;
             i--)
        {
            if (rotting[i].bamboo == bamboo)
            {
                rotting.RemoveAt(i);
            }
        }


        rottingSet.Remove(bamboo);


        // Free its growth spot and forget it.
        Forget(bamboo);


        Destroy(bamboo);
    }


    // =========================================================
    // FORGET BAMBOO
    // =========================================================

    private void Forget(
        GameObject bamboo
    )
    {
        if (bamboo == null)
            return;


        if (activeBamboo.Remove(bamboo))
        {
            TotalRemoved++;
        }


        if (spotOf.TryGetValue(
            bamboo,
            out BambooGrowthSpot spot
        ))
        {
            if (spot != null)
            {
                spot.Free();
            }


            spotOf.Remove(bamboo);
        }
    }


    // =========================================================
    // CLEAN UP DESTROYED BAMBOO
    // =========================================================

    private void Prune()
    {
        // Remove destroyed bamboo from active list.
        for (int i = activeBamboo.Count - 1;
             i >= 0;
             i--)
        {
            if (activeBamboo[i] != null)
                continue;


            activeBamboo.RemoveAt(i);

            TotalRemoved++;
        }


        if (spotOf.Count == 0)
            return;


        bool anyGone = false;

        keptSpots.Clear();


        foreach (
            KeyValuePair<GameObject, BambooGrowthSpot> pair
            in spotOf
        )
        {
            if (pair.Key == null)
            {
                if (pair.Value != null)
                {
                    pair.Value.Free();
                }

                anyGone = true;
            }
            else
            {
                keptSpots.Add(pair);
            }
        }


        if (anyGone)
        {
            spotOf.Clear();


            foreach (
                KeyValuePair<GameObject, BambooGrowthSpot> pair
                in keptSpots
            )
            {
                spotOf.Add(
                    pair.Key,
                    pair.Value
                );
            }
        }
    }


    // =========================================================
    // FREE GROWTH SPOTS
    // =========================================================

    public List<BambooGrowthSpot> GetFreeSpots()
    {
        List<BambooGrowthSpot> free =
            new List<BambooGrowthSpot>();


        foreach (BambooGrowthSpot spot in growthSpots)
        {
            if (spot == null)
                continue;


            if (spot.IsAvailable)
            {
                free.Add(spot);
            }
        }


        return free;
    }


    // =========================================================
    // NATURAL GROWTH TIMER
    // =========================================================

    private void SetNaturalGrowthTimer()
    {
        naturalGrowthTargetTime =
            Random.Range(
                minimumNaturalGrowthInterval,
                maximumNaturalGrowthInterval
            );


        naturalGrowthTimer = 0f;
    }


    // =========================================================
    // POLLUTION -> ROTTING
    // =========================================================

    private void UpdatePollutionRotTimer()
    {
        float pollution =
            Pollution;


        float severity =
            Mathf.InverseLerp(
                rotStartsAbovePollution,
                1f,
                pollution
            );


        // Pollution is below the threshold.
        // No bamboo should start rotting.
        if (severity <= 0f)
        {
            rotTimer = 0f;
            return;
        }


        // Higher pollution = shorter interval.
        float interval =
            Mathf.Lerp(
                slowestRotInterval,
                fastestRotInterval,
                severity
            );


        rotTimer +=
            Time.deltaTime;


        if (rotTimer >= interval)
        {
            rotTimer = 0f;

            RotRandomBamboo();
        }
    }


    // =========================================================
    // START ROTTING
    // =========================================================

    [ContextMenu("Rot one bamboo")]
    public void RotRandomBamboo()
    {
        Prune();


        List<GameObject> healthy =
            new List<GameObject>();


        foreach (GameObject bamboo in activeBamboo)
        {
            if (bamboo == null)
                continue;


            if (rottingSet.Contains(bamboo))
                continue;


            healthy.Add(bamboo);
        }


        if (healthy.Count == 0)
            return;


        GameObject selected =
            healthy[
                Random.Range(
                    0,
                    healthy.Count
                )
            ];


        StartRotting(selected);
    }


    public void StartRotting(
        GameObject bamboo
    )
    {
        if (bamboo == null)
            return;


        if (rottingSet.Contains(bamboo))
            return;


        Renderer[] renderers =
            bamboo.GetComponentsInChildren<Renderer>(
                true
            );


        int[] ids =
            new int[renderers.Length];


        Color[] originals =
            new Color[renderers.Length];


        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Material material =
                renderers[i].sharedMaterial;


            ids[i] = -1;

            originals[i] =
                Color.white;


            if (material == null)
                continue;


            if (material.HasProperty(BaseColorId))
            {
                ids[i] =
                    BaseColorId;
            }
            else if (material.HasProperty(ColorId))
            {
                ids[i] =
                    ColorId;
            }


            if (ids[i] != -1)
            {
                originals[i] =
                    material.GetColor(
                        ids[i]
                    );
            }
        }


        rotting.Add(
            new RotState
            {
                bamboo = bamboo,

                timer = 0f,

                shrinking = false,

                startScale =
                    bamboo.transform.localScale,

                renderers = renderers,

                colorIds = ids,

                originalColors = originals
            }
        );


        rottingSet.Add(bamboo);
    }


    // =========================================================
    // ROTTING + SHRINKING
    // =========================================================

    private void UpdateRotting()
    {
        for (int i = rotting.Count - 1;
             i >= 0;
             i--)
        {
            RotState state =
                rotting[i];


            // Bamboo was destroyed externally.
            if (state.bamboo == null)
            {
                rotting.RemoveAt(i);

                continue;
            }


            state.timer +=
                Time.deltaTime;


            // -------------------------------------------------
            // PHASE 1 - TURN BROWN
            // -------------------------------------------------

            if (!state.shrinking)
            {
                float t =
                    Mathf.Clamp01(
                        state.timer /
                        rotDuration
                    );


                ApplyTint(
                    state,
                    t
                );


                if (state.timer >= rotDuration)
                {
                    state.shrinking = true;

                    state.timer = 0f;


                    state.startScale =
                        state.bamboo.transform.localScale;
                }


                continue;
            }


            // -------------------------------------------------
            // PHASE 2 - SHRINK
            // -------------------------------------------------

            float shrink =
                Mathf.Clamp01(
                    state.timer /
                    shrinkDuration
                );


            state.bamboo.transform.localScale =
                Vector3.Lerp(
                    state.startScale,
                    Vector3.zero,
                    shrink
                );


            // -------------------------------------------------
            // FINISHED
            // -------------------------------------------------

            if (shrink >= 1f)
            {
                GameObject bamboo =
                    state.bamboo;


                rotting.RemoveAt(i);

                rottingSet.Remove(
                    bamboo
                );


                Forget(bamboo);


                TotalRotted++;


                Destroy(bamboo);
            }
        }
    }


    // =========================================================
    // MATERIAL COLOR
    // =========================================================

    private void ApplyTint(
        RotState state,
        float amount
    )
    {
        for (int i = 0;
             i < state.renderers.Length;
             i++)
        {
            if (state.renderers[i] == null)
                continue;


            if (state.colorIds[i] == -1)
                continue;


            state.renderers[i].GetPropertyBlock(
                block
            );


            block.SetColor(
                state.colorIds[i],
                Color.Lerp(
                    state.originalColors[i],
                    rottenColor,
                    amount
                )
            );


            state.renderers[i].SetPropertyBlock(
                block
            );
        }
    }
}