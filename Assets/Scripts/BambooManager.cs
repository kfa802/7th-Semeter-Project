
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

    private float naturalGrowthTimer;
    private float naturalGrowthTargetTime;

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

    public int ActiveCount
    {
        get
        {
            Prune();
            return activeBamboo.Count;
        }
    }

    public int RottingCount => rotting.Count;

    public int HealthyCount
    {
        get
        {
            Prune();
            return Mathf.Max(0, activeBamboo.Count - rottingSet.Count);
        }
    }

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

        if (available.Count == 0)
            return;

        BambooGrowthSpot selected =
            available[Random.Range(0, available.Count)];

        selected.GrowBamboo();
    }

    // =========================================================
    // NATURAL BAMBOO GROWTH
    // =========================================================

    private void UpdateNaturalBambooGrowth()
    {
        if (environment == null)
            return;

        EcosystemZone zone =
            EnvironmentSystem.Instance != null
                ? EnvironmentSystem.Instance.ActiveZone
                : null;

        if (zone == null)
        {
            naturalGrowthTimer = 0f;
            return;
        }

        if (environment.Water < naturalGrowthWaterThreshold)
        {
            naturalGrowthTimer = 0f;
            return;
        }

        naturalGrowthTimer += Time.deltaTime;

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

    // The panda must be inside an ecosystem zone.
    if (EnvironmentSystem.Instance == null ||
        EnvironmentSystem.Instance.ActiveZone == null)
    {
        Debug.Log("Cannot eat: The panda is not inside an ecosystem zone.");
        return false;
    }

    EcosystemZone activeZone = EnvironmentSystem.Instance.ActiveZone;

    // Only consume bamboo that is actually active in the scene,
    // healthy, and assigned to the panda's current zone.
    foreach (GameObject bamboo in activeBamboo.ToArray())
    {
        if (bamboo == null)
            continue;

        if (!bamboo.activeInHierarchy)
            continue;

        if (rottingSet.Contains(bamboo))
            continue;

        if (!spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot))
            continue;

        if (spot == null || spot.Zone != activeZone)
            continue;

        // Consume exactly one bamboo.
        Forget(bamboo);
        Destroy(bamboo);

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
            if (bamboo == null || rottingSet.Contains(bamboo))
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

    private int GetCurrentCountForZone(EcosystemZone zone)
    {
        if (zone == null)
            return 0;

        Prune();

        int count = 0;

        foreach (GameObject bamboo in activeBamboo)
        {
            if (bamboo == null)
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
            if (bamboo == null || rottingSet.Contains(bamboo))
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
        if (bamboo == null || rottingSet.Contains(bamboo))
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

        if (rottingSet.Contains(bamboo))
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

    
}