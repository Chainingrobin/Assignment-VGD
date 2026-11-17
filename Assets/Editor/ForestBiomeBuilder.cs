using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Forest biome on the shared terrain: the green woods north of the armory and the
// pink blossom grove down to the lake become one layered forest built for the tutorial.
//  - Terrain: rolling hills, a stony creek, a pond, and the Blossom Glade: a flat, gently dished
//    arena ringed by standing stones where the Earth Guardian (the tutorial boss) waits.
//  - Trails: armory -> past the vampires -> the glade -> along the shore to the ice statue.
//  - Paint, zoned trees (blossoms, willows by water, dense broadleaf and fir up north) and props
//    (cover rocks for boulder throws, logs, cliffs, the guardian's shrine, water, falling petals).
// The first build snapshots the untouched region; every build restores it first, so rebuilding is
// repeatable. Run after the volcano builder (its baseline restores all terrain trees).
public static class ForestBiomeBuilder
{
    private const string Folder = "Assets/Environment/Forest";
    private const string BaselinePath = Folder + "/Forest baseline.bytes";
    private const string Nature = "Assets/Idyllic Fantasy Nature/Prefabs/";
    private const string NatureDemo = "Assets/Idyllic Fantasy Nature/Demo/Prefabs/";

    public static readonly Vector2 ArenaCenter = new(485f, 442f);
    public const float ArenaRadius = 22f;
    private static readonly Vector2 PondCenter = new(428f, 448f);
    private const float PondRadius = 13f;
    private static readonly Rect Area = Rect.MinMaxRect(300f, 395f, 640f, 880f);
    private static readonly Vector2 Armory = new(560f, 567f);
    private static readonly Vector2 Spawn = new(566f, 604f);
    private static readonly Vector2[] TrailToArena = { new(556f, 552f), new(538f, 524f), new(518f, 496f), new(501f, 468f) };
    private static readonly Vector2[] TrailToLake = { new(505f, 424f), new(540f, 410f), new(585f, 400f), new(632f, 392f) };
    private static readonly Vector2[] Creek = { new(335f, 830f), new(368f, 760f), new(396f, 690f), new(420f, 622f), new(436f, 560f), new(440f, 505f), new(434f, 466f) };
    private static readonly Vector2 Knoll = new(398f, 705f);

    private static Terrain terrain;

    // Ground this builder reshapes; the mountain checks leave it to this builder.
    internal static bool Sculpts(float x, float z) => Area.Contains(new Vector2(x, z));
    private static float arenaFloor, pondSurface;

    [MenuItem("Wulang/Forest/Rebuild forest biome")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Environment", "Forest");
        terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        TerrainBaseline.Restore(terrain, Area, BaselinePath);
        arenaFloor = Average(ArenaCenter, 9f) - 0.2f;
        pondSurface = Average(PondCenter, 6f) - 0.5f;
        Sculpt(data);
        Paint(data);
        Trees(data, data.treeInstances);
        Details(data);
        terrain.Flush();
        Physics.SyncTransforms();
        Props(world);
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        AssetDatabase.SaveAssets();
        Debug.Log("ForestBiomeBuilder: forest, blossom grove and the Blossom Glade arena built.");
    }

    // ---------- shapes ----------

    private static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, high, value));
    private static float Noise(Vector2 p, float frequency, float seed = 0f) => Mathf.PerlinNoise(p.x * frequency + seed, p.y * frequency + seed * 1.7f);
    private static float Fbm(Vector2 p) => Noise(p, 0.011f) * 0.6f + Noise(p, 0.027f, 31f) * 0.3f + Noise(p, 0.06f, 77f) * 0.1f;

    public static float DistanceToLine(Vector2[] line, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < line.Length - 1; i++)
        {
            var a = line[i]; var b = line[i + 1];
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
        }
        return best;
    }

    private static float Trail(Vector2 p) => Mathf.Min(DistanceToLine(TrailToArena, p), DistanceToLine(TrailToLake, p));

    // How much this builder may change a point: full inside the forest, fading at the region's
    // edge, and zero around the armory, the spawn arch and the volcano's burnt fringe.
    private static float Edit(Vector2 p)
    {
        float edge = Smooth(0f, 18f, Mathf.Min(Mathf.Min(p.x - Area.xMin, Area.xMax - p.x), Mathf.Min(p.y - Area.yMin, Area.yMax - p.y)));
        float armory = Smooth(34f, 46f, Vector2.Distance(p, Armory));
        float spawn = Smooth(14f, 22f, Vector2.Distance(p, Spawn));
        float volcano = 1f - Smooth(598f, 622f, p.x);
        return edge * armory * spawn * volcano;
    }

    private static float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;

    private static float Average(Vector2 center, float radius)
    {
        float sum = 0f; int count = 0;
        for (float x = -radius; x <= radius; x += 2f)
            for (float z = -radius; z <= radius; z += 2f)
                if (x * x + z * z <= radius * radius) { sum += Ground(center + new Vector2(x, z)); count++; }
        return sum / count;
    }

    // ---------- terrain ----------

    private static RectInt Region(int resolution, float cell) => TerrainBaseline.Region(Area, resolution, cell);

    private static void Sculpt(TerrainData data)
    {
        float cell = data.size.x / (data.heightmapResolution - 1);
        var region = Region(data.heightmapResolution, cell);
        var heights = data.GetHeights(region.x, region.y, region.width, region.height);
        for (int z = 0; z < region.height; z++)
            for (int x = 0; x < region.width; x++)
            {
                var p = new Vector2((region.x + x) * cell, (region.y + z) * cell);
                float edit = Edit(p);
                if (edit <= 0f) continue;
                float h = heights[z, x] * data.size.y;
                float original = h;
                // Rolling woodland hills, taller in the northern woods than in the blossom grove.
                float woods = Smooth(500f, 640f, p.y);
                h += (Fbm(p) - 0.5f) * Mathf.Lerp(3f, 9f, woods);
                h += Mathf.Max(0f, 1f - Vector2.Distance(p, Knoll) / 38f) * 7f;
                // A stony creek winding south to the pond.
                float creek = DistanceToLine(Creek, p);
                h -= 1.4f * (1f - Smooth(1.5f, 6f, creek));
                // The pond: a shallow bowl under the water surface.
                float pond = Vector2.Distance(p, PondCenter);
                h = Mathf.Lerp(h, pondSurface - 1.3f * (1f - Mathf.Clamp01(pond / PondRadius)), 1f - Smooth(PondRadius - 3f, PondRadius + 6f, pond));
                // The Blossom Glade: a broad, gently dished floor for the guardian fight.
                float arena = Vector2.Distance(p, ArenaCenter);
                float dish = arenaFloor - 0.45f * (1f - Mathf.Clamp01(arena / ArenaRadius) * Mathf.Clamp01(arena / ArenaRadius));
                h = Mathf.Lerp(h, dish, 1f - Smooth(ArenaRadius, ArenaRadius + 10f, arena));
                // Trails stay near the original ground so they read as worn paths through the hills.
                h = Mathf.Lerp(h, Mathf.Lerp(original, h, 0.25f), 1f - Smooth(2.5f, 7f, Trail(p)));
                heights[z, x] = Mathf.Clamp01(Mathf.Lerp(original, h, edit) / data.size.y);
            }
        data.SetHeights(region.x, region.y, heights);
        terrain.Flush();
    }

    private static TerrainLayer Layer(string name)
    {
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"Assets/Idyllic Fantasy Nature/Terrain Layer/{name}.terrainlayer");
        if (layer == null) throw new FileNotFoundException(name);
        return layer;
    }

    private static void Paint(TerrainData data)
    {
        var layers = data.terrainLayers.ToList();
        int Index(TerrainLayer layer) { int i = layers.IndexOf(layer); if (i < 0) { layers.Add(layer); i = layers.Count - 1; } return i; }
        int grass = Index(Layer("Grass_Layer")), forest = Index(Layer("Forest_Layer")), dirt = Index(Layer("Dirt_Layer"));
        int stony = Index(Layer("Dirt_Stone_Layer")), cobble = Index(Layer("Cobblestone_Layer")), sand = Index(Layer("Sand_Layer"));
        int rock = Index(Layer("Rock_Layer"));
        data.terrainLayers = layers.ToArray();
        float cell = data.size.x / data.alphamapResolution;
        var region = Region(data.alphamapResolution, cell);
        var maps = data.GetAlphamaps(region.x, region.y, region.width, region.height);
        int count = layers.Count;
        var target = new float[count];
        for (int z = 0; z < region.height; z++)
            for (int x = 0; x < region.width; x++)
            {
                var p = new Vector2((region.x + x + 0.5f) * cell, (region.y + z + 0.5f) * cell);
                float edit = Edit(p);
                if (edit <= 0f) continue;
                Array.Clear(target, 0, count);
                float slope = data.GetSteepness(p.x / data.size.x, p.y / data.size.z);
                float steep = Smooth(24f, 38f, slope), rest = 1f;
                void Put(int layer, float amount) { amount = Mathf.Clamp01(amount) * rest; target[layer] += amount; rest -= amount; }
                Put(rock, steep);
                float trail = Trail(p);
                Put(cobble, 1f - Smooth(1.6f, 2.6f, trail));
                Put(dirt, 1f - Smooth(2.6f, 4.8f, trail));
                float pond = Vector2.Distance(p, PondCenter);
                Put(stony, 1f - Smooth(PondRadius - 2f, PondRadius, pond));
                Put(sand, 1f - Smooth(PondRadius, PondRadius + 4f, pond));
                Put(stony, 1f - Smooth(1.2f, 3f, DistanceToLine(Creek, p)));
                float arena = Vector2.Distance(p, ArenaCenter);
                // A worn ring marks the fighting floor; the centre is packed earth.
                Put(stony, (1f - Smooth(1.2f, 2.6f, Mathf.Abs(arena - (ArenaRadius - 1.5f)))) * 0.8f);
                Put(dirt, (1f - Smooth(4f, 7f, arena)) * 0.7f);
                float woods = Smooth(520f, 620f, p.y) * Smooth(0.35f, 0.6f, Noise(p, 0.05f, 9f) + 0.25f);
                Put(forest, woods * Smooth(ArenaRadius, ArenaRadius + 6f, arena));
                Put(grass, 1f);
                for (int l = 0; l < count; l++) maps[z, x, l] = Mathf.Lerp(maps[z, x, l], target[l], edit);
                float sum = 0f;
                for (int l = 0; l < count; l++) sum += maps[z, x, l];
                for (int l = 0; l < count; l++) maps[z, x, l] /= Mathf.Max(sum, 1e-4f);
            }
        data.SetAlphamaps(region.x, region.y, maps);
    }

    // ---------- vegetation ----------

    private static int Prototype(List<TreePrototype> prototypes, string prefab)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (asset == null) throw new FileNotFoundException(prefab);
        int i = prototypes.FindIndex(t => t.prefab == asset);
        if (i < 0) { prototypes.Add(new TreePrototype { prefab = asset }); i = prototypes.Count - 1; }
        return i;
    }

    private static bool KeepClear(Vector2 p, float margin)
    {
        if (Trail(p) < 5f + margin) return true;
        if (Vector2.Distance(p, ArenaCenter) < ArenaRadius + 4f + margin) return true;
        if (Vector2.Distance(p, PondCenter) < PondRadius + 2f + margin) return true;
        if (DistanceToLine(Creek, p) < 2.5f + margin) return true;
        if (Vector2.Distance(p, Knoll) < 12f) return true;
        return false;
    }

    private static void Trees(TerrainData data, TreeInstance[] baseline)
    {
        var prototypes = data.treePrototypes.ToList();
        int[] blossom = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}BlossomTree_0{i}.prefab")).ToArray();
        int[] green = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}BroadleafTree_0{i}_Green.prefab")).ToArray();
        int[] red = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}BroadleafTree_0{i}_Red.prefab")).ToArray();
        int[] blue = Enumerable.Range(1, 3).Select(i => Prototype(prototypes, $"{Nature}BroadleafTree_0{i}_Blue.prefab")).ToArray();
        int[] fir = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}Fir_0{i}.prefab")).ToArray();
        int[] willowPink = Enumerable.Range(1, 5).Select(i => Prototype(prototypes, $"{Nature}WillowTree_0{i}_Pink.prefab")).ToArray();
        int[] willowGreen = Enumerable.Range(1, 3).Select(i => Prototype(prototypes, $"{Nature}WillowTree_0{i}_Green.prefab")).ToArray();
        // Understory reuses the terrain's existing no-collision bushes and flower prototypes.
        int[] understory = prototypes.Select((t, i) => (t, i)).Where(e => e.t.prefab != null &&
            (e.t.prefab.name.Contains("NoCollision") || e.t.prefab.name.StartsWith("Flower"))).Select(e => e.i).ToArray();
        data.treePrototypes = prototypes.ToArray();

        // Trees outside the edited forest (and in the protected armory/spawn/volcano edge) stay;
        // everything inside is regenerated, so rebuilding never piles trees up.
        var kept = baseline.Where(t =>
        {
            var p = new Vector2(t.position.x * data.size.x, t.position.z * data.size.z);
            return !Area.Contains(p) || Edit(p) < 0.5f;
        }).ToList();
        var random = new System.Random(4242);
        float Rand() => (float)random.NextDouble();
        int Pick(int[] set) => set[random.Next(set.Length)];
        void Add(Vector2 p, int prototype, float scale)
        {
            kept.Add(new TreeInstance
            {
                position = new Vector3(p.x / data.size.x, 0f, p.y / data.size.z),
                widthScale = scale, heightScale = scale * (0.9f + Rand() * 0.25f), rotation = Rand() * Mathf.PI * 2f,
                prototypeIndex = prototype, color = Color.white, lightmapColor = Color.white,
            });
        }
        const float step = 7f;
        for (float x = Area.xMin; x < Area.xMax; x += step)
            for (float z = Area.yMin; z < Area.yMax; z += step)
            {
                var p = new Vector2(x + Rand() * step, z + Rand() * step);
                if (Edit(p) < 0.5f) continue;
                float slope = data.GetSteepness(p.x / data.size.x, p.y / data.size.z);
                if (slope > 30f) continue;
                float arena = Vector2.Distance(p, ArenaCenter), pond = Vector2.Distance(p, PondCenter);
                // A ring of blossoms frames the glade; willows lean over the pond.
                if (arena > ArenaRadius + 6f && arena < ArenaRadius + 13f && Trail(p) > 6f) { if (Rand() < 0.6f) Add(p, Pick(blossom), 1.1f + Rand() * 0.35f); continue; }
                if (pond > PondRadius + 3f && pond < PondRadius + 11f && Trail(p) > 6f) { if (Rand() < 0.55f) Add(p, Pick(willowPink), 1f + Rand() * 0.3f); continue; }
                if (KeepClear(p, 0f)) continue;
                float density = Fbm(p * 1.7f);
                if (p.y < 505f)
                {
                    // The blossom grove: airy, with meadow gaps.
                    if (density < 0.42f || Rand() > 0.55f) continue;
                    float r = Rand();
                    Add(p, r < 0.72f ? Pick(blossom) : r < 0.86f ? Pick(red) : Pick(green), 0.9f + Rand() * 0.4f);
                }
                else if (p.y < 600f)
                {
                    if (density < 0.38f || Rand() > 0.7f) continue;
                    float r = Rand();
                    Add(p, r < 0.5f ? Pick(green) : r < 0.7f ? Pick(red) : r < 0.85f ? Pick(blue) : Pick(blossom), 0.9f + Rand() * 0.4f);
                }
                else
                {
                    // The northern woods: dense broadleaf and fir, willows along the creek.
                    if (density < 0.3f || Rand() > 0.85f) continue;
                    float r = Rand();
                    int kind = DistanceToLine(Creek, p) < 10f ? Pick(willowGreen) : r < 0.55f ? Pick(green) : r < 0.85f ? Pick(fir) : Pick(red);
                    Add(p, kind, 0.95f + Rand() * 0.45f);
                }
                if (understory.Length > 0 && Rand() < 0.45f)
                    Add(p + new Vector2(Rand() * 3f - 1.5f, Rand() * 3f - 1.5f), Pick(understory), 0.8f + Rand() * 0.5f);
            }
        // Flowers scattered through the grove and the glade's edge.
        for (int i = 0; i < 900 && understory.Length > 0; i++)
        {
            var p = new Vector2(Mathf.Lerp(340f, 600f, Rand()), Mathf.Lerp(398f, 520f, Rand()));
            if (Edit(p) < 0.5f || Trail(p) < 3f || Vector2.Distance(p, PondCenter) < PondRadius + 1f) continue;
            if (Vector2.Distance(p, ArenaCenter) < ArenaRadius - 3f) continue;
            Add(p, Pick(understory), 0.7f + Rand() * 0.6f);
        }
        data.SetTreeInstances(kept.ToArray(), true);
    }

    // Grass stays off the trails, the pond, the creek bed and the glade's packed centre.
    private static void Details(TerrainData data)
    {
        float cell = data.size.x / data.detailResolution;
        var region = Region(data.detailResolution, cell);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var detail = data.GetDetailLayer(region.x, region.y, region.width, region.height, layer);
            for (int z = 0; z < region.height; z++)
                for (int x = 0; x < region.width; x++)
                {
                    var p = new Vector2((region.x + x + 0.5f) * cell, (region.y + z + 0.5f) * cell);
                    float edit = Edit(p);
                    if (edit <= 0f) continue;
                    float keep = Smooth(2f, 4.5f, Trail(p)) * Smooth(PondRadius - 1f, PondRadius + 2f, Vector2.Distance(p, PondCenter))
                        * Smooth(1.5f, 3f, DistanceToLine(Creek, p)) * Mathf.Lerp(0.35f, 1f, Smooth(6f, 14f, Vector2.Distance(p, ArenaCenter)));
                    detail[z, x] = Mathf.RoundToInt(detail[z, x] * Mathf.Lerp(1f, keep, edit));
                }
            data.SetDetailLayer(region.x, region.y, layer, detail);
        }
    }

    // ---------- props ----------

    private static GameObject Place(Transform parent, string path, Vector3 position, Quaternion rotation, float scale, float sink = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(position - Vector3.up * sink, rotation);
        instance.transform.localScale = Vector3.one * scale;
        return instance;
    }

    private static Vector3 At(Vector2 p) => new(p.x, Ground(p), p.y);

    private static void Props(UnityEngine.SceneManagement.Scene world)
    {
        var old = world.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest biome");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject("Forest biome").transform;
        var random = new System.Random(77);
        float Rand() => (float)random.NextDouble();
        Quaternion Yaw() => Quaternion.Euler(0f, Rand() * 360f, 0f);

        // The Blossom Glade: a ring of standing stones with gaps where the two trails enter,
        // the guardian's shrine stone to the north, and four boulders for cover from thrown rock.
        var glade = new GameObject("Blossom Glade").transform;
        glade.SetParent(root, false);
        var gaps = new[] { TrailToArena[^1] - ArenaCenter, TrailToLake[0] - ArenaCenter }.Select(v => Mathf.Atan2(v.y, v.x)).ToArray();
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI * 2f / 14f;
            if (gaps.Any(g => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, g * Mathf.Rad2Deg)) < 18f)) continue;
            var p = ArenaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ArenaRadius + 2.5f);
            var stone = Place(glade, "Assets/PBR_Statues_pack/Statues_Viking/Prefab/Stone_01.prefab", At(p),
                Quaternion.LookRotation(new Vector3(ArenaCenter.x - p.x, 0f, ArenaCenter.y - p.y)), 1.3f + Rand() * 0.5f, 0.3f);
            stone.name = "Standing stone " + i;
        }
        var shrine = ArenaCenter + Vector2.up * (ArenaRadius - 4f);
        Place(glade, "Assets/PBR_Statues_pack/Statues_Viking/Prefab/Stone_01.prefab", At(shrine), Quaternion.Euler(0f, 180f, 0f), 2.4f, 0.2f).name = "Guardian shrine";
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + i * 90f) * Mathf.Deg2Rad;
            var p = ArenaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11.5f;
            // Big enough to hide behind when the guardian hurls a boulder.
            Place(glade, $"{Nature}Rock_Big_0{1 + i % 3}.prefab", At(p), Yaw(), 0.7f + Rand() * 0.12f, 0.5f).name = "Cover rock " + i;
        }
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 20f) * Mathf.Deg2Rad;
            var p = ArenaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ArenaRadius - 3.5f);
            Place(glade, $"{Nature}Branch_0{1 + i}.prefab", At(p), Yaw(), 3.2f, 0.05f).name = "Fallen log " + i;
        }
        Place(glade, $"{Nature}GodRays.prefab", At(ArenaCenter) + Vector3.up * 14f, Quaternion.identity, 1f);
        Place(glade, $"{NatureDemo}FloatingLeafs_Pink.prefab", At(ArenaCenter) + Vector3.up * 6f, Quaternion.identity, 2.5f);

        // The pond below the glade: water, lily pads, reeds and cattails.
        var pond = new GameObject("Glade pond").transform;
        pond.SetParent(root, false);
        var water = Place(pond, $"{Nature}Water.prefab", new Vector3(PondCenter.x, pondSurface, PondCenter.y), Quaternion.identity, 1f);
        water.transform.localScale = new Vector3(PondRadius * 2.2f / 10f, 1f, PondRadius * 2.2f / 10f);
        for (int i = 0; i < 6; i++)
        {
            var p = PondCenter + new Vector2(Rand() - 0.5f, Rand() - 0.5f) * PondRadius * 1.1f;
            Place(pond, $"{Nature}LilyPads_0{1 + i % 3}.prefab", new Vector3(p.x, pondSurface + 0.02f, p.y), Yaw(), 1f);
        }
        for (int i = 0; i < 16; i++)
        {
            float a = Rand() * Mathf.PI * 2f;
            var p = PondCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (PondRadius + Rand() * 2f - 0.5f);
            Place(pond, i % 2 == 0 ? $"{Nature}Cattail_0{1 + i % 3}.prefab" : $"{Nature}Reeds_0{1 + i % 3}.prefab", At(p), Yaw(), 1f + Rand() * 0.4f, 0.1f);
        }

        // The northern woods: a rocky knoll and cliff shoulders to climb around, boulders and logs.
        var woods = new GameObject("Northern woods").transform;
        woods.SetParent(root, false);
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 30f) * Mathf.Deg2Rad;
            var p = Knoll + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f;
            Place(woods, $"{Nature}Cliff_0{1 + i}.prefab", At(p), Yaw(), 0.55f + Rand() * 0.2f, 2.5f).name = "Knoll cliff " + i;
        }
        foreach (var p in new[] { new Vector2(318f, 640f), new Vector2(316f, 720f), new Vector2(322f, 800f), new Vector2(360f, 860f) })
            Place(woods, $"{Nature}Cliff_0{1 + random.Next(5)}.prefab", At(p), Yaw(), 0.8f + Rand() * 0.3f, 3f).name = "Edge cliff";
        for (int placed = 0, tries = 0; placed < 14 && tries < 200; tries++)
        {
            var p = new Vector2(Mathf.Lerp(330f, 590f, Rand()), Mathf.Lerp(560f, 860f, Rand()));
            if (KeepClear(p, 2f) || Edit(p) < 0.6f) continue;
            Place(woods, $"{Nature}Rock_Big_0{1 + random.Next(3)}.prefab", At(p), Yaw(), 0.3f + Rand() * 0.35f, 0.4f);
            placed++;
        }
        for (int placed = 0, tries = 0; placed < 18 && tries < 200; tries++)
        {
            var p = new Vector2(Mathf.Lerp(330f, 600f, Rand()), Mathf.Lerp(470f, 860f, Rand()));
            if (KeepClear(p, 1f) || Edit(p) < 0.6f) continue;
            Place(woods, $"{Nature}Branch_0{1 + random.Next(6)}.prefab", At(p), Yaw(), 2f + Rand() * 1.5f, 0.05f);
            placed++;
        }
        // Stepping stones along the creek bed.
        for (int i = 0; i < 24; i++)
        {
            int leg = random.Next(Creek.Length - 1);
            var p = Vector2.Lerp(Creek[leg], Creek[leg + 1], Rand()) + new Vector2(Rand() - 0.5f, Rand() - 0.5f) * 3f;
            Place(woods, $"{Nature}Stones_0{1 + random.Next(3)}.prefab", At(p), Yaw(), 1.5f + Rand(), 0.1f);
        }
        Place(root, $"{NatureDemo}FloatingLeafs_Pink.prefab", At(new Vector2(530f, 455f)) + Vector3.up * 5f, Quaternion.identity, 3f).name = "Grove petals";
        Place(root, $"{Nature}FloatingLeafs_Green.prefab", At(new Vector2(430f, 700f)) + Vector3.up * 5f, Quaternion.identity, 3f).name = "Woods leaves";
    }
}
