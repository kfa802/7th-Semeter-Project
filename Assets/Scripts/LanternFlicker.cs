using UnityEngine;

/// <summary>
/// Makes a light flicker like a flame. Put it on the Point Light inside your lantern.
/// The lantern model (the light's parent) glows and flickers too — found automatically.
/// </summary>
[RequireComponent(typeof(Light))]
public class LanternFlicker : MonoBehaviour
{
    public float baseIntensity = 4f;
    [Range(0f, 1f)] public float flickerAmount = 0.3f; // how strong the flicker is
    public float flickerSpeed = 3f;                    // 2-5 = candle, 8+ = faulty bulb

    [Header("Lantern glow")]
    public bool makeLanternGlow = true;
    [ColorUsage(false, true)] public Color glowColor = new Color(1f, 0.5f, 0.15f) * 1.5f;

    Light lt;
    float seed;
    Material[] mats = new Material[0];
    string[] props = new string[0];

    void Awake()
    {
        lt = GetComponent<Light>();
        seed = Random.value * 100f; // each lantern flickers differently

        if (makeLanternGlow && transform.parent != null)
        {
            // Find every mesh under the lantern, however deep it is nested
            var list = new System.Collections.Generic.List<Material>();
            var propList = new System.Collections.Generic.List<string>();
            foreach (var r in transform.parent.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)
                {
                    string p = null;
                    if (m.HasProperty("_EmissionColor")) { p = "_EmissionColor"; m.EnableKeyword("_EMISSION"); } // URP / Built-in
                    else if (m.HasProperty("emissiveFactor")) p = "emissiveFactor";                              // glTFast (GLB)
                    else if (m.HasProperty("_EmissiveColor")) p = "_EmissiveColor";                              // HDRP
                    if (p != null) { list.Add(m); propList.Add(p); }
                }
            }
            mats = list.ToArray();
            props = propList.ToArray();
            if (mats.Length == 0) Debug.LogWarning("LanternFlicker: no glowing material found on the lantern.", this);
        }
    }

    void Update()
    {
        float t = Time.time * flickerSpeed + seed;
        float noise = (Mathf.PerlinNoise(t, 0f) * 0.7f + Mathf.PerlinNoise(t * 3.7f, 1f) * 0.3f) * 2f - 1f;
        float k = 1f + noise * flickerAmount;

        lt.intensity = baseIntensity * k;
        for (int i = 0; i < mats.Length; i++)
            mats[i].SetColor(props[i], glowColor * k);
    }
}