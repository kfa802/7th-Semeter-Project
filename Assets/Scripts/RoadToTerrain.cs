
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RoadToTerrain : MonoBehaviour
{
    [Tooltip("Leave empty to use whichever terrain is under the road (works with several terrain tiles)")]
    public Terrain terrain;

    [Tooltip("The main road surface, used to flatten the terrain. Leave empty to pick it automatically.")]
    public MeshFilter roadSurface;

    [Header("Road")]
    [Tooltip("Raise the road this far above the terrain (m)")]
    public float lift = 0.05f;
    [Tooltip("How much the road ignores small bumps (m). 0 = follows every bump. " +
             "Bigger = flatter, gentler road that stays level side to side.")]
    public float smoothRadius = 8f;

    [Header("Terrain")]
    public bool flattenTerrain = true;
    [Tooltip("Terrain ends up this far below the road surface (m)")]
    public float terrainSink = 0.05f;
    [Tooltip("Distance over which the terrain blends back to its original shape (m)")]
    public float blendDistance = 4f;

    // Original (flat) meshes, kept so the fit can be re-run cleanly.
    [SerializeField, HideInInspector] List<MeshFilter> filters = new List<MeshFilter>();
    [SerializeField, HideInInspector] List<Mesh> originals = new List<Mesh>();

    [System.NonSerialized] public string lastResult = "";
    [System.NonSerialized] public bool lastWasError;

    // ------------------------------------------------------------------ main

    [ContextMenu("Fit Road To Terrain")]
    public void Fit()
    {
        var terrains = GetTerrains();
        if (terrains.Count == 0) { Report("No terrain found in the scene.", true); return; }

        int n = ConformMeshes(terrains, out string problem);
        if (problem != null) { Report(problem, true); return; }
        if (n == 0) { Report("No visible road meshes found under this object.", true); return; }

        string msg = $"Fitted {n} road piece(s) to the terrain.";
        if (flattenTerrain)
        {
            var surf = roadSurface && roadSurface.gameObject.activeInHierarchy ? roadSurface : AutoSurface();
            if (!surf) msg += "\nCould not find a road surface to flatten the terrain under.";
            else
            {
                int t = 0;
                foreach (var ter in terrains)
                    if (FlattenTerrain(ter, surf)) t++;
                msg += $"\nFlattened {t} terrain(s) under '{surf.name}'.";
            }
        }
        Report(msg, false);
    }

    [ContextMenu("Restore Original Road Mesh")]
    public void Restore()
    {
        int n = 0;
        for (int i = 0; i < filters.Count && i < originals.Count; i++)
            if (filters[i] && originals[i])
            {
#if UNITY_EDITOR
                Undo.RecordObject(filters[i], "Restore road mesh");
#endif
                filters[i].sharedMesh = originals[i];
                n++;
            }
        Report($"Restored {n} road piece(s) to their flat shape. (The terrain is not changed - use Ctrl+Z for that.)", false);
    }

    void Report(string msg, bool error)
    {
        lastResult = msg;
        lastWasError = error;
        if (error) Debug.LogWarning("RoadToTerrain: " + msg, this);
        else Debug.Log("RoadToTerrain: " + msg, this);
    }

    // ------------------------------------------------------------- terrains

    List<Terrain> GetTerrains()
    {
        var list = new List<Terrain>();
        if (terrain) list.Add(terrain);
        else list.AddRange(Terrain.activeTerrains);
        return list;
    }

    static Terrain TerrainAt(Vector3 world, List<Terrain> terrains)
    {
        foreach (var t in terrains)
        {
            Vector3 p = t.transform.position;
            Vector3 s = t.terrainData.size;
            if (world.x >= p.x && world.x <= p.x + s.x && world.z >= p.z && world.z <= p.z + s.z)
                return t;
        }
        return null;
    }

    static float HeightAt(Vector3 world, Terrain ter) =>
        ter.SampleHeight(world) + ter.transform.position.y;

    // Average terrain height in a circle around the point, so small bumps and
    // sideways tilt are ignored and the road only follows the larger shape.
    float SmoothHeight(Vector3 w, List<Terrain> terrains)
    {
        var centre = TerrainAt(w, terrains);
        if (smoothRadius <= 0.01f) return HeightAt(w, centre);

        float sum = HeightAt(w, centre), weight = 1f;
        const int rings = 3, perRing = 10;
        for (int r = 1; r <= rings; r++)
        {
            float dist = smoothRadius * r / rings;
            float wt = 1f - 0.5f * r / rings;              // nearer samples count more
            for (int k = 0; k < perRing; k++)
            {
                float a = (k + 0.5f * r) / perRing * Mathf.PI * 2f;
                var p = new Vector3(w.x + Mathf.Cos(a) * dist, w.y, w.z + Mathf.Sin(a) * dist);
                var ter = TerrainAt(p, terrains);
                if (!ter) continue;
                sum += HeightAt(p, ter) * wt;
                weight += wt;
            }
        }
        return sum / weight;
    }

    // ------------------------------------------------------------------ road

    int ConformMeshes(List<Terrain> terrains, out string problem)
    {
        problem = null;

        // Forget pieces that were deleted or hidden; remember any new visible ones.
        if (filters.Count != originals.Count) { filters.Clear(); originals.Clear(); }
        for (int i = filters.Count - 1; i >= 0; i--)
            if (!filters[i] || !originals[i] || !filters[i].gameObject.activeInHierarchy)
            {
                filters.RemoveAt(i);
                originals.RemoveAt(i);
            }
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (filters.Contains(mf) || !mf.sharedMesh) continue;
            if (mf.sharedMesh.name.EndsWith("_Fitted")) continue;   // original unknown
            filters.Add(mf);
            originals.Add(mf.sharedMesh);
        }

        // The road was modelled flat, so a vertex's height above the root's
        // y position is its height above the road plane (curb, paint, etc.).
        float baseY = transform.position.y;
        int count = 0;

        for (int i = 0; i < filters.Count; i++)
        {
            var mf = filters[i];
            var src = originals[i];

            var mesh = Instantiate(src);
            mesh.name = src.name + "_Fitted";
            var verts = mesh.vertices;
            if (verts.Length == 0 && src.vertexCount > 0)
            {
                DestroyImmediate(mesh);
                problem = "Unity won't let the script read the road mesh. Select the road's FBX file in the " +
                          "Project window, open the Model tab, tick 'Read/Write', click Apply, and try again.";
                return count;
            }

            var t = mf.transform;
            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 w = t.TransformPoint(verts[v]);
                if (!TerrainAt(w, terrains)) continue;       // off the terrain - leave as is
                float above = w.y - baseY;
                w.y = SmoothHeight(w, terrains) + above + lift;
                verts[v] = t.InverseTransformPoint(w);
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
#if UNITY_EDITOR
            Undo.RecordObject(mf, "Fit road to terrain");
#endif
            mf.sharedMesh = mesh;

            var mc = mf.GetComponent<MeshCollider>();
            if (mc) mc.sharedMesh = mesh;
            count++;
        }
        return count;
    }

    MeshFilter AutoSurface()
    {
        MeshFilter best = null;
        float bestArea = 0f;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.name.ToLower();
            if (n.Contains("asphalt") || n.StartsWith("gravel_road")) return mf;
            var r = mf.GetComponent<Renderer>();
            if (!r) continue;
            float a = r.bounds.size.x * r.bounds.size.z;
            if (a > bestArea) { bestArea = a; best = mf; }
        }
        return best;
    }

    // --------------------------------------------------------------- terrain

    bool FlattenTerrain(Terrain ter, MeshFilter surf)
    {
        var td = ter.terrainData;
        Vector3 tPos = ter.transform.position;
        Vector3 size = td.size;

        Bounds b = surf.GetComponent<Renderer>().bounds;
        b.Expand(new Vector3(blendDistance * 2f, 0f, blendDistance * 2f));
        if (b.max.x < tPos.x || b.min.x > tPos.x + size.x ||
            b.max.z < tPos.z || b.min.z > tPos.z + size.z)
            return false;                                     // road isn't on this terrain

#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(td, "Flatten terrain under road");
#endif
        int res = td.heightmapResolution;
        float cell = size.x / (res - 1);

        int x0 = ToCell(b.min.x - tPos.x, size.x, res, false);
        int x1 = ToCell(b.max.x - tPos.x, size.x, res, true);
        int z0 = ToCell(b.min.z - tPos.z, size.z, res, false);
        int z1 = ToCell(b.max.z - tPos.z, size.z, res, true);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        if (w <= 0 || h <= 0) return false;

        float[,] heights = td.GetHeights(x0, z0, w, h);   // indexed [z, x]
        float[,] target = new float[h, w];
        bool[,] onRoad = new bool[h, w];

        // Temporary collider to raycast against the fitted road surface.
        var col = surf.gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = surf.sharedMesh;
        float top = b.max.y + 50f;
        bool any = false;

        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            float wx = tPos.x + (x0 + x) / (float)(res - 1) * size.x;
            float wz = tPos.z + (z0 + z) / (float)(res - 1) * size.z;
            var ray = new Ray(new Vector3(wx, top, wz), Vector3.down);
            if (col.Raycast(ray, out RaycastHit hit, top - tPos.y + 1000f))
            {
                onRoad[z, x] = true;
                any = true;
                target[z, x] = Mathf.Clamp01((hit.point.y - terrainSink - tPos.y) / size.y);
            }
        }
        DestroyImmediate(col);
        if (!any) return false;

        // Blend cells near the road back toward the original terrain.
        float[,] result = (float[,])heights.Clone();
        int rc = Mathf.CeilToInt(blendDistance / cell);

        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            if (onRoad[z, x]) { result[z, x] = target[z, x]; continue; }

            float best = float.MaxValue, bestH = 0f;
            for (int dz = -rc; dz <= rc; dz++)
            for (int dx = -rc; dx <= rc; dx++)
            {
                int nz = z + dz, nx = x + dx;
                if (nz < 0 || nz >= h || nx < 0 || nx >= w || !onRoad[nz, nx]) continue;
                float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                if (d < best) { best = d; bestH = target[nz, nx]; }
            }

            if (best <= blendDistance)
            {
                float t = Mathf.SmoothStep(0f, 1f, best / blendDistance);
                result[z, x] = Mathf.Lerp(bestH, heights[z, x], t);
            }
        }

        td.SetHeights(x0, z0, result);
        return true;
    }

    static int ToCell(float local, float size, int res, bool ceil)
    {
        float f = local / size * (res - 1);
        int i = ceil ? Mathf.CeilToInt(f) : Mathf.FloorToInt(f);
        return Mathf.Clamp(i, 0, res - 1);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoadToTerrain))]
public class RoadToTerrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var rt = (RoadToTerrain)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Fit Road To Terrain", GUILayout.Height(32)))
            rt.Fit();
        if (GUILayout.Button("Restore Original Road Mesh"))
            rt.Restore();

        if (!string.IsNullOrEmpty(rt.lastResult))
            EditorGUILayout.HelpBox(rt.lastResult, rt.lastWasError ? MessageType.Warning : MessageType.Info);
    }
}
#endif