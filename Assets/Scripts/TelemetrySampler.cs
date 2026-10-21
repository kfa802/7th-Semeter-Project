using UnityEngine;

public class TelemetrySampler : MonoBehaviour
{
    [SerializeField] private EnvironmentSystem environment;
    [SerializeField] private PandaSystem panda;
    [SerializeField] private float sampleInterval = 0.2f;

    private float nextSample;
    private int lastHealth = int.MinValue;
    private int lastWater = int.MinValue;
    private int lastBambooCount = int.MinValue;
    private int lastBambooTotal = int.MinValue;
    private string lastZone;

    private void Start()
    {
        if (TelemetryManager.Instance == null)
        {
            Debug.LogWarning("[TelemetrySampler] No TelemetryManager. Logging disabled.");
            enabled = false;
            return;
        }

        TelemetryManager.Instance.LogTemperatureStart(environment.Temperature);
    }

    private void Update()
    {
        TelemetryManager t = TelemetryManager.Instance;
        if (t == null || !t.SessionActive) return;

        if (Time.realtimeSinceStartup < nextSample) return;
        nextSample = Time.realtimeSinceStartup + sampleInterval;

        // Zone
        string zone = environment.ActiveZoneName;
        if (zone != lastZone)
        {
            lastZone = zone;
            t.LogZone(zone);
        }

        // Health
        int health = Mathf.RoundToInt(panda.Health);
        if (health != lastHealth)
        {
            lastHealth = health;
            t.LogMeter("Health", health.ToString());
        }

        // Water
        int water = Mathf.RoundToInt(environment.Water);
        if (water != lastWater)
        {
            lastWater = water;
            t.LogMeter("Water", water.ToString());
        }

        // Bamboo: count and total are logged separately
        int bambooCount = 0;
        int bambooTotal = 0;
        BambooManager bm = BambooManager.Instance;

        if (bm != null && environment.ActiveZone != null)
        {
            bambooCount = bm.GetCurrentCountForZone(environment.ActiveZone);
            bambooTotal = bm.GetCapacityForZone(environment.ActiveZone);
        }

        if (bambooCount != lastBambooCount)
        {
            lastBambooCount = bambooCount;
            t.LogMeter("Bamboo", bambooCount.ToString());
        }

        if (bambooTotal != lastBambooTotal)
        {
            lastBambooTotal = bambooTotal;
            t.LogMeter("BambooTotal", bambooTotal.ToString());
        }
    }
}
