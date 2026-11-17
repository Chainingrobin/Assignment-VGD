using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Frozen Reach on the lake island, replacing the flat snow plateau with terrain worth
// moving and fighting through:
//  - two glacier ridges with walkable flanks and passes, stepped ice terraces in the east,
//    three frozen ponds, and packed-snow trails from the landing to the arena;
//  - the Frozen Amphitheater: a flat icy floor inside a raised snow ring with three entrances,
//    and four ice pillars for cover against the Ice Guardian's shard volleys;
//  - ice spires, boulder clusters (with colliders) for cover in the field, thinned blue groves
//    with firs, and falling snow.
// Same snapshot/restore rebuild as the other biome builders; run after the volcano builder.
public static class SnowBiomeBuilder
{
    private const string Folder = "Assets/Environment/Snow";
    private const string BaselinePath = Folder + "/Snow baseline.bytes";
    private const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    private const string Nature = "Assets/Idyllic Fantasy Nature/Prefabs/";
    private const string IceSet = "Assets/Blink/Art/Textures/StylizedIceTextures/";

    public static readonly Vector2 ArenaCenter = new(650f, 245f);
    public const float ArenaRadius = 20f;
    private static readonly Rect Area = Rect.MinMaxRect(440f, 150f, 870f, 335f);
    private static readonly Vector2 Landing = new(655f, 328f);
    private static readonly Vector2[] RidgeWest = { new(470f, 305f), new(520f, 275f), new(560f, 235f), new(585f, 190f) };
    private static readonly Vector2[] RidgeEast = { new(715f, 185f), new(760f, 225f), new(800f, 262f), new(845f, 300f) };
    private static readonly Vector2[] Ponds = { new(555f, 300f), new(728f, 296f), new(805f, 195f) };
    private const float PondRadius = 9f;
    private static readonly Vector2[] TrailToArena = { new(655f, 326f), new(652f, 298f), new(650f, 270f) };
    private static readonly Vector2[] TrailEast = { new(668f, 248f), new(705f, 252f), new(745f, 262f), new(790f, 280f) };
    private static readonly Vector2[] TrailWest = { new(630f, 248f), new(600f, 262f), new(565f, 282f) };
    private static readonly float[] Entrances = { 90f, 0f, 180f }; // degrees: north to the landing, east, west

    private static Terrain terrain;

    // Ground this builder reshapes; the mountain checks leave it to this builder.
    internal static bool Sculpts(float x, float z) => Area.Contains(new Vector2(x, z));
    private static float arenaFloor;

    [MenuItem("Wulang/Snow/Rebuild snow biome")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Environment", "Snow");
        terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        TerrainBaseline.Restore(terrain, Area, BaselinePath);
        arenaFloor = Average(ArenaCenter, 10f);
        Sculpt(data);
        Paint(data);
        Trees(data);
        Details(data);
        terrain.Flush();
        Physics.SyncTransforms();
        Props(world);
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        AssetDatabase.SaveAssets();
        Debug.Log("SnowBiomeBuilder: Frozen Reach and the Frozen Amphitheater built.");
    }

    private static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, high, value));
    private static float Noise(Vector2 p, float frequency, float seed = 0f) => Mathf.PerlinNoise(p.x * frequency + seed, p.y * frequency + seed * 1.3f);
    private static float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
    private static float Line(Vector2[] line, Vector2 p) => ForestBiomeBuilder.DistanceToLine(line, p);
    private static float Trail(Vector2 p) => Mathf.Min(Line(TrailToArena, p), Mathf.Min(Line(TrailEast, p), Line(TrailWest, p)));
    private static float NearestPond(Vector2 p) => Ponds.Min(c => Vector2.Distance(p, c));

    private static float Average(Vector2 center, float radius)
    {
        float sum = 0f; int count = 0;
        for (float x = -radius; x <= radius; x += 2f)
            for (float z = -radius; z <= radius; z += 2f)
                if (x * x + z * z <= radius * radius) { sum += Ground(center + new Vector2(x, z)); count++; }
        return sum / count;
    }

    // Only the island plateau is reshaped: never the shoreline the frozen lake meets, nor the
    // landing where the region begins, nor the mountain walls behind.
    private static float Edit(Vector2 p, float originalHeight)
    {
        float edge = Smooth(0f, 16f, Mathf.Min(Mathf.Min(p.x - Area.xMin, Area.xMax - p.x), Mathf.Min(p.y - Area.yMin, Area.yMax - p.y)));
        float plateau = Smooth(18.5f, 21.5f, originalHeight) * (1f - Smooth(40f, 60f, originalHeight));
        float landing = Smooth(10f, 18f, Vector2.Distance(p, Landing));
        return edge * plateau * landing;
    }

    private static float Ridge(Vector2[] line, Vector2 p, float height, float width)
    {
        float d = Line(line, p);
        float crest = 0.8f + 0.4f * Noise(p, 0.04f, 13f);
        return height * crest * Mathf.Exp(-d * d / (width * width));
    }

    private static void Sculpt(TerrainData data)
    {
        float cell = TerrainBaseline.HeightCell(data);
        var region = TerrainBaseline.Region(Area, data.heightmapResolution, cell);
        var heights = data.GetHeights(region.x, region.y, region.width, region.height);
        for (int z = 0; z < region.height; z++)
            for (int x = 0; x < region.width; x++)
            {
                var p = new Vector2((region.x + x) * cell, (region.y + z) * cell);
                float original = heights[z, x] * data.size.y;
                float edit = Edit(p, original);
                if (edit <= 0f) continue;
                float h = original;
                // Wind-sculpted drifts across the whole plateau.
                h += (Noise(p, 0.018f) - 0.5f) * 5f + (Noise(p, 0.05f, 40f) - 0.5f) * 1.5f;
                // Two glacier ridges with a low pass cut through each, so they route rather than wall off.
                float passWest = 1f - 0.75f * (1f - Smooth(8f, 18f, Vector2.Distance(p, new Vector2(540f, 255f))));
                float passEast = 1f - 0.75f * (1f - Smooth(8f, 18f, Vector2.Distance(p, new Vector2(780f, 243f))));
                h += Ridge(RidgeWest, p, 8f, 13f) * passWest + Ridge(RidgeEast, p, 9f, 14f) * passEast;
                // Stepped ice terraces in the east: climb ledge by ledge.
                float terraces = Smooth(745f, 790f, p.x) * (1f - Smooth(310f, 330f, p.y)) * Smooth(160f, 185f, p.y);
                h = Mathf.Lerp(h, Mathf.Floor(h / 2.2f) * 2.2f + 0.4f, terraces * 0.85f);
                // Frozen ponds sit in shallow basins.
                float pond = NearestPond(p);
                h = Mathf.Lerp(h, Ground(Ponds.OrderBy(c => Vector2.Distance(p, c)).First()) - 1.2f, 1f - Smooth(PondRadius - 2f, PondRadius + 5f, pond));
                // The Frozen Amphitheater: a flat floor ringed by a raised snow berm with three gaps.
                float r = Vector2.Distance(p, ArenaCenter);
                float angle = Mathf.Atan2(p.y - ArenaCenter.y, p.x - ArenaCenter.x) * Mathf.Rad2Deg;
                float gap = Entrances.Max(e => 1f - Smooth(9f, 16f, Mathf.Abs(Mathf.DeltaAngle(angle, e))));
                float berm = (1f - Smooth(2f, 5f, Mathf.Abs(r - (ArenaRadius + 5f)))) * 3.8f * (1f - gap);
                h = Mathf.Lerp(h, arenaFloor, 1f - Smooth(ArenaRadius, ArenaRadius + 4f, r));
                if (berm > 0.01f) h = Mathf.Max(h, arenaFloor + berm);
                // Trails stay smooth and level enough to run.
                h = Mathf.Lerp(h, Mathf.Lerp(original, h, 0.3f), 1f - Smooth(2.5f, 6f, Trail(p)));
                heights[z, x] = Mathf.Clamp01(Mathf.Lerp(original, h, edit) / data.size.y);
            }
        data.SetHeights(region.x, region.y, heights);
        terrain.Flush();
    }

    // Copies of the pack's ice and snow layers with a larger tile, so the ground doesn't repeat.
    private static TerrainLayer Layer(string name, string set, float tile)
    {
        string path = $"{Folder}/{name}.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        var source = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{IceSet}{set}/{set}.terrainlayer");
        layer.diffuseTexture = source.diffuseTexture;
        layer.normalMapTexture = source.normalMapTexture;
        layer.tileSize = Vector2.one * tile;
        layer.smoothness = set == "Ice_3" ? 0.7f : 0.15f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void Paint(TerrainData data)
    {
        var layers = data.terrainLayers.ToList();
        int Index(TerrainLayer layer) { int i = layers.IndexOf(layer); if (i < 0) { layers.Add(layer); i = layers.Count - 1; } return i; }
        int packed = Index(Layer("Packed snow", "Snow_4", 7f));
        int ice = Index(Layer("Glacier ice", "Ice_3", 9f));
        int fresh = layers.FindIndex(l => l != null && l.name == "Mixed_Snow_1");
        int rock = layers.FindIndex(l => l != null && l.name == "Frosted mountain rock");
        if (fresh < 0) fresh = packed;
        if (rock < 0) rock = packed;
        data.terrainLayers = layers.ToArray();
        float cell = TerrainBaseline.AlphaCell(data);
        var region = TerrainBaseline.Region(Area, data.alphamapResolution, cell);
        var maps = data.GetAlphamaps(region.x, region.y, region.width, region.height);
        int count = layers.Count;
        var target = new float[count];
        for (int z = 0; z < region.height; z++)
            for (int x = 0; x < region.width; x++)
            {
                var p = new Vector2((region.x + x + 0.5f) * cell, (region.y + z + 0.5f) * cell);
                float u = p.x / data.size.x, v = p.y / data.size.z;
                float edit = Mathf.Clamp01(Edit(p, data.GetInterpolatedHeight(u, v)) * 1.5f);
                if (edit <= 0f) continue;
                Array.Clear(target, 0, count);
                float rest = 1f;
                void Put(int layer, float amount) { amount = Mathf.Clamp01(amount) * rest; target[layer] += amount; rest -= amount; }
                Put(rock, Smooth(30f, 42f, data.GetSteepness(u, v)));
                Put(ice, 1f - Smooth(PondRadius - 1f, PondRadius + 1f, NearestPond(p)));
                float r = Vector2.Distance(p, ArenaCenter);
                Put(ice, (1f - Smooth(ArenaRadius - 6f, ArenaRadius - 2f, r)) * 0.75f);
                Put(packed, 1f - Smooth(1.8f, 3.8f, Trail(p)));
                Put(packed, (1f - Smooth(ArenaRadius, ArenaRadius + 2f, r)));
                Put(fresh, 1f);
                for (int l = 0; l < count; l++) maps[z, x, l] = Mathf.Lerp(maps[z, x, l], target[l], edit);
                float sum = 0f;
                for (int l = 0; l < count; l++) sum += maps[z, x, l];
                for (int l = 0; l < count; l++) maps[z, x, l] /= Mathf.Max(sum, 1e-4f);
            }
        data.SetAlphamaps(region.x, region.y, maps);
    }

    // Meadow grass doesn't grow through snow: clear it across the edited island.
    private static void Details(TerrainData data)
    {
        float cell = TerrainBaseline.DetailCell(data);
        var region = TerrainBaseline.Region(Area, data.detailResolution, cell);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var detail = data.GetDetailLayer(region.x, region.y, region.width, region.height, layer);
            for (int z = 0; z < region.height; z++)
                for (int x = 0; x < region.width; x++)
                {
                    var p = new Vector2((region.x + x + 0.5f) * cell, (region.y + z + 0.5f) * cell);
                    float edit = Mathf.Clamp01(Edit(p, Ground(p)) * 2f);
                    detail[z, x] = Mathf.RoundToInt(detail[z, x] * (1f - edit));
                }
            data.SetDetailLayer(region.x, region.y, layer, detail);
        }
    }

    private static int Prototype(List<TreePrototype> prototypes, string prefab)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        int i = prototypes.FindIndex(t => t.prefab == asset);
        if (i < 0) { prototypes.Add(new TreePrototype { prefab = asset }); i = prototypes.Count - 1; }
        return i;
    }

    private static bool KeepClear(Vector2 p) =>
        Trail(p) < 5f || Vector2.Distance(p, ArenaCenter) < ArenaRadius + 9f || NearestPond(p) < PondRadius + 2f || Vector2.Distance(p, Landing) < 16f;

    // Blue and purple groves on the ridges and terraces, with dark firs; the arena, trails and
    // ponds stay open. Trees outside the island are left exactly as they are.
    private static void Trees(TerrainData data)
    {
        var prototypes = data.treePrototypes.ToList();
        int[] blue = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}BroadleafTree_0{i}_Blue.prefab")).ToArray();
        int[] purple = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}BroadleafTree_0{i}_Purple.prefab")).ToArray();
        int[] willow = Enumerable.Range(1, 3).Select(i => Prototype(prototypes, $"{Nature}WillowTree_0{i}_Blue.prefab")).ToArray();
        int[] fir = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}Fir_0{i}.prefab")).ToArray();
        data.treePrototypes = prototypes.ToArray();
        var kept = data.treeInstances.Where(t =>
        {
            var p = new Vector2(t.position.x * data.size.x, t.position.z * data.size.z);
            return !Area.Contains(p) || Edit(p, Ground(p)) < 0.4f;
        }).ToList();
        var random = new System.Random(1337);
        float Rand() => (float)random.NextDouble();
        int Pick(int[] set) => set[random.Next(set.Length)];
        const float step = 8f;
        for (float x = Area.xMin; x < Area.xMax; x += step)
            for (float z = Area.yMin; z < Area.yMax; z += step)
            {
                var p = new Vector2(x + Rand() * step, z + Rand() * step);
                if (Edit(p, Ground(p)) < 0.4f || KeepClear(p)) continue;
                if (data.GetSteepness(p.x / data.size.x, p.y / data.size.z) > 32f) continue;
                // Groves cluster: dense where the noise is high, open snowfields between.
                if (Noise(p, 0.03f, 5f) < 0.5f || Rand() > 0.6f) continue;
                float r = Rand();
                int kind = NearestPond(p) < PondRadius + 10f ? Pick(willow) : r < 0.35f ? Pick(fir) : r < 0.7f ? Pick(blue) : Pick(purple);
                float scale = 0.9f + Rand() * 0.45f;
                kept.Add(new TreeInstance
                {
                    position = new Vector3(p.x / data.size.x, 0f, p.y / data.size.z), widthScale = scale, heightScale = scale * (0.9f + Rand() * 0.25f),
                    rotation = Rand() * Mathf.PI * 2f, prototypeIndex = kind, color = Color.white, lightmapColor = Color.white,
                });
            }
        data.SetTreeInstances(kept.ToArray(), true);
    }

    private static GameObject Place(Transform parent, string path, Vector2 p, Quaternion rotation, Vector3 scale, float sink)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(new Vector3(p.x, Ground(p) - sink, p.y), rotation);
        instance.transform.localScale = scale;
        // The rock pack ships without colliders; cover has to block movement and shots.
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            if (filter.name.Contains("lod0") && filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<MeshCollider>();
        return instance;
    }

    private static Material PondMaterial()
    {
        string path = $"{Folder}/Frozen pond.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Frozen pond" };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{IceSet}Ice_3/Ice_3_BaseColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{IceSet}Ice_3/Ice_3_Normal.png"));
        material.EnableKeyword("_NORMALMAP");
        material.SetColor("_BaseColor", new Color(0.75f, 0.9f, 1f));
        material.SetFloat("_Smoothness", 0.9f);
        material.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Props(UnityEngine.SceneManagement.Scene world)
    {
        var old = world.GetRootGameObjects().FirstOrDefault(g => g.name == "Snow biome");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject("Snow biome").transform;
        var random = new System.Random(2718);
        float Rand() => (float)random.NextDouble();
        Quaternion Yaw() => Quaternion.Euler(0f, Rand() * 360f, 0f);

        // The amphitheater: four tall ice pillars as cover, crystal shards along the berm.
        var arena = new GameObject("Frozen Amphitheater").transform;
        arena.SetParent(root, false);
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + i * 90f) * Mathf.Deg2Rad;
            var p = ArenaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11f;
            Place(arena, $"{Rocks}Cliff_ice.prefab", p, Yaw(), new Vector3(2.6f, 3.6f, 2.6f), 0.6f).name = "Ice pillar " + i;
        }
        for (int i = 0; i < 18; i++)
        {
            float a = Rand() * Mathf.PI * 2f;
            float angle = a * Mathf.Rad2Deg;
            if (Entrances.Any(e => Mathf.Abs(Mathf.DeltaAngle(angle, e)) < 16f)) continue;
            var p = ArenaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ArenaRadius + 5f + Rand() * 2f);
            float s = 1.2f + Rand() * 1.4f;
            Place(arena, $"{Rocks}Rock_ice.prefab", p, Quaternion.Euler(Rand() * 25f - 12f, Rand() * 360f, Rand() * 25f - 12f), new Vector3(s, s * (1.6f + Rand()), s), 0.4f).name = "Berm crystal";
        }

        // Frozen ponds: an ice sheet over each basin.
        var pondMaterial = PondMaterial();
        foreach (var pond in Ponds)
        {
            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sheet.name = "Frozen pond";
            sheet.transform.SetParent(root, false);
            sheet.transform.position = new Vector3(pond.x, Ground(pond) + 0.55f, pond.y);
            sheet.transform.localScale = new Vector3(PondRadius * 2.1f, 0.05f, PondRadius * 2.1f);
            sheet.GetComponent<MeshRenderer>().sharedMaterial = pondMaterial;
            UnityEngine.Object.DestroyImmediate(sheet.GetComponent<Collider>());
            sheet.AddComponent<MeshCollider>();
        }

        // Ice spires and boulder clusters across the field: landmarks and cover between groves.
        var field = new GameObject("Ice field").transform;
        field.SetParent(root, false);
        for (int placed = 0, tries = 0; placed < 22 && tries < 400; tries++)
        {
            var p = new Vector2(Mathf.Lerp(Area.xMin + 15f, Area.xMax - 15f, Rand()), Mathf.Lerp(Area.yMin + 15f, Area.yMax - 15f, Rand()));
            if (Edit(p, Ground(p)) < 0.6f || KeepClear(p)) continue;
            bool spire = Rand() < 0.4f;
            float s = spire ? 1.5f + Rand() : 1.8f + Rand() * 1.6f;
            var scale = spire ? new Vector3(s, s * (3f + Rand() * 1.5f), s) : Vector3.one * s;
            Place(field, spire ? $"{Rocks}Cliff_ice.prefab" : (Rand() < 0.5f ? $"{Rocks}Rock_snow.prefab" : $"{Rocks}Plate_snow.prefab"), p,
                Quaternion.Euler(spire ? Rand() * 16f - 8f : 0f, Rand() * 360f, spire ? Rand() * 16f - 8f : 0f), scale, 0.5f).name = spire ? "Ice spire" : "Snow boulder";
            placed++;
        }
        // Snow-capped rocks along both ridge crests.
        foreach (var ridge in new[] { RidgeWest, RidgeEast })
            for (int i = 0; i < 7; i++)
            {
                int leg = random.Next(ridge.Length - 1);
                var p = Vector2.Lerp(ridge[leg], ridge[leg + 1], Rand()) + new Vector2(Rand() - 0.5f, Rand() - 0.5f) * 8f;
                if (KeepClear(p)) continue;
                float s = 2f + Rand() * 2f;
                Place(field, $"{Rocks}Cliff_snow.prefab", p, Yaw(), new Vector3(s, s * 1.2f, s), 0.8f).name = "Ridge rock";
            }

        // Gentle snowfall over the island.
        var snow = new GameObject("Snowfall");
        snow.transform.SetParent(root, false);
        snow.transform.position = new Vector3(655f, 70f, 245f);
        var system = snow.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = true;
        main.startLifetime = 14f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.maxParticles = 4000;
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission;
        emission.rateOverTime = 260f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Area.width, 1f, Area.height);
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.2f;
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Effects/Snowflake.mat");
    }
}
