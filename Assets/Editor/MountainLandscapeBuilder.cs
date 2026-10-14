using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;

// Authored terrain, painted layers and prefab instances; no runtime mountain generation.
public static class MountainLandscapeBuilder
{
    public const string Folder = "Assets/Environment/Mountains";
    const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    const string Real = "Assets/Free Realistic Textures/Textures/";
    const string IceTextures = "Assets/Blink/Art/Textures/StylizedIceTextures/";
    const string LavaTextures = "Assets/Blink/Art/Textures/StylizedLavaTextures/";
    static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));
    static float IceWeight(float x, float z) => 1 - Smooth(.72f, 1.16f, new Vector2((x - 650) / 365, (z - 210) / 290).magnitude);
    static float VolcanoWeight(float x, float z) => 1 - Smooth(.62f, 1.05f, new Vector2((x - 845) / 285, (z - 675) / 275).magnitude);

    public static string Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play mode before editing mountains.");
        var terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("The world terrain is required.");
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/New Terrain.asset");
        if (source == null || source.terrainLayers.Length != 12) throw new InvalidOperationException("Expected the original 12-layer terrain.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Environment", "Mountains");
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/Refined mountain terrain.asset");
        if (data == null)
        {
            // Instantiate/CopySerialized shares terrain paint textures; copying the asset gives them their own storage.
            if (!AssetDatabase.CopyAsset("Assets/New Terrain.asset", Folder + "/Refined mountain terrain.asset")) throw new InvalidOperationException("Terrain copy failed.");
            data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/Refined mountain terrain.asset");
        }
        else Undo.RegisterCompleteObjectUndo(data, "Reshape background mountains");
        data.name = "Refined mountain terrain";
        Undo.RecordObjects(new UnityEngine.Object[] { terrain, terrain.GetComponent<TerrainCollider>() }, "Assign improved mountain terrain");
        terrain.terrainData = data; terrain.GetComponent<TerrainCollider>().terrainData = data;
        int resolution = source.heightmapResolution;
        var original = source.GetHeights(0, 0, resolution, resolution);
        var heights = (float[,])original.Clone();
        int changed = 0;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            float h = original[z, x] * source.size.y;
            float mask = Smooth(38, 95, h);
            if (mask == 0) continue;
            float wx = x * source.size.x / (resolution - 1), wz = z * source.size.z / (resolution - 1);
            float average = 0;
            for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++) average += original[Mathf.Clamp(z + dz * 2, 0, resolution - 1), Mathf.Clamp(x + dx * 2, 0, resolution - 1)] * source.size.y / 25f;
            float warp = (Mathf.PerlinNoise(wx * .006f + 12, wz * .006f + 37) - .5f) * 45;
            float ridge = 1 - Mathf.Abs(Mathf.PerlinNoise((wx + warp) * .018f + 8, wz * .014f + 41) * 2 - 1);
            float channel = Mathf.Pow(1 - Mathf.Abs(Mathf.PerlinNoise(wx * .035f + 84, (wz + warp) * .019f + 13) * 2 - 1), 8);
            float detail = (Mathf.PerlinNoise(wx * .085f + 7, wz * .085f + 29) - .5f) * 5;
            float shaped = Mathf.Lerp(h, average, .2f) + (ridge - .68f) * 37 - channel * 18 + detail;
            heights[z, x] = Mathf.Clamp01(Mathf.Lerp(h, shaped, mask) / source.size.y);
            changed++;
        }
        data.SetHeights(0, 0, heights);
        var layers = new List<TerrainLayer>(source.terrainLayers);
        layers[4] = Layer("Weathered mountain rock", Real + "Rough_Rock_1/Rough_Rock_1_Albedo.png", Real + "Rough_Rock_1/Rough_Rock_1_Normal.png", 12, .07f);
        layers[5] = Layer("Mountain summit snow", IceTextures + "Snow_4/Snow_4_BaseColor.png", IceTextures + "Snow_4/Snow_4_Normal.png", 10, .22f);
        layers[7] = Layer("Frosted mountain rock", Real + "Snowy_Rock_21/Snowy_Rock_21_Albedo.png", Real + "Snowy_Rock_21/Snowy_Rock_21_Normal.png", 12, .12f);
        layers.Add(Layer("Glacial mountain faces", IceTextures + "Ice_3/Ice_3_BaseColor.png", IceTextures + "Ice_3/Ice_3_Normal.png", 16, .48f));
        layers.Add(Layer("Mountain scree", Real + "Rocky_Ground_15/Rocky_Ground_15_Albedo.png", Real + "Rocky_Ground_15/Rocky_Ground_15_Normal.png", 9, .05f));
        layers.Add(Layer("Volcanic mountain basalt", LavaTextures + "Dry_Lava/Dry_Lava_BaseColor.png", LavaTextures + "Dry_Lava/Dry_Lava_Normal.png", 12, .06f));
        data.terrainLayers = layers.ToArray();
        int width = source.alphamapWidth, height = source.alphamapHeight;
        var oldPaint = source.GetAlphamaps(0, 0, width, height);
        var paint = new float[height, width, layers.Count];
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (width - 1f), v = z / (height - 1f);
            float wx = u * data.size.x, wz = v * data.size.z;
            float h = data.GetInterpolatedHeight(u, v), slope = data.GetSteepness(u, v);
            float originalHeight = source.GetInterpolatedHeight(u, v);
            float blend = Mathf.Max(Smooth(32, 72, originalHeight), Smooth(18, 38, originalHeight) * Smooth(24, 58, slope));
            for (int i = 0; i < 12; i++) paint[z, x, i] = oldPaint[z, x, i] * (1 - blend);
            if (blend == 0) { Normalize(paint, x, z, layers.Count); continue; }
            float cliff = Smooth(28, 65, slope), ice = IceWeight(wx, wz), volcanic = VolcanoWeight(wx, wz);
            ice *= 1 - volcanic;
            float snow = Mathf.Max(ice * Smooth(65, 175, h), (1 - volcanic) * Smooth(230, 295, h) * .65f) * (1 - Smooth(44, 82, slope));
            snow = Mathf.Clamp01(snow + ice * Smooth(190, 285, h) * .45f);
            float remaining = blend * (1 - snow);
            paint[z, x, 5] += blend * snow;
            paint[z, x, 14] += remaining * volcanic;
            remaining *= 1 - volcanic;
            float icyFace = ice * (.3f + .5f * cliff), frosted = ice * (1 - cliff) * .25f;
            paint[z, x, 12] += remaining * icyFace; paint[z, x, 7] += remaining * frosted;
            remaining *= 1 - icyFace - frosted;
            float grass = (1 - ice) * (1 - cliff) * (1 - Smooth(90, 145, h)) * .12f;
            paint[z, x, 1] += remaining * grass;
            paint[z, x, 4] += remaining * (.6f + .4f * cliff) * (1 - grass);
            paint[z, x, 13] += remaining * (.4f - .4f * cliff) * (1 - grass);
            Normalize(paint, x, z, layers.Count);
        }
        data.SetAlphamaps(0, 0, paint);
        var trees = new List<TreeInstance>();
        foreach (var tree in source.treeInstances)
        {
            var instance = tree; float u = tree.position.x, v = tree.position.z;
            float h = data.GetInterpolatedHeight(u, v), slope = data.GetSteepness(u, v);
            if (source.GetInterpolatedHeight(u, v) > 38 && (h > 150 || slope > 42 || (VolcanoWeight(u * data.size.x, v * data.size.z) > .5f && h > 65))) continue;
            instance.position.y = h / data.size.y; trees.Add(instance);
        }
        data.SetTreeInstances(trees.ToArray(), false);
        terrain.Flush(); Physics.SyncTransforms();
        var previous = GameObject.Find("Background mountain outcrops");
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        var root = new GameObject("Background mountain outcrops"); Undo.RegisterCreatedObjectUndo(root, "Mountain outcrops");
        var rock = RockMaterial("Weathered crags", layers[4], new Color(.9f,.91f,.93f), .07f);
        var frost = RockMaterial("Ice crags", layers[12], new Color(.65f,.85f,1), .4f);
        var snowMat = RockMaterial("Snow caps", layers[5], Color.white, .18f);
        var basalt = RockMaterial("Basalt crags", layers[14], new Color(.52f,.52f,.55f), .08f);
        int count = 0, fissures = 0;
        var random = new System.Random(1742);
        for (int z = 35; z < 990; z += 58)
        for (int x = 35; x < 990; x += 58)
        {
            float wx = x + (float)random.NextDouble() * 26, wz = z + (float)random.NextDouble() * 26;
            float u = wx / data.size.x, v = wz / data.size.z;
            float h = data.GetInterpolatedHeight(u, v), slope = data.GetSteepness(u, v);
            if (h < 65 || slope < 28 || slope > 76) continue;
            float icy = IceWeight(wx, wz), hot = VolcanoWeight(wx, wz);
            float size = 20 + (float)random.NextDouble() * 22;
            Material mat = hot > .4f ? basalt : icy > .35f ? frost : rock;
            var position = new Vector3(wx, h, wz) + terrain.transform.position;
            var crag = Crag(root.transform, "Cliff_basic", position, new Vector3(size, size * (1.15f + (float)random.NextDouble() * .6f), size * .7f), random.Next(360), mat);
            crag.name = (hot > .4f ? "Volcanic basalt" : icy > .35f ? "Glacial ridge" : "Weathered ridge") + " " + count;
            if (icy > .45f && hot < .4f)
            {
                SnowCrown(root.transform, crag, snowMat, count);
            }
            if (hot > .55f && fissures < 10)
            {
                Fissure(root.transform, terrain, position, fissures++);
            }
            count++;
        }
        terrain.heightmapPixelError = 2;
        terrain.Flush(); Physics.SyncTransforms();
        EditorUtility.SetDirty(data); EditorUtility.SetDirty(terrain); EditorUtility.SetDirty(terrain.GetComponent<TerrainCollider>());
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Assert.IsTrue(changed > 10000 && count > 20 && fissures > 0);
        return "Reshaped " + changed + " mountain samples; painted slope/height biomes; placed " + count + " LOD crags and " + fissures + " lava fissures. Original height sculpt retained at Assets/New Terrain.asset.";
    }

    static TerrainLayer Layer(string name, string albedo, string normal, float tile, float smooth)
    {
        string path = Folder + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo); layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        if (layer.diffuseTexture == null || layer.normalMapTexture == null) throw new InvalidOperationException("Missing mountain texture: " + albedo);
        layer.name = name; layer.tileSize = Vector2.one * tile; layer.normalScale = 1.3f; layer.smoothness = smooth;
        EditorUtility.SetDirty(layer); return layer;
    }

    static void Normalize(float[,,] paint, int x, int z, int count)
    {
        float total = 0; for (int i = 0; i < count; i++) total += paint[z,x,i];
        if (total <= .00001f) { paint[z,x,4] = 1; return; }
        for (int i = 0; i < count; i++) paint[z,x,i] /= total;
    }

    static Material RockMaterial(string name, TerrainLayer layer, Color tint, float smooth)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", layer.diffuseTexture); material.SetTexture("_BumpMap", layer.normalMapTexture);
        material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", smooth); material.SetFloat("_BumpScale", 1.25f);
        material.EnableKeyword("_NORMALMAP"); EditorUtility.SetDirty(material); return material;
    }

    static GameObject Crag(Transform parent, string prefab, Vector3 position, Vector3 size, float yaw, Material material)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rocks + prefab + ".prefab"), parent);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterial = material; }
        go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
        bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        go.transform.position += position - Vector3.up * size.y * .15f - bounds.center;
        go.transform.Rotate(0, yaw, 0, Space.Self);
        // A buried base joins the outcrop to the terrain instead of balancing a boulder on a slope.
        foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.gameObject.layer = LayerMask.NameToLayer("Ground");
        if (go.GetComponentsInChildren<Collider>().Length == 0)
        {
            var filter = go.GetComponentInChildren<MeshFilter>();
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            filter.gameObject.layer = LayerMask.NameToLayer("Ground");
        }
        return go;
    }

    static void SnowCrown(Transform root, GameObject crag, Material material, int index)
    {
        var filter = crag.GetComponentInChildren<MeshFilter>(); var source = filter.sharedMesh;
        var bounds = filter.GetComponent<Renderer>().bounds;
        var vertices = source.vertices; var normals = source.normals; var indices = source.triangles;
        var triangles = new List<int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = filter.transform.TransformPoint(vertices[i]);
            normals[i] = filter.transform.TransformDirection(normals[i]).normalized;
        }
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
            float y = (vertices[a].y + vertices[b].y + vertices[c].y) / 3;
            if (normal.y > .12f && y > bounds.center.y + bounds.size.y * .08f) triangles.AddRange(new[] { a, b, c });
        }
        if (triangles.Count == 0) return;
        for (int i = 0; i < vertices.Length; i++) vertices[i] += normals[i] * .055f;
        string path = Folder + "/Snow crown " + index + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.vertices = vertices; mesh.uv = source.uv; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var cap = new GameObject("Snow crown " + index); cap.transform.SetParent(root, false);
        cap.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = cap.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var group = crag.GetComponentInChildren<LODGroup>();
        if (group != null)
        {
            var lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++) { var renderers = new List<Renderer>(lods[i].renderers) { renderer }; lods[i].renderers = renderers.ToArray(); }
            group.SetLODs(lods);
        }
    }

    static void Fissure(Transform root, Terrain terrain, Vector3 position, int index)
    {
        var data = terrain.terrainData; var collider = terrain.GetComponent<TerrainCollider>();
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        float x = position.x, z = position.z; const int rows = 65;
        for (int i = 0; i <= rows; i++)
        {
            float t = i / (float)rows;
            float px = x + Mathf.Sin(t * 9 + index) * 3, pz = z + (t - .5f) * 54;
            float width = (.3f + .5f * Mathf.PerlinNoise(t * 11 + index, 14)) * Mathf.Sin(t * Mathf.PI);
            for (int j = 0; j < 3; j++)
            {
                float sx = px + (j - 1) * width;
                float y = data.GetInterpolatedHeight(Mathf.Clamp01(sx / data.size.x), Mathf.Clamp01(pz / data.size.z));
                if (collider.Raycast(new Ray(new Vector3(sx, data.size.y + 20, pz), Vector3.down), out var hit, data.size.y + 40)) y = hit.point.y;
                vertices.Add(new Vector3(sx, y + .14f + (j == 1 ? .08f : 0), pz)); uv.Add(new Vector2(j * .5f, t * 12));
                if (i < rows && j < 2) { int k = i * 3 + j; triangles.AddRange(new[] { k, k + 3, k + 1, k + 1, k + 3, k + 4 }); }
            }
        }
        string path = Folder + "/Lava fissure " + index + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var go = new GameObject("Molten mountain fissure " + index); go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/LavaCave/Flowing Lava.mat");
        go.AddComponent<LavaFlow>();
        var lightGo = new GameObject("Fissure glow"); lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.position = new Vector3(x, data.GetInterpolatedHeight(x / data.size.x, z / data.size.z) + 3, z);
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1,.22f,.025f); light.range = 34; light.intensity = 5; light.shadows = LightShadows.None;
    }
}
