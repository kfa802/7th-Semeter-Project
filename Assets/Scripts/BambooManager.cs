using System.Collections.Generic;
using UnityEngine;

public class BambooManager : MonoBehaviour
{
    public static BambooManager Instance { get; private set; }

    // =========================================================
    // ZONE CONFIGURATION
    // =========================================================

    [System.Serializable]
    public class ZoneBambooSettings
    {
        [Header("Zone")]
        public EcosystemZone zone;

        [Header("Bamboo Already in Scene")]
        public List<GameObject> bambooInScene =
            new List<GameObject>();

        [Header("Growth Spots")]
        public List<BambooGrowthSpot> growthSpots =
            new List<BambooGrowthSpot>();

        [Header("Bamboo Statistics - Runtime")]
        [SerializeField] private int startingBamboo;
        [SerializeField] private int currentBamboo;
        [SerializeField] private int totalBamboo;
        [SerializeField] private int remainingCapacity;

        public int StartingBamboo => startingBamboo;
        public int CurrentBamboo => currentBamboo;
        public int TotalBamboo => totalBamboo;
        public int RemainingCapacity => remainingCapacity;

        public void CaptureStartingCount()
        {
            bambooInScene.RemoveAll(bamboo => bamboo == null);
            startingBamboo = bambooInScene.Count;
        }

        public void UpdateStatistics(int current)
        {
            growthSpots.RemoveAll(spot => spot == null);

            currentBamboo = current;
            totalBamboo = growthSpots.Count;

            remainingCapacity = Mathf.Max(
                0,
                totalBamboo - currentBamboo
            );
        }
    }

    [Header("Bamboo Setup Per Zone")]
    [SerializeField]
    private List<ZoneBambooSettings> zoneSettings =
        new List<ZoneBambooSettings>();

    [Header("Automatic Detection")]
    [SerializeField] private bool autoFindSpots = true;
    [SerializeField] private bool autoFindBamboo = true;
    [SerializeField] private string bambooTag = "";

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

    [Header("Water-Based Growth Difficulty")]
    [SerializeField, Range(0f, 100f)]
    private float bambooWaterThreshold = 30f;

    [Tooltip("Growth speed multiplier when water is at the threshold.")]
    [SerializeField, Range(0.01f, 1f)]
    private float growthMultiplierAtThreshold = 1f;

    [Tooltip("Growth speed multiplier when water is almost empty.")]
    [SerializeField, Range(0.01f, 1f)]
    private float growthMultiplierAtZeroWater = 0.05f;

    private float naturalGrowthTimer;
    private float naturalGrowthTargetTime;

    // =========================================================
    // PANDA POOP
    // =========================================================

    [Header("Panda Poop")]
    [Tooltip("Fewest bamboo that start growing per poop.")]
    [SerializeField, Min(1)]
    private int minBambooPerPoop = 1;

    [Tooltip("Most bamboo that start growing per poop. A random amount " +
             "between Min and Max is picked each time (both included). " +
             "Limited by the free growth spots in the zone.")]
    [SerializeField, Min(1)]
    private int maxBambooPerPoop = 2;

    [Tooltip("Extra delay (seconds) added to each bamboo after the first, " +
             "so they don't appear and grow at exactly the same time.")]
    [SerializeField, Min(0f)]
    private float minExtraDelay = 0.5f;

    [SerializeField, Min(0f)]
    private float maxExtraDelay = 2f;

    // =========================================================
    // POLLUTION
    // =========================================================

    [Header("Pollution")]
    [SerializeField] private EnvironmentSystem environment;

    [SerializeField, Range(0f, 1f)]
    private float rotStartsAbovePollution = 0.3f;

    [SerializeField] private float slowestRotInterval = 20f;
    [SerializeField] private float fastestRotInterval = 4f;

    private float rotTimer;

    // =========================================================
    // ROTTING
    // =========================================================

    [Header("Rotting")]
    [SerializeField] private float rotDuration = 8f;
    [SerializeField] private float shrinkDuration = 1.5f;

    [SerializeField]
    private Color rottenColor = new Color(0.35f, 0.25f, 0.1f);

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
    // RUNTIME DATA
    // =========================================================

    private readonly List<BambooGrowthSpot> growthSpots =
        new List<BambooGrowthSpot>();

    private readonly List<GameObject> startingBamboo =
        new List<GameObject>();

    private readonly List<GameObject> activeBamboo =
        new List<GameObject>();

    private readonly Dictionary<GameObject, BambooGrowthSpot> spotOf =
        new Dictionary<GameObject, BambooGrowthSpot>();

    private readonly List<KeyValuePair<GameObject, BambooGrowthSpot>>
        keptSpots =
            new List<KeyValuePair<GameObject, BambooGrowthSpot>>();

    private readonly List<RotState> rotting =
        new List<RotState>();

    private readonly HashSet<GameObject> rottingSet =
        new HashSet<GameObject>();

    private readonly HashSet<GameObject> bambooBeingEaten =
        new HashSet<GameObject>();

    private MaterialPropertyBlock block;
    private bool initialized;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    // =========================================================
    // TOTAL COUNTERS
    // =========================================================

    public int TotalSpawned { get; private set; }
    public int TotalRemoved { get; private set; }
    public int TotalRotted { get; private set; }

    public IReadOnlyList<BambooGrowthSpot> GrowthSpots => growthSpots;
    public IReadOnlyList<GameObject> StartingBamboo => startingBamboo;

    public IReadOnlyList<GameObject> ActiveBamboo
    {
        get
        {
            Prune();
            return activeBamboo;
        }
    }

    public int Capacity => growthSpots.Count;

    // Only fully grown bamboo counts. Bamboo that is still growing,
    // or being eaten (breaking), is left out of the numbers.
    public int ActiveCount
    {
        get
        {
            Prune();

            int count = 0;

            foreach (GameObject bamboo in activeBamboo)
            {
                if (CountsAsBamboo(bamboo))
                    count++;
            }

            return count;
        }
    }

    public int RottingCount => rotting.Count;

    public int HealthyCount
    {
        get
        {
            Prune();

            int count = 0;

            foreach (GameObject bamboo in activeBamboo)
            {
                if (CountsAsBamboo(bamboo) && !rottingSet.Contains(bamboo))
                    count++;
            }

            return count;
        }
    }

    // True while a newly planted bamboo hasn't finished growing.
    public bool IsStillGrowing(GameObject bamboo)
    {
        if (bamboo == null)
            return false;

        BambooPlant plant = bamboo.GetComponent<BambooPlant>();

        if (plant == null)
            plant = bamboo.GetComponentInChildren<BambooPlant>();

        return plant != null && !plant.IsFullyGrown;
    }

    // A bamboo counts when it exists, is fully grown and isn't being eaten.
    private bool CountsAsBamboo(GameObject bamboo)
    {
        return bamboo != null &&
               !bambooBeingEaten.Contains(bamboo) &&
               !IsStillGrowing(bamboo);
    }

    /// <summary>
    /// Fired whenever the bamboo count changes (fed, broken, grown, rotted).
    /// UI scripts can subscribe to this to update instantly.
    /// </summary>
    public event System.Action BambooCountChanged;

    public float Percent
    {
        get
        {
            EcosystemZone zone =
                EnvironmentSystem.Instance != null
                    ? EnvironmentSystem.Instance.ActiveZone
                    : null;

            return GetPercentForZone(zone);
        }
    }

    public float Pollution
    {
        get
        {
            if (EnvironmentSystem.Instance == null ||
                EnvironmentSystem.Instance.ActiveZone == null)
                return 0f;

            if (environment == null)
                return EnvironmentSystem.Instance.Pollution;

            return environment.Pollution;
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "More than one BambooManager exists.",
                this
            );

            enabled = false;
            return;
        }

        Instance = this;
        block = new MaterialPropertyBlock();

        BuildZoneLists();
    }

    private void Start()
    {
        // Wait one frame so BambooGrowthSpot.Start can run first.
        StartCoroutine(InitializeBamboo());
    }

    private System.Collections.IEnumerator InitializeBamboo()
    {
        yield return null;

        ScanForBamboo();

        startingBamboo.Clear();

        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings == null)
                continue;

            settings.CaptureStartingCount();

            foreach (GameObject bamboo in settings.bambooInScene)
            {
                if (bamboo != null && !startingBamboo.Contains(bamboo))
                    startingBamboo.Add(bamboo);
            }
        }

        SetNaturalGrowthTimer();
        RefreshZoneStatistics();

        initialized = true;
    }

    private void Update()
    {
        UpdateRotting();
        UpdatePollutionRotTimer();

        if (initialized)
            UpdateNaturalBambooGrowth();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // BUILD ZONE LISTS
    // =========================================================

    private void BuildZoneLists()
    {
        growthSpots.Clear();
        startingBamboo.Clear();

        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings == null || settings.zone == null)
                continue;

            settings.growthSpots.RemoveAll(spot => spot == null);
            settings.bambooInScene.RemoveAll(bamboo => bamboo == null);

            AddUniqueSpots(settings.growthSpots);
            AddUniqueBamboo(settings.bambooInScene);
        }

        if (!autoFindSpots)
            return;

        BambooGrowthSpot[] foundSpots =
            FindObjectsOfType<BambooGrowthSpot>();

        foreach (BambooGrowthSpot spot in foundSpots)
        {
            if (spot == null)
                continue;

            ZoneBambooSettings settings = FindSettings(spot.Zone);

            if (settings == null)
            {
                Debug.LogWarning(
                    "Growth spot '" + spot.name +
                    "' has no matching zone configured in BambooManager.",
                    spot
                );

                continue;
            }

            if (!settings.growthSpots.Contains(spot))
                settings.growthSpots.Add(spot);

            if (!growthSpots.Contains(spot))
                growthSpots.Add(spot);
        }

        // Keep the combined list synchronized with zone lists.
        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings != null)
                AddUniqueSpots(settings.growthSpots);
        }
    }

    private void AddUniqueSpots(List<BambooGrowthSpot> spots)
    {
        foreach (BambooGrowthSpot spot in spots)
        {
            if (spot != null && !growthSpots.Contains(spot))
                growthSpots.Add(spot);
        }
    }

    private void AddUniqueBamboo(List<GameObject> bambooList)
    {
        foreach (GameObject bamboo in bambooList)
        {
            if (bamboo != null && !startingBamboo.Contains(bamboo))
                startingBamboo.Add(bamboo);
        }
    }

    private ZoneBambooSettings FindSettings(EcosystemZone zone)
    {
        if (zone == null)
            return null;

        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings != null && settings.zone == zone)
                return settings;
        }

        return null;
    }

    // =========================================================
    // FIND EXISTING BAMBOO
    // =========================================================

    private void ScanForBamboo()
    {
        // Register bamboo explicitly assigned in the Inspector.
        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings == null)
                continue;

            foreach (GameObject bamboo in settings.bambooInScene)
            {
                if (bamboo == null)
                    continue;

                BambooGrowthSpot spot = FindSpotForBamboo(bamboo);

                GameObject registeredBamboo = bamboo;

                if (spot != null && spot.ExistingBamboo != null)
                    registeredBamboo = spot.ExistingBamboo;

                RegisterBamboo(registeredBamboo, spot);
            }
        }

        // Discover bamboo plants in the scene.
        if (autoFindBamboo)
        {
            BambooPlant[] plants = FindObjectsOfType<BambooPlant>();

            foreach (BambooPlant plant in plants)
            {
                if (plant == null)
                    continue;

                BambooGrowthSpot spot =
                    FindSpotForBamboo(plant.gameObject);

                if (spot == null)
                    continue;

                GameObject registeredBamboo =
                    spot.ExistingBamboo != null
                        ? spot.ExistingBamboo
                        : plant.gameObject;

                RegisterBamboo(registeredBamboo, spot);

                ZoneBambooSettings settings = FindSettings(spot.Zone);

                if (settings != null &&
                    !settings.bambooInScene.Contains(registeredBamboo))
                {
                    settings.bambooInScene.Add(registeredBamboo);
                }
            }
        }

        // Optional tag-based discovery.
        if (!string.IsNullOrEmpty(bambooTag))
        {
            try
            {
                GameObject[] tagged =
                    GameObject.FindGameObjectsWithTag(bambooTag);

                foreach (GameObject bamboo in tagged)
                {
                    if (bamboo == null)
                        continue;

                    BambooGrowthSpot spot = FindSpotForBamboo(bamboo);

                    if (spot == null)
                        continue;

                    GameObject registeredBamboo =
                        spot.ExistingBamboo != null
                            ? spot.ExistingBamboo
                            : bamboo;

                    RegisterBamboo(registeredBamboo, spot);
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    "The bamboo tag '" + bambooTag +
                    "' does not exist. Tag detection was skipped.",
                    this
                );

                bambooTag = "";
            }
        }
    }

    private BambooGrowthSpot FindSpotForBamboo(GameObject bamboo)
    {
        if (bamboo == null)
            return null;

        foreach (BambooGrowthSpot spot in growthSpots)
        {
            if (spot == null)
                continue;

            GameObject existing = spot.ExistingBamboo;

            if (existing == null)
                continue;

            if (existing == bamboo ||
                bamboo.transform.IsChildOf(existing.transform) ||
                existing.transform.IsChildOf(bamboo.transform))
            {
                return spot;
            }
        }

        return null;
    }

    // =========================================================
    // REGISTER BAMBOO
    // =========================================================

    public void RegisterBamboo(
        GameObject bamboo,
        BambooGrowthSpot spot = null)
    {
        if (bamboo == null)
            return;

        if (spot == null)
            spot = FindSpotForBamboo(bamboo);

        if (spot != null)
            spotOf[bamboo] = spot;

        if (activeBamboo.Contains(bamboo))
        {
            RefreshZoneStatistics();
            return;
        }

        activeBamboo.Add(bamboo);
        TotalSpawned++;

        RefreshZoneStatistics();
    }

    // =========================================================
    // GROWTH LIMIT
    // =========================================================

    public bool CanGrowInZone(EcosystemZone zone)
    {
        ZoneBambooSettings settings = FindSettings(zone);

        if (settings == null)
            return false;

        // The growth spots define the maximum.
        int totalSpots = settings.growthSpots.Count;

        if (totalSpots <= 0)
            return false;

        int occupiedSpots = 0;

        foreach (BambooGrowthSpot spot in settings.growthSpots)
        {
            if (spot != null && !spot.IsAvailable)
                occupiedSpots++;
        }

        return occupiedSpots < totalSpots;
    }

    // =========================================================
    // PANDA POOP -> BAMBOO GROWTH
    // =========================================================

    public void PandaPooped()
    {
        EcosystemZone zone =
            EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.ActiveZone
                : null;

        if (zone == null || !CanGrowInZone(zone))
            return;

        List<BambooGrowthSpot> available = GetFreeSpotsForZone(zone);

        // Random amount between min and max (both included),
        // but never more than there are free spots in this zone.
        int min = Mathf.Max(1, minBambooPerPoop);
        int max = Mathf.Max(min, maxBambooPerPoop);

        int amount = Mathf.Min(
            Random.Range(min, max + 1),
            available.Count
        );

        float extraDelay = 0f;

        for (int i = 0; i < amount; i++)
        {
            // The first bamboo comes on time; each following one
            // gets an extra random delay on top of the previous one.
            if (i > 0)
            {
                extraDelay += Random.Range(
                    minExtraDelay,
                    Mathf.Max(minExtraDelay, maxExtraDelay)
                );
            }

            int index = Random.Range(0, available.Count);

            available[index].GrowBamboo(extraDelay);

            // Remove it so the same spot isn't picked twice.
            available.RemoveAt(index);
        }
    }

    // =========================================================
    // NATURAL BAMBOO GROWTH
    // =========================================================

    private void UpdateNaturalBambooGrowth()
    {
        if (environment == null ||
            EnvironmentSystem.Instance == null)
            return;

        EcosystemZone zone = EnvironmentSystem.Instance.ActiveZone;

        if (zone == null)
        {
            naturalGrowthTimer = 0f;
            return;
        }

        float water = environment.Water;

        // Keep your existing minimum water requirement.
        if (water < naturalGrowthWaterThreshold)
        {
            naturalGrowthTimer = 0f;
            return;
        }

        float growthMultiplier = GetWaterGrowthMultiplier();

        if (growthMultiplier <= 0f)
            return;

        // Slower growth means more time is required.
        naturalGrowthTimer += Time.deltaTime * growthMultiplier;

        if (naturalGrowthTimer < naturalGrowthTargetTime)
            return;

        naturalGrowthTimer = 0f;
        SetNaturalGrowthTimer();

        if (!CanGrowInZone(zone))
            return;

        List<BambooGrowthSpot> available = GetFreeSpotsForZone(zone);

        if (available.Count == 0)
            return;

        BambooGrowthSpot selected =
            available[Random.Range(0, available.Count)];

        selected.GrowBamboo();
    }

    private void SetNaturalGrowthTimer()
    {
        float minimum = Mathf.Max(0f, minimumNaturalGrowthInterval);
        float maximum = Mathf.Max(minimum, maximumNaturalGrowthInterval);

        naturalGrowthTargetTime = Random.Range(minimum, maximum);
        naturalGrowthTimer = 0f;
    }

    // =========================================================
    // CONSUME BAMBOO
    // =========================================================

    public bool ConsumeBamboo()
    {
        Prune();

        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone == null)
        {
            Debug.Log(
                "Cannot eat: The panda is not inside an ecosystem zone."
            );
            return false;
        }

        EcosystemZone activeZone =
            EnvironmentSystem.Instance.ActiveZone;

        // Make a copy because bamboo will be removed after its animation.
        List<GameObject> bambooList =
            new List<GameObject>(activeBamboo);

        foreach (GameObject bamboo in bambooList)
        {
            if (bamboo == null || !bamboo.activeInHierarchy)
                continue;

            // Skip rotting, already-eaten and still-growing bamboo.
            if (rottingSet.Contains(bamboo) ||
                bambooBeingEaten.Contains(bamboo) ||
                IsStillGrowing(bamboo))
                continue;

            if (!spotOf.TryGetValue(
                    bamboo,
                    out BambooGrowthSpot spot))
                continue;

            if (spot == null || spot.Zone != activeZone)
                continue;

            // Reserve this bamboo so it cannot be eaten twice.
            bambooBeingEaten.Add(bamboo);

            // Update the count right away, before the break animation plays.
            RefreshZoneStatistics();

            StartCoroutine(BreakAndRemoveBamboo(bamboo));

            Debug.Log("Panda ate bamboo in " + activeZone.zoneName);
            return true;
        }

        Debug.Log("No available bamboo in " + activeZone.zoneName);
        return false;
    }

    // =========================================================
    // REMOVE BAMBOO
    // =========================================================

    public void RemoveBamboo(GameObject bamboo)
    {
        if (bamboo == null)
            return;

        for (int i = rotting.Count - 1; i >= 0; i--)
        {
            if (rotting[i].bamboo == bamboo)
                rotting.RemoveAt(i);
        }

        rottingSet.Remove(bamboo);

        Forget(bamboo);
        Destroy(bamboo);
    }

    private void Forget(GameObject bamboo)
    {
        if (bamboo == null)
            return;

        if (activeBamboo.Remove(bamboo))
            TotalRemoved++;

        if (spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot))
        {
            if (spot != null)
                spot.Free();

            spotOf.Remove(bamboo);
        }

        RefreshZoneStatistics();
    }

    // =========================================================
    // CLEAN UP DESTROYED BAMBOO
    // =========================================================

    private void Prune()
    {
        bool changed = false;

        // Forget bamboo that was destroyed while it was being eaten.
        if (bambooBeingEaten.RemoveWhere(bamboo => bamboo == null) > 0)
            changed = true;

        for (int i = activeBamboo.Count - 1; i >= 0; i--)
        {
            if (activeBamboo[i] != null)
                continue;

            activeBamboo.RemoveAt(i);
            TotalRemoved++;
            changed = true;
        }

        if (spotOf.Count > 0)
        {
            keptSpots.Clear();
            bool anyGone = false;

            foreach (KeyValuePair<GameObject, BambooGrowthSpot> pair in spotOf)
            {
                if (pair.Key == null)
                {
                    if (pair.Value != null)
                        pair.Value.Free();

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

                foreach (KeyValuePair<GameObject, BambooGrowthSpot> pair in keptSpots)
                    spotOf.Add(pair.Key, pair.Value);

                changed = true;
            }
        }

        if (changed)
            RefreshZoneStatistics();
    }

    // =========================================================
    // GROWTH SPOT QUERIES
    // =========================================================

    public List<BambooGrowthSpot> GetFreeSpots()
    {
        List<BambooGrowthSpot> free =
            new List<BambooGrowthSpot>();

        foreach (BambooGrowthSpot spot in growthSpots)
        {
            if (spot != null && spot.IsAvailable)
                free.Add(spot);
        }

        return free;
    }

    public int GetCapacityForZone(EcosystemZone zone)
    {
        ZoneBambooSettings settings = FindSettings(zone);

        if (settings == null)
            return 0;

        settings.growthSpots.RemoveAll(spot => spot == null);

        return settings.growthSpots.Count;
    }

    public List<BambooGrowthSpot> GetFreeSpotsForZone(
        EcosystemZone zone)
    {
        List<BambooGrowthSpot> free =
            new List<BambooGrowthSpot>();

        ZoneBambooSettings settings = FindSettings(zone);

        if (settings == null)
            return free;

        foreach (BambooGrowthSpot spot in settings.growthSpots)
        {
            if (spot != null && spot.IsAvailable)
                free.Add(spot);
        }

        return free;
    }

    // =========================================================
    // BAMBOO STATISTICS PER ZONE
    // =========================================================

    public int GetHealthyCountForZone(EcosystemZone zone)
    {
        if (zone == null)
            return 0;

        Prune();

        int count = 0;

        foreach (GameObject bamboo in activeBamboo)
        {
            if (!CountsAsBamboo(bamboo) || rottingSet.Contains(bamboo))
                continue;

            if (spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot) &&
                spot != null &&
                spot.Zone == zone)
            {
                count++;
            }
        }

        return count;
    }

    public int GetCurrentCountForZone(EcosystemZone zone)
    {
        if (zone == null)
            return 0;

        Prune();

        int count = 0;

        foreach (GameObject bamboo in activeBamboo)
        {
            // Only fully grown bamboo that isn't being eaten counts.
            if (!CountsAsBamboo(bamboo))
                continue;

            if (spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot) &&
                spot != null &&
                spot.Zone == zone)
            {
                count++;
            }
        }

        return count;
    }

    public float GetPercentForZone(EcosystemZone zone)
    {
        int totalBamboo = GetCapacityForZone(zone);

        if (totalBamboo <= 0)
            return 0f;

        return Mathf.Clamp(
            (float)GetHealthyCountForZone(zone) / totalBamboo * 100f,
            0f,
            100f
        );
    }

    public void RefreshZoneStatistics()
    {
        foreach (ZoneBambooSettings settings in zoneSettings)
        {
            if (settings == null || settings.zone == null)
                continue;

            int current = GetCurrentCountForZone(settings.zone);

            settings.UpdateStatistics(current);
        }

        // Tell any listening UI to update now.
        BambooCountChanged?.Invoke();
    }

    // =========================================================
    // POLLUTION -> ROTTING
    // =========================================================

    private void UpdatePollutionRotTimer()
    {
        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone == null)
        {
            rotTimer = 0f;
            return;
        }

        float severity = Mathf.InverseLerp(
            rotStartsAbovePollution,
            1f,
            Pollution
        );

        if (severity <= 0f)
        {
            rotTimer = 0f;
            return;
        }

        float interval = Mathf.Lerp(
            slowestRotInterval,
            fastestRotInterval,
            severity
        );

        rotTimer += Time.deltaTime;

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

        EcosystemZone zone =
            EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.ActiveZone
                : null;

        if (zone == null)
            return;

        List<GameObject> healthy = new List<GameObject>();

        foreach (GameObject bamboo in activeBamboo)
        {
            if (bamboo == null ||
                rottingSet.Contains(bamboo) ||
                bambooBeingEaten.Contains(bamboo) ||
                IsStillGrowing(bamboo))
                continue;

            if (!spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot))
                continue;

            if (spot == null || spot.Zone != zone)
                continue;

            healthy.Add(bamboo);
        }

        if (healthy.Count == 0)
            return;

        GameObject selected =
            healthy[Random.Range(0, healthy.Count)];

        StartRotting(selected);
    }

    public void StartRotting(GameObject bamboo)
    {
        if (bamboo == null ||
            rottingSet.Contains(bamboo) ||
            bambooBeingEaten.Contains(bamboo))
            return;

        if (!activeBamboo.Contains(bamboo))
            return;

        Renderer[] renderers =
            bamboo.GetComponentsInChildren<Renderer>(true);

        int[] ids = new int[renderers.Length];
        Color[] originals = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;

            ids[i] = -1;
            originals[i] = Color.white;

            if (material == null)
                continue;

            if (material.HasProperty(BaseColorId))
                ids[i] = BaseColorId;
            else if (material.HasProperty(ColorId))
                ids[i] = ColorId;

            if (ids[i] != -1)
                originals[i] = material.GetColor(ids[i]);
        }

        rotting.Add(new RotState
        {
            bamboo = bamboo,
            timer = 0f,
            shrinking = false,
            startScale = bamboo.transform.localScale,
            renderers = renderers,
            colorIds = ids,
            originalColors = originals
        });

        rottingSet.Add(bamboo);

        RefreshZoneStatistics();
    }

    // =========================================================
    // ROTTING AND SHRINKING
    // =========================================================

    private void UpdateRotting()
    {
        for (int i = rotting.Count - 1; i >= 0; i--)
        {
            RotState state = rotting[i];

            if (state.bamboo == null)
            {
                rotting.RemoveAt(i);
                continue;
            }

            state.timer += Time.deltaTime;

            // Phase 1: turn brown.
            if (!state.shrinking)
            {
                float t = rotDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(state.timer / rotDuration);

                ApplyTint(state, t);

                if (state.timer >= rotDuration)
                {
                    state.shrinking = true;
                    state.timer = 0f;
                    state.startScale = state.bamboo.transform.localScale;
                }

                continue;
            }

            // Phase 2: shrink.
            float shrink = shrinkDuration <= 0f
                ? 1f
                : Mathf.Clamp01(state.timer / shrinkDuration);

            state.bamboo.transform.localScale =
                Vector3.Lerp(state.startScale, Vector3.zero, shrink);

            if (shrink >= 1f)
            {
                GameObject bamboo = state.bamboo;

                rotting.RemoveAt(i);
                rottingSet.Remove(bamboo);

                Forget(bamboo);

                TotalRotted++;

                Destroy(bamboo);
            }
        }
    }

    // =========================================================
    // MATERIAL COLOR
    // =========================================================

    private void ApplyTint(RotState state, float amount)
    {
        for (int i = 0; i < state.renderers.Length; i++)
        {
            if (state.renderers[i] == null || state.colorIds[i] == -1)
                continue;

            state.renderers[i].GetPropertyBlock(block);

            block.SetColor(
                state.colorIds[i],
                Color.Lerp(
                    state.originalColors[i],
                    rottenColor,
                    amount
                )
            );

            state.renderers[i].SetPropertyBlock(block);
        }
    }

    public bool HasAvailableBambooInCurrentZone()
    {
        Prune();

        if (EnvironmentSystem.Instance == null ||
            EnvironmentSystem.Instance.ActiveZone == null)
        {
            return false;
        }

        EcosystemZone activeZone =
            EnvironmentSystem.Instance.ActiveZone;

        foreach (GameObject bamboo in activeBamboo)
        {
            if (bamboo == null || !bamboo.activeInHierarchy)
                continue;

            // Skip rotting, already-eaten and still-growing bamboo.
            if (rottingSet.Contains(bamboo) ||
                bambooBeingEaten.Contains(bamboo) ||
                IsStillGrowing(bamboo))
                continue;

            if (!spotOf.TryGetValue(
                    bamboo,
                    out BambooGrowthSpot spot))
            {
                continue;
            }

            if (spot != null && spot.Zone == activeZone)
                return true;
        }

        return false;
    }

    public float GetWaterGrowthMultiplier()
    {
        if (EnvironmentSystem.Instance == null)
            return 0f;

        float water = EnvironmentSystem.Instance.Water;

        // No water means no bamboo growth.
        if (water <= 0f)
            return 0f;

        // At or above the threshold, growth is normal.
        if (water >= bambooWaterThreshold)
            return 1f;

        // Progressively slow growth as water drops below the threshold.
        float waterFactor = Mathf.InverseLerp(
            0f,
            bambooWaterThreshold,
            water
        );

        return Mathf.Lerp(
            growthMultiplierAtZeroWater,
            growthMultiplierAtThreshold,
            waterFactor
        );
    }

    public bool ConsumeBambooInZone(EcosystemZone zone)
    {
        Prune();

        if (zone == null)
            return false;

        List<GameObject> bambooList =
            new List<GameObject>(activeBamboo);

        foreach (GameObject bamboo in bambooList)
        {
            if (bamboo == null || !bamboo.activeInHierarchy)
                continue;

            // Skip rotting, already-eaten and still-growing bamboo.
            if (rottingSet.Contains(bamboo) ||
                bambooBeingEaten.Contains(bamboo) ||
                IsStillGrowing(bamboo))
                continue;

            if (!spotOf.TryGetValue(
                    bamboo,
                    out BambooGrowthSpot spot))
                continue;

            if (spot == null || spot.Zone != zone)
                continue;

            bambooBeingEaten.Add(bamboo);

            // Update the count right away, before the break animation plays.
            RefreshZoneStatistics();

            StartCoroutine(BreakAndRemoveBamboo(bamboo));

            Debug.Log("Other panda ate bamboo in " + zone.zoneName);
            return true;
        }

        Debug.Log("Other panda found no bamboo in " + zone.zoneName);
        return false;
    }

    public bool OtherPandaPooped(EcosystemZone zone)
    {
        if (zone == null || !CanGrowInZone(zone))
            return false;

        List<BambooGrowthSpot> available =
            GetFreeSpotsForZone(zone);

        if (available.Count == 0)
            return false;

        BambooGrowthSpot selected =
            available[Random.Range(0, available.Count)];

        selected.GrowBamboo();

        Debug.Log("Other panda grew bamboo in " + zone.zoneName);

        return true;
    }

    private System.Collections.IEnumerator BreakAndRemoveBamboo(
        GameObject bamboo)
    {
        if (bamboo == null)
            yield break;

        Animator bambooAnimator =
            bamboo.GetComponentInChildren<Animator>(true);

        if (bambooAnimator != null &&
            bambooAnimator.runtimeAnimatorController != null)
        {
            bambooAnimator.speed = 1f;
            bambooAnimator.ResetTrigger("Break");
            bambooAnimator.SetTrigger("Break");

            float elapsed = 0f;
            const float startTimeout = 2f;
            bool breakStarted = false;

            // Wait for the Break state to begin.
            while (elapsed < startTimeout)
            {
                if (bamboo == null || bambooAnimator == null)
                    break;

                AnimatorStateInfo state =
                    bambooAnimator.GetCurrentAnimatorStateInfo(0);

                if (state.IsName("Break"))
                {
                    breakStarted = true;
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Wait until the Break animation has finished.
            if (breakStarted && bamboo != null)
            {
                elapsed = 0f;

                while (elapsed < 10f)
                {
                    if (bamboo == null || bambooAnimator == null)
                        break;

                    AnimatorStateInfo state =
                        bambooAnimator.GetCurrentAnimatorStateInfo(0);

                    if (state.IsName("Break") &&
                        !bambooAnimator.IsInTransition(0) &&
                        state.normalizedTime >= 1f)
                    {
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else if (bamboo != null)
            {
                Debug.LogWarning(
                    "The Break animation did not start for " + bamboo.name,
                    bamboo
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "No Animator Controller assigned to bamboo: " + bamboo.name,
                bamboo
            );
        }

        // Fade out the bamboo after the break animation.
        if (bamboo != null)
        {
            bambooBeingEaten.Remove(bamboo);
            Forget(bamboo);

            yield return StartCoroutine(
                FadeOutAndRemoveBamboo(bamboo, 1.2f)
            );
        }
    }

    private System.Collections.IEnumerator FadeOutAndRemoveBamboo(
        GameObject bamboo,
        float fadeDuration)
    {
        if (bamboo == null)
            yield break;

        Renderer[] renderers =
            bamboo.GetComponentsInChildren<Renderer>(true);

        List<Material> materials = new List<Material>();
        List<Color> originalColors = new List<Color>();
        List<string> colorProperties = new List<string>();

        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null)
                    continue;

                string property = material.HasProperty("_BaseColor")
                    ? "_BaseColor"
                    : material.HasProperty("_Color")
                        ? "_Color"
                        : "";

                if (property == "")
                    continue;

                // Make a unique material instance for this bamboo.
                materials.Add(material);
                originalColors.Add(material.GetColor(property));
                colorProperties.Add(property);

                // Enable transparency for common Unity shaders.
                if (material.HasProperty("_Surface"))
                    material.SetFloat("_Surface", 1f);

                if (material.HasProperty("_Mode"))
                    material.SetFloat("_Mode", 2f);

                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend",
                    (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend",
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);

                material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }
        }

        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            if (bamboo == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / fadeDuration);

            // Smooth fade: gradual at the start and end.
            float alpha = 1f - t * t * (3f - 2f * t);

            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] == null)
                    continue;

                Color color = originalColors[i];
                color.a *= alpha;

                materials[i].SetColor(colorProperties[i], color);
            }

            yield return null;
        }

        Destroy(bamboo);
    }
}