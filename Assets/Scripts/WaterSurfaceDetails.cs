using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(WaterEnvironmentVisuals), typeof(MeshFilter))]
public class WaterSurfaceDetails : MonoBehaviour
{
    [SerializeField] private Shader surfaceShader;
    [SerializeField, Min(0.001f)] private float iceOffset = 0.025f;
    [SerializeField, Min(0.1f)] private float crackScale = 2f;
    [SerializeField] private Color iceTint = new Color(0.48f, 0.72f, 0.81f);
    [Header("Terrain shoreline - horizontal Unity Plane")]
    [SerializeField, Range(32, 256)] private int shorelineResolution = 128;
    [Tooltip("Terrain height within this distance below the water counts as shore, in world units.")]
    [SerializeField, Min(0f)] private float shoreDepth = 0.03f;

    private WaterEnvironmentVisuals water;
    private Renderer source, iceRenderer;
    private GameObject iceObject;
    private Material iceMaterial;
    private Texture2D shoreMask;
    private Mesh mesh;
    private float nextMaskUpdate;
    private Matrix4x4 lastMatrix;
    private int lastResolution;
    private float lastShoreDepth;
    private bool maskReady;

    private void OnEnable()
    {
        water = GetComponent<WaterEnvironmentVisuals>();
        source = GetComponent<Renderer>();
        mesh = GetComponent<MeshFilter>().sharedMesh;
        if (surfaceShader == null || !surfaceShader.isSupported || mesh == null)
        {
            Debug.LogError("Assign WaterSurfaceDetails.shader to Surface Shader.", this);
            enabled = false;
            return;
        }
        iceMaterial = new Material(surfaceShader) { name = "Shoreline ice (runtime)" };
        iceMaterial.SetFloat("_IsIce", 1f);
        iceObject = new GameObject("Ice detail (runtime)");
        iceObject.layer = gameObject.layer;
        iceObject.transform.SetParent(transform, false);
        iceObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        iceRenderer = iceObject.AddComponent<MeshRenderer>();
        iceRenderer.sharedMaterial = iceMaterial;
        iceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        iceRenderer.enabled = false;
        maskReady = false;
        nextMaskUpdate = 0f;
    }

    // Uses local mesh X/Z bounds, independent of mesh UV orientation.
    private void RebuildShoreMask()
    {
        int size = Mathf.Clamp(shorelineResolution, 32, 256);
        float[] distances = new float[size * size];
        bool[] wet = new bool[distances.Length];
        Terrain[] terrains = Terrain.activeTerrains;
        Bounds bounds = mesh.bounds;
        iceMaterial.SetVector("_ShoreBounds",new Vector4(bounds.min.x,bounds.min.z,
            Mathf.Max(bounds.size.x,0.0001f),Mathf.Max(bounds.size.z,0.0001f)));
        bool foundTerrain = false;
        bool foundShore = false;
        float stepX = transform.TransformVector(Vector3.right * bounds.size.x).magnitude / (size-1);
        float stepZ = transform.TransformVector(Vector3.forward * bounds.size.z).magnitude / (size-1);
        float diagonal = Mathf.Sqrt(stepX*stepX + stepZ*stepZ);
        const float far = 1e20f;
        for (int y=0; y<size; y++)
        for (int x=0; x<size; x++)
        {
            Vector3 local = new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,(float)x/(size-1)),
                bounds.center.y, Mathf.Lerp(bounds.min.z,bounds.max.z,(float)y/(size-1)));
            Vector3 world = transform.TransformPoint(local);
            bool covered = false;
            float ground = float.NegativeInfinity;
            foreach (Terrain terrain in terrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 extent = terrain.terrainData.size;
                if(world.x < origin.x || world.x > origin.x+extent.x ||
                   world.z < origin.z || world.z > origin.z+extent.z) continue;
                covered = true;
                ground = Mathf.Max(ground,terrain.SampleHeight(world)+origin.y);
            }
            int index=y*size+x;
            foundTerrain |= covered;
            // Outside Terrain is not invented shoreline; it remains open water.
            wet[index] = !covered || ground < world.y-Mathf.Max(0f,shoreDepth);
            distances[index] = wet[index] ? far : 0f;
            foundShore |= !wet[index];
        }
        if(foundShore)
        {
            // Two-pass chamfer distance to land, including islands.
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                int i=y*size+x;
                if(x>0) distances[i]=Mathf.Min(distances[i],distances[i-1]+stepX);
                if(y>0) distances[i]=Mathf.Min(distances[i],distances[i-size]+stepZ);
                if(x>0 && y>0) distances[i]=Mathf.Min(distances[i],distances[i-size-1]+diagonal);
                if(x<size-1 && y>0) distances[i]=Mathf.Min(distances[i],distances[i-size+1]+diagonal);
            }
            for(int y=size-1;y>=0;y--) for(int x=size-1;x>=0;x--)
            {
                int i=y*size+x;
                if(x<size-1) distances[i]=Mathf.Min(distances[i],distances[i+1]+stepX);
                if(y<size-1) distances[i]=Mathf.Min(distances[i],distances[i+size]+stepZ);
                if(x<size-1 && y<size-1) distances[i]=Mathf.Min(distances[i],distances[i+size+1]+diagonal);
                if(x>0 && y<size-1) distances[i]=Mathf.Min(distances[i],distances[i+size-1]+diagonal);
            }
        }
        float maximum=0.001f;
        if(foundShore) foreach(float distance in distances) maximum=Mathf.Max(maximum,distance);
        Color[] pixels=new Color[distances.Length];
        for(int i=0;i<pixels.Length;i++)
            pixels[i]=new Color(foundShore ? distances[i]/maximum : 0.5f,wet[i]?1f:0f,0f,1f);
        if(shoreMask == null || shoreMask.width != size)
        {
            if(shoreMask != null) Destroy(shoreMask);
            shoreMask=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            shoreMask.name="Terrain shoreline (runtime)";
            shoreMask.wrapMode=TextureWrapMode.Clamp;
            shoreMask.filterMode=FilterMode.Bilinear;
        }
        shoreMask.SetPixels(pixels);
        shoreMask.Apply(false,false);
        iceMaterial.SetTexture("_ShoreMask",shoreMask);
        iceMaterial.SetFloat("_HasShore",foundShore?1f:0f);
        if(!maskReady && (!foundTerrain || !foundShore))
            Debug.LogWarning("No Terrain shoreline intersects Water. Ice uses patchy freezing until a shoreline is found. Check water position and Terrain coverage.",this);
        maskReady=true;
        lastMatrix=transform.localToWorldMatrix;
        lastResolution=shorelineResolution;
        lastShoreDepth=shoreDepth;
    }

    private void LateUpdate()
    {
        if(iceMaterial == null) return;
        iceObject.transform.localPosition=Vector3.up*Mathf.Max(0.001f,iceOffset);
        if(!maskReady || (Time.time>=nextMaskUpdate &&
            (lastMatrix != transform.localToWorldMatrix || lastResolution != shorelineResolution || lastShoreDepth != shoreDepth)))
        {
            RebuildShoreMask();
            nextMaskUpdate=Time.time+0.5f;
        }
        iceRenderer.enabled=water.isActiveAndEnabled && source.enabled && water.IceAmount>0.001f;
        iceMaterial.SetFloat("_Amount",water.IceAmount);
        iceMaterial.SetColor("_IceTint",iceTint);
        iceMaterial.SetFloat("_CrackScale",Mathf.Max(0.1f,crackScale));
    }

    private void OnDisable()
    {
        if(iceObject != null) Destroy(iceObject);
        if(iceMaterial != null) Destroy(iceMaterial);
        if(shoreMask != null) Destroy(shoreMask);
        maskReady=false;
    }
}