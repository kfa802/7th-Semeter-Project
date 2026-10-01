using System.Collections.Generic;
using UnityEngine;

public class BambooManager : MonoBehaviour
{
    public static BambooManager Instance { get; private set; }

    [Header("Possible bamboo (every spot that can grow one)")]
    [SerializeField] private List<BambooGrowthSpot> growthSpots =
        new List<BambooGrowthSpot>();
    [SerializeField] private bool autoFindSpots = true;

    [Header("Active bamboo (finding it)")]
    // Registers every BambooPlant in the scene automatically
    [SerializeField] private bool autoFindBamboo = true;
    // Optional: also registers every object with this tag (the tag must exist)
    [SerializeField] private string bambooTag = "";
    // Bamboo placed by hand that you want to add manually
    [SerializeField] private List<GameObject> startingBamboo = new List<GameObject>();

    [Header("Pollution")]
    // Optional: if assigned, pollution also rises when the water gets dirty (100 - water)
    [SerializeField] private EnvironmentSystem environment;
    // Manual pollution (0 = clean, 1 = very polluted). The higher of this
    // and the water-based value is used, so the slider always works.
    [SerializeField, Range(0f, 1f)] private float pollution = 0f;
    // Below this pollution level nothing rots
    [SerializeField, Range(0f, 1f)] private float rotStartsAbovePollution = 0.3f;
    // Seconds between new rotting bamboos: slow just above the limit, fast at full pollution
    [SerializeField] private float slowestRotInterval = 20f;
    [SerializeField] private float fastestRotInterval = 4f;

    [Header("Rotting")]
    [SerializeField] private float rotDuration = 8f;        // seconds from healthy to fully rotten
    [SerializeField] private float shrinkDuration = 1.5f;   // seconds to disappear after that
    [SerializeField] private Color rottenColor = new Color(0.35f, 0.25f, 0.1f);

    [Header("Debug")]
    [SerializeField] private bool showDebugText = true;

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

    private readonly List<GameObject> activeBamboo = new List<GameObject>();
    private readonly Dictionary<GameObject, BambooGrowthSpot> spotOf =
        new Dictionary<GameObject, BambooGrowthSpot>();
    private readonly List<KeyValuePair<GameObject, BambooGrowthSpot>> keptSpots =
        new List<KeyValuePair<GameObject, BambooGrowthSpot>>();
    private readonly List<RotState> rotting = new List<RotState>();
    private readonly HashSet<GameObject> rottingSet = new HashSet<GameObject>();

    // Created in Awake (Unity doesn't allow creating it in a field initializer)
    private MaterialPropertyBlock block;

    private float rotTimer;
    private float scanTimer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // ---- Counts ----
    public int TotalSpawned { get; private set; }
    public int TotalRemoved { get; private set; }
    public int TotalRotted { get; private set; }

    public IReadOnlyList<BambooGrowthSpot> GrowthSpots => growthSpots;

    // Bamboo placed by hand (spots use this to find the bamboo standing on them)
    public IReadOnlyList<GameObject> StartingBamboo => startingBamboo;

    public IReadOnlyList<GameObject> ActiveBamboo
    {
        get
        {
            Prune();
            return activeBamboo;
        }
    }

    // How many bamboos are possible in total (one per spot)
    public int Capacity => growthSpots.Count;

    // Every bamboo that exists right now (healthy and rotting)
    public int ActiveCount
    {
        get
        {
            Prune();
            return activeBamboo.Count;
        }
    }

    public int RottingCount => rotting.Count;
    public int HealthyCount => Mathf.Max(0, ActiveCount - rotting.Count);

    // Healthy bamboo as a percentage of all possible bamboo
    public float Percent =>
        Capacity == 0 ? 0f : Mathf.Clamp01((float)HealthyCount / Capacity) * 100f;

    // 0 = clean, 1 = very polluted
    public float Pollution
    {
        get
        {
            float fromWater = environment != null
                ? Mathf.Clamp01(1f - environment.Water / 100f)
                : 0f;

            return Mathf.Max(pollution, fromWater);
        }
    }

    // Lets other scripts set pollution
    public void SetPollution(float value)
    {
        pollution = Mathf.Clamp01(value);
    }


    private void Awake()
    {
        block = new MaterialPropertyBlock();

        Instance = this;

        growthSpots.RemoveAll(spot => spot == null);

        if (autoFindSpots && growthSpots.Count == 0)
            growthSpots.AddRange(FindObjectsOfType<BambooGrowthSpot>());

        // Bamboo that was placed by hand in the scene
        foreach (GameObject bamboo in startingBamboo)
            RegisterBamboo(bamboo);
    }

    private void Start()
    {
        ScanForBamboo();
    }

    private void Update()
    {
        // Look for new bamboo about once a second
        scanTimer += Time.deltaTime;

        if (scanTimer >= 1f)
        {
            scanTimer = 0f;
            ScanForBamboo();
        }

        UpdatePollution();
        UpdateRotting();
    }


    // =========================================================
    // ACTIVE BAMBOO
    // =========================================================

    // Call this when a bamboo is spawned (pass the spot so it can be freed later)
    public void RegisterBamboo(GameObject bamboo, BambooGrowthSpot spot = null)
    {
        if (bamboo == null)
            return;

        // Always remember which spot the bamboo belongs to
        if (spot != null)
            spotOf[bamboo] = spot;

        if (activeBamboo.Contains(bamboo))
            return;

        activeBamboo.Add(bamboo);
        TotalSpawned++;
    }

    // Registers bamboo that exists in the scene but wasn't reported by a spot
    private void ScanForBamboo()
    {
        if (autoFindBamboo)
        {
            foreach (BambooPlant plant in FindObjectsOfType<BambooPlant>())
            {
                // Skip anything that is already rotting away
                if (!rottingSet.Contains(plant.gameObject))
                    RegisterBamboo(plant.gameObject);
            }
        }

        if (!string.IsNullOrEmpty(bambooTag))
        {
            try
            {
                foreach (GameObject bamboo in GameObject.FindGameObjectsWithTag(bambooTag))
                {
                    if (!rottingSet.Contains(bamboo))
                        RegisterBamboo(bamboo);
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    "BambooManager: the tag '" + bambooTag + "' does not exist. " +
                    "Create it in the Tag dropdown (Add Tag...)."
                );

                bambooTag = "";
            }
        }
    }

    // Remove a bamboo on purpose
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

    // Takes a bamboo out of the list, frees its spot and counts it
    private void Forget(GameObject bamboo)
    {
        if (activeBamboo.Remove(bamboo))
            TotalRemoved++;

        if (spotOf.TryGetValue(bamboo, out BambooGrowthSpot spot))
        {
            if (spot != null)
                spot.Free();

            spotOf.Remove(bamboo);
        }
    }

    // Bamboos destroyed by something else drop out of the list on their own
    private void Prune()
    {
        for (int i = activeBamboo.Count - 1; i >= 0; i--)
        {
            if (activeBamboo[i] != null)
                continue;

            activeBamboo.RemoveAt(i);
            TotalRemoved++;
        }

        // Free the spots of bamboo that no longer exists
        if (spotOf.Count == 0)
            return;

        bool anyGone = false;
        keptSpots.Clear();

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
        }
    }


    // =========================================================
    // POSSIBLE BAMBOO (SPOTS)
    // =========================================================

    public List<BambooGrowthSpot> GetFreeSpots()
    {
        List<BambooGrowthSpot> free = new List<BambooGrowthSpot>();

        foreach (BambooGrowthSpot spot in growthSpots)
        {
            if (spot != null && spot.IsAvailable)
                free.Add(spot);
        }

        return free;
    }


    // =========================================================
    // POLLUTION -> ROTTING
    // =========================================================

    private void UpdatePollution()
    {
        // 0 = at the limit, 1 = maximum pollution
        float severity = Mathf.InverseLerp(rotStartsAbovePollution, 1f, Pollution);

        if (severity <= 0f)
        {
            rotTimer = 0f;
            return;
        }

        float interval = Mathf.Lerp(slowestRotInterval, fastestRotInterval, severity);

        rotTimer += Time.deltaTime;

        if (rotTimer >= interval)
        {
            rotTimer = 0f;
            RotRandomBamboo();
        }
    }

    // Also handy for testing: right-click the component title > "Rot one bamboo"
    [ContextMenu("Rot one bamboo")]
    public void RotRandomBamboo()
    {
        Prune();

        List<GameObject> healthy = new List<GameObject>();

        foreach (GameObject bamboo in activeBamboo)
        {
            if (bamboo != null && !rottingSet.Contains(bamboo))
                healthy.Add(bamboo);
        }

        if (healthy.Count == 0)
        {
            Debug.Log("BambooManager: no bamboo registered, so nothing can rot.");
            return;
        }

        StartRotting(healthy[Random.Range(0, healthy.Count)]);
    }

    public void StartRotting(GameObject bamboo)
    {
        if (bamboo == null || rottingSet.Contains(bamboo))
            return;

        Renderer[] renderers = bamboo.GetComponentsInChildren<Renderer>();
        int[] ids = new int[renderers.Length];
        Color[] originals = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;

            ids[i] = -1;
            originals[i] = Color.white;

            if (material == null)
                continue;

            if (material.HasProperty(BaseColorId)) ids[i] = BaseColorId;   // URP / HDRP
            else if (material.HasProperty(ColorId)) ids[i] = ColorId;      // Built-in

            if (ids[i] != -1)
                originals[i] = material.GetColor(ids[i]);
        }

        rotting.Add(new RotState
        {
            bamboo = bamboo,
            renderers = renderers,
            colorIds = ids,
            originalColors = originals
        });

        rottingSet.Add(bamboo);
    }

    private void UpdateRotting()
    {
        for (int i = rotting.Count - 1; i >= 0; i--)
        {
            RotState state = rotting[i];

            // Destroyed by something else
            if (state.bamboo == null)
            {
                rotting.RemoveAt(i);
                continue;
            }

            state.timer += Time.deltaTime;

            // Phase 1: turns brown
            if (!state.shrinking)
            {
                float t = Mathf.Clamp01(state.timer / rotDuration);
                ApplyTint(state, t);

                if (state.timer >= rotDuration)
                {
                    state.shrinking = true;
                    state.timer = 0f;
                    state.startScale = state.bamboo.transform.localScale;
                }

                continue;
            }

            // Phase 2: shrinks away, then is removed
            float shrink = Mathf.Clamp01(state.timer / shrinkDuration);

            state.bamboo.transform.localScale =
                Vector3.Lerp(state.startScale, Vector3.zero, shrink);

            if (shrink >= 1f)
            {
                GameObject bamboo = state.bamboo;

                rotting.RemoveAt(i);
                rottingSet.Remove(bamboo);

                // Frees the spot, so new bamboo can grow there
                Forget(bamboo);
                TotalRotted++;

                Destroy(bamboo);
            }
        }
    }

    private void ApplyTint(RotState state, float amount)
    {
        for (int i = 0; i < state.renderers.Length; i++)
        {
            if (state.renderers[i] == null || state.colorIds[i] == -1)
                continue;

            state.renderers[i].GetPropertyBlock(block);

            block.SetColor(
                state.colorIds[i],
                Color.Lerp(state.originalColors[i], rottenColor, amount)
            );

            state.renderers[i].SetPropertyBlock(block);
        }
    }


    // =========================================================
    // DEBUG
    // =========================================================

    private void OnGUI()
    {
        if (!showDebugText)
            return;

        float severity = Mathf.InverseLerp(rotStartsAbovePollution, 1f, Pollution);
        float interval = Mathf.Lerp(slowestRotInterval, fastestRotInterval, severity);

        string status;

        if (ActiveCount == 0)
            status = "NO BAMBOO REGISTERED - nothing can rot";
        else if (severity <= 0f)
            status = "pollution too low, nothing rots";
        else
            status = "next rot in " + Mathf.Max(0f, interval - rotTimer).ToString("0.0") + "s";

        GUI.Label(
            new Rect(10, 10, 700, 100),
            "Bamboo in scene: " + ActiveCount + "   possible spots: " + Capacity +
            "   free spots: " + GetFreeSpots().Count +
            "   rotting: " + RottingCount +
            "\nspawned: " + TotalSpawned +
            "   removed: " + TotalRemoved +
            "   rotted: " + TotalRotted +
            "   pollution: " + Pollution.ToString("0.00") +
            "\n" + status
        );
    }
}