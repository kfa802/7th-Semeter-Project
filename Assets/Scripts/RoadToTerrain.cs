// RoadToTerrain.cs
// Drapes a road mesh (e.g. the one from make_road.py) over a Unity Terrain,
// then flattens/blends the terrain under it so there are no gaps or poke-through.
//
// Setup:
//   1. Put this file anywhere in your Assets folder.
//   2. Drag the imported road into the scene and add this component to its root object.
//   3. Assign Terrain (or leave empty to use the active terrain) and Road Surface
//      (the asphalt child, e.g. "Road_Asphalt").
//   4. Right-click the component header (or the ⋮ menu) → "Fit Road To Terrain".
//      Run it again any time you move the road; it always starts from the original mesh.
//
// Ctrl+Z undoes the terrain change.

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RoadToTerrain : MonoBehaviour
{
    [Tooltip("Leave empty to use Terrain.activeTerrain")]
    public Terrain terrain;

    [Tooltip("The asphalt mesh. Used to flatten the terrain under the road.")]
    public MeshFilter roadSurface;

    [Header("Road")]
    [Tooltip("Raise the road this far above the terrain (m)")]
    public float lift = 0.05f;

    [Header("Terrain")]
    public bool flattenTerrain = true;
    [Tooltip("Terrain ends up this far below the road surface (m)")]
    public float terrainSink = 0.05f;
    [Tooltip("Distance over which the terrain blends back to its original shape (m)")]
    public float blendDistance = 4f;

    // Original (flat) meshes, kept so the fit can be re-run cleanly.
    [SerializeField, HideInInspector] List<MeshFilter> filters = new List<MeshFilter>();
    [SerializeField, HideInInspector] List<Mesh> originals = new List<Mesh>();

    [ContextMenu("Fit Road To Terrain")]
    public void Fit()
    {
        if (!terrain) terrain = Terrain.activeTerrain;
        if (!terrain) { Debug.LogError("RoadToTerrain: no terrain found."); return; }

        ConformMeshes();
        if (flattenTerrain) FlattenTerrain();
    }

    [ContextMenu("Restore Original Road Mesh")]
    public void Restore()
    {
        for (int i = 0; i < filters.Count; i++)
            if (filters[i] && originals[i]) filters[i].sharedMesh = originals[i];
    }

    // ------------------------------------------------------------------ road

    void ConformMeshes()
    {
        if (filters.Count == 0)
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                filters.Add(mf);
                originals.Add(mf.sharedMesh);
            }
        }

        // The road was modelled flat, so a vertex's height above the root's
        // y position is its height above the road plane (curb, paint, etc.).
        float baseY = transform.position.y;

        for (int i = 0; i < filters.Count; i++)
        {
            var mf = filters[i];
            var src = originals[i];
            if (!mf || !src) continue;

            var mesh = Instantiate(src);
            mesh.name = src.name + "_Fitted";
            var verts = mesh.vertices;
            var t = mf.transform;

            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 w = t.TransformPoint(verts[v]);
                float above = w.y - baseY;
                w.y = SampleTerrain(w) + above + lift;
                verts[v] = t.InverseTransformPoint(w);
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var mc = mf.GetComponent<MeshCollider>();
            if (mc) mc.sharedMesh = mesh;
        }
    }

    float SampleTerrain(Vector3 world) =>
        terrain.SampleHeight(world) + terrain.transform.position.y;

    // --------------------------------------------------------------- terrain

    void FlattenTerrain()
    {
        if (!roadSurface) { Debug.LogWarning("RoadToTerrain: assign Road Surface to flatten terrain."); return; }

        var td = terrain.terrainData;
#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(td, "Flatten terrain under road");
#endif
        int res = td.heightmapResolution;
        Vector3 tPos = terrain.transform.position;
        Vector3 size = td.size;
        float cell = size.x / (res - 1);

        Bounds b = roadSurface.GetComponent<Renderer>().bounds;
        b.Expand(new Vector3(blendDistance * 2f, 0f, blendDistance * 2f));

        int x0 = ToCell(b.min.x - tPos.x, size.x, res, false);
        int x1 = ToCell(b.max.x - tPos.x, size.x, res, true);
        int z0 = ToCell(b.min.z - tPos.z, size.z, res, false);
        int z1 = ToCell(b.max.z - tPos.z, size.z, res, true);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        if (w <= 0 || h <= 0) return;

        float[,] heights = td.GetHeights(x0, z0, w, h);   // indexed [z, x]
        float[,] target = new float[h, w];
        bool[,] onRoad = new bool[h, w];

        // Temporary collider to raycast against the fitted asphalt.
        var col = roadSurface.gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = roadSurface.sharedMesh;
        float top = b.max.y + 50f;

        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            float wx = tPos.x + (x0 + x) / (float)(res - 1) * size.x;
            float wz = tPos.z + (z0 + z) / (float)(res - 1) * size.z;
            var ray = new Ray(new Vector3(wx, top, wz), Vector3.down);
            if (col.Raycast(ray, out RaycastHit hit, top - tPos.y + 100f))
            {
                onRoad[z, x] = true;
                target[z, x] = Mathf.Clamp01((hit.point.y - terrainSink - tPos.y) / size.y);
            }
        }
        DestroyImmediate(col);

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
    }

    static int ToCell(float local, float size, int res, bool ceil)
    {
        float f = local / size * (res - 1);
        int i = ceil ? Mathf.CeilToInt(f) : Mathf.FloorToInt(f);
        return Mathf.Clamp(i, 0, res - 1);
    }
}