// RoadMaterialSetup.cs
// Sets up materials for the baked road from bake_road_for_unity.py - automatically.
// Works with HDRP, URP and the Built-in render pipeline.
//
// What it does, for every "<name>_Baked" object under the selected road:
//   - finds <name>_Color / _Normal / _MetalSmooth textures anywhere in Assets
//     (including the "Road.fbm" folder Unity makes for embedded textures)
//   - fixes their import settings (8192 max size, normal map, sRGB off)
//   - for HDRP, makes a <name>_Mask texture (HDRP's smoothness format)
//   - creates <name>_Mat and assigns it
// It also hides the original (non-baked) road pieces if they were exported too.
//
// Usage:
//   1. Put this file in your Assets folder.
//   2. Select the road in the Hierarchy (the top object).
//   3. Menu: Tools -> Road -> Set Up Baked Road Materials.
// Ctrl+Z undoes the material assignment and hiding.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RoadMaterialSetup
{
    const string BakedSuffix = "_Baked";

    [MenuItem("Tools/Road/Set Up Baked Road Materials")]
    static void Run()
    {
        var root = Selection.activeGameObject;
        if (!root)
        {
            EditorUtility.DisplayDialog("Road setup", "Select the road in the Hierarchy first.", "OK");
            return;
        }

        string pipeline = DetectPipeline();
        int done = 0, hidden = 0;
        var missing = new List<string>();

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.gameObject.name;
            if (!n.EndsWith(BakedSuffix))
            {
                // original road piece exported by accident - hide it
                if (r.gameObject.activeSelf)
                {
                    Undo.RecordObject(r.gameObject, "Hide original road piece");
                    r.gameObject.SetActive(false);
                    hidden++;
                }
                continue;
            }

            string baseName = n.Substring(0, n.Length - BakedSuffix.Length);
            string colorPath = FindTexture(baseName + "_Color");
            string normalPath = FindTexture(baseName + "_Normal");
            string msPath = FindTexture(baseName + "_MetalSmooth");
            if (colorPath == null) { missing.Add(baseName); continue; }

            Configure(colorPath, srgb: true, normal: false);
            if (normalPath != null) Configure(normalPath, srgb: false, normal: true);
            if (msPath != null) Configure(msPath, srgb: false, normal: false);

            string folder = Path.GetDirectoryName(colorPath).Replace('\\', '/');
            var mat = BuildMaterial(pipeline, baseName, folder, colorPath, normalPath, msPath);

            Undo.RecordObject(r, "Assign road material");
            var mats = r.sharedMaterials;
            if (mats.Length == 0) mats = new Material[1];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            done++;
        }

        AssetDatabase.SaveAssets();

        string msg = $"Render pipeline: {pipeline}\nMaterials set up: {done}";
        if (hidden > 0) msg += $"\nHid {hidden} original (non-baked) road pieces.";
        if (missing.Count > 0)
            msg += "\n\nNo textures found for:\n  " + string.Join("\n  ", missing) +
                   "\n\nDrag the PNG files from the RoadTextures folder (next to your .blend file) into Unity and run this again.";
        if (done == 0 && missing.Count == 0)
            msg += "\n\nNo objects named \"..._Baked\" found under the selected object.";
        EditorUtility.DisplayDialog("Road setup", msg, "OK");
    }

    // ------------------------------------------------------------ helpers

    static string DetectPipeline()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null) return "Built-in";
        string tn = rp.GetType().Name;
        if (tn.Contains("HDRenderPipeline")) return "HDRP";
        if (tn.Contains("Universal")) return "URP";
        return "Built-in";
    }

    static string FindTexture(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    static void Configure(string path, bool srgb, bool normal)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.maxTextureSize < 8192) { imp.maxTextureSize = 8192; changed = true; }
        if (normal && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap; changed = true;
        }
        if (!normal && imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; changed = true; }
        if (changed) imp.SaveAndReimport();
    }

    static Texture2D Load(string path) =>
        path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    // HDRP mask map: R = metallic, G = ambient occlusion, B = detail mask, A = smoothness
    static string MakeHdrpMask(string msPath, string folder, string baseName)
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        src.LoadImage(File.ReadAllBytes(Path.GetFullPath(msPath)));
        var px = src.GetPixels32();
        bool hasAlpha = false;
        for (int i = 0; i < px.Length && !hasAlpha; i++)
            if (px[i].a < 250) hasAlpha = true;
        if (!hasAlpha)
            Debug.LogWarning($"RoadMaterialSetup: {baseName}_MetalSmooth has no smoothness data - using a matte surface.");
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(0, 255, 255, hasAlpha ? px[i].a : (byte)40);

        var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
        dst.SetPixels32(px);
        dst.Apply();
        string outPath = folder + "/" + baseName + "_Mask.png";
        File.WriteAllBytes(Path.GetFullPath(outPath), dst.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(src);
        UnityEngine.Object.DestroyImmediate(dst);

        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
        Configure(outPath, srgb: false, normal: false);
        return outPath;
    }

    static Material BuildMaterial(string pipeline, string baseName, string folder,
                                  string colorPath, string normalPath, string msPath)
    {
        Shader shader =
            pipeline == "HDRP" ? Shader.Find("HDRP/Lit") :
            pipeline == "URP" ? Shader.Find("Universal Render Pipeline/Lit") :
            Shader.Find("Standard");

        string matPath = folder + "/" + baseName + "_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else mat.shader = shader;

        var col = Load(colorPath);
        var nrm = Load(normalPath);
        var ms = Load(msPath);

        switch (pipeline)
        {
            case "HDRP":
                mat.SetTexture("_BaseColorMap", col);
                if (nrm) { mat.SetTexture("_NormalMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
                if (msPath != null)
                {
                    var mask = Load(MakeHdrpMask(msPath, folder, baseName));
                    mat.SetTexture("_MaskMap", mask);
                    mat.EnableKeyword("_MASKMAP");
                    mat.SetFloat("_SmoothnessRemapMin", 0f);
                    mat.SetFloat("_SmoothnessRemapMax", 1f);
                    mat.SetFloat("_MetallicRemapMin", 0f);
                    mat.SetFloat("_MetallicRemapMax", 0f);
                }
                mat.SetFloat("_Metallic", 0f);
                ValidateHdrp(mat);
                break;

            case "URP":
                mat.SetTexture("_BaseMap", col);
                mat.SetTexture("_MainTex", col);
                if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
                if (ms)
                {
                    mat.SetTexture("_MetallicGlossMap", ms);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    mat.SetFloat("_Smoothness", 1f);
                }
                break;

            default:
                mat.SetTexture("_MainTex", col);
                if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
                if (ms)
                {
                    mat.SetTexture("_MetallicGlossMap", ms);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    mat.SetFloat("_GlossMapScale", 1f);
                }
                break;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Lets HDRP set its own internal keywords/passes for the new textures.
    static void ValidateHdrp(Material mat)
    {
        try
        {
            var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Editor");
            var m = t?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static);
            m?.Invoke(null, new object[] { mat });
        }
        catch (Exception e)
        {
            Debug.LogWarning("RoadMaterialSetup: could not validate HDRP material: " + e.Message);
        }
    }
}
#endif