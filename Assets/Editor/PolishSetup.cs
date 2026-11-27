using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The feedback pass on the World and region scenes, as re-runnable steps (each menu item can be run again):
//  - World: easier interaction reach, the unlock card's time on screen, brighter nights, the objective beacon,
//    no god rays over the Blossom Glade's heart tree (they read as a beacon with nothing there), and the
//    snow lake's shore lifted clear of the lake plane so water no longer pokes through the snow.
//  - Ice: field vampires (none were placed: the lake plane under the snow rejected every spot), in numbers
//    matching the Forest and Volcano for the region's size.
//  - Volcano: the rift into the Sanctum moved beside Abe's camp under a tall beacon, and a framed lava-tube
//    entrance (a rock collar that closes the seams, flanking spires and braziers).
//  - Sanctum: its floor on the Ground layer (area spells need it), a dais instead of a 700 m column, and the
//    fixed stormy sky and light shafts (ArenaAtmosphere).
public static class PolishSetup
{
    private const string Landmarks = "Assets/Environment/Landmarks";
    private const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    private const string Torch = "Assets/Stylized Fantasy Armory/Prefabs/Decorative Props/Torch/Torch.prefab";
    private const string StormSky = "Assets/ZerinLabs_shaderPack_CartoonSky/Materials/mat_skybox_magicStorm.mat";

    // Ice field packs: (near, count). Area-for-area about as busy as the Forest and the Volcano.
    public static readonly (Vector2 at, int count)[] IcePacks =
    {
        (new Vector2(600f, 262f), 2), (new Vector2(712f, 252f), 2), (new Vector2(770f, 300f), 2), (new Vector2(560f, 215f), 2),
        (new Vector2(520f, 300f), 1), (new Vector2(800f, 232f), 1), (new Vector2(690f, 185f), 1), (new Vector2(745f, 200f), 1),
    };

    [MenuItem("Wulang/Polish/Run every step")]
    public static string All()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        ArmorySetup.Build();
        var log = new List<string> { World(), Forest(), Ice(), Volcano(), Sanctum(), BossSetup.UpdateMoves() };
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        return string.Join("\n", log);
    }

    [MenuItem("Wulang/Polish/World")]
    public static string World()
    {
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        Set(Object.FindFirstObjectByType<PlayerInteract>(), "range", 3.5f);
        Set(Object.FindFirstObjectByType<ElementUnlockNotification>(), "duration", 3.5f);

        // Nights: moonlit, not black. The ambient stays blue so it still reads as night.
        var cycle = Object.FindFirstObjectByType<DayNightCycle>();
        Set(cycle, "moonIntensity", 1.1f);
        SetColor(cycle, "nightAmbient", new Color(0.2f, 0.24f, 0.34f));
        Set(cycle, "nightSkyExposure", 0.3f);
        Set(cycle, "nightReflectionIntensity", 0.4f);

        var objectives = Object.FindFirstObjectByType<ObjectiveManager>();
        if (objectives != null)
        {
            var settings = new SerializedObject(objectives);
            settings.FindProperty("beaconMaterial").objectReferenceValue = BeamMaterial("Objective", new Color(0.75f, 0.9f, 1f), 0.5f);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        var rays = GameObject.Find("Landmarks and arenas/Blossom Glade henge/GodRays");
        if (rays != null) Object.DestroyImmediate(rays);

        string shore = LiftLakeShore() + "; " + SeatPonds();
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        return "World: interaction 3.5 m, unlock card 3.5 s, brighter nights, objective beacon, heart-tree rays removed; " + shore;
    }

    // The snow region's lake is one plane at a fixed height under the whole region. Where the ground came within
    // a few centimetres of it the two surfaces flickered, and small hollows filled with stray water in the snow.
    // Lifts the hollows (anything not joined to the main lake) above the water and pushes the shore clear of it.
    private static string LiftLakeShore()
    {
        var water = GameObject.Find("Water");
        var terrain = Terrain.activeTerrain;
        if (water == null || terrain == null) return "lake: not found";
        var data = terrain.terrainData;
        var bounds = water.GetComponent<Renderer>().bounds;
        float level = bounds.center.y, size = data.size.y, baseY = terrain.transform.position.y;
        int res = data.heightmapResolution;
        Vector2Int Cell(Vector3 p) => new(
            Mathf.Clamp(Mathf.RoundToInt((p.x - terrain.transform.position.x) / data.size.x * (res - 1)), 0, res - 1),
            Mathf.Clamp(Mathf.RoundToInt((p.z - terrain.transform.position.z) / data.size.z * (res - 1)), 0, res - 1));
        var min = Cell(bounds.min);
        var max = Cell(bounds.max);
        int w = max.x - min.x + 1, h = max.y - min.y + 1;
        var heights = data.GetHeights(min.x, min.y, w, h); // [z, x], normalised
        float Height(int x, int z) => heights[z, x] * size + baseY;
        void SetHeight(int x, int z, float y) => heights[z, x] = (y - baseY) / size;

        // Wet cells grouped into connected pools; the largest is the lake.
        var pool = new int[h, w];
        var sizes = new List<int> { 0 };
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                if (pool[z, x] != 0 || Height(x, z) >= level + 0.05f) continue;
                int id = sizes.Count, count = 0;
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, z));
                pool[z, x] = id;
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    count++;
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || pool[n.y, n.x] != 0 || Height(n.x, n.y) >= level + 0.05f) continue;
                        pool[n.y, n.x] = id;
                        stack.Push(n);
                    }
                }
                sizes.Add(count);
            }
        int lake = sizes.Count > 1 ? sizes.IndexOf(sizes.Skip(1).Max()) : 0;
        int lifted = 0, shored = 0;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float y = Height(x, z);
                if (pool[z, x] != 0 && pool[z, x] != lake) { SetHeight(x, z, level + 0.35f); lifted++; }
                else if (Mathf.Abs(y - level) < 0.15f) { SetHeight(x, z, y < level ? level - 0.2f : level + 0.2f); shored++; }
            }
        data.SetHeights(min.x, min.y, heights);
        EditorUtility.SetDirty(data);
        return $"lake: {sizes.Count - 2} stray pools ({lifted} cells) lifted, {shored} shore cells cleared of the water line";
    }

    // The armory's two training vampires move with the training ground to the yard's west side.
    [MenuItem("Wulang/Polish/Forest training vampires")]
    public static string Forest()
    {
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("Forest"), OpenSceneMode.Additive);
        var training = scene.GetRootGameObjects().First(g => g.name == "Combat training");
        var targets = training.GetComponentsInChildren<CombatTarget>(true);
        for (int i = 0; i < targets.Length && i < RegionContentSetup.TrainingSpots.Length; i++)
            targets[i].transform.position = SceneSplitSetup.Ground(RegionContentSetup.TrainingSpots[i]) - Vector3.up * 0.1f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return $"Forest: {targets.Length} training vampires moved to the training ground.";
    }

    // The snow biome's frozen ponds are flat discs laid on sloping ground: their edges floated up to 60 cm
    // above the snow (a bright strip from afar) or sank into it. Each one gets a shallow basin just under
    // its surface and a low lip of snow where the ground fell away, so it sits in the snow like real ice.
    private static string SeatPonds()
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        int res = data.heightmapResolution, seated = 0;
        float cellX = data.size.x / (res - 1), cellZ = data.size.z / (res - 1);
        foreach (var pond in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Frozen pond"))
        {
            var b = pond.bounds;
            float radius = Mathf.Min(b.extents.x, b.extents.z), surface = b.max.y, rim = radius + 3f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((b.center.x - rim - origin.x) / cellX)), z0 = Mathf.Max(0, Mathf.FloorToInt((b.center.z - rim - origin.z) / cellZ));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((b.center.x + rim - origin.x) / cellX)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((b.center.z + rim - origin.z) / cellZ));
            var heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int z = 0; z <= z1 - z0; z++)
                for (int x = 0; x <= x1 - x0; x++)
                {
                    float d = Vector2.Distance(new Vector2(origin.x + (x0 + x) * cellX, origin.z + (z0 + z) * cellZ), new Vector2(b.center.x, b.center.z));
                    float y = heights[z, x] * data.size.y + origin.y, want = y;
                    if (d < radius * 0.92f) want = surface - 0.08f;
                    else if (d < rim && y < surface + 0.1f) want = Mathf.Lerp(surface + 0.1f, y, Mathf.InverseLerp(radius * 0.92f, rim, d));
                    heights[z, x] = (want - origin.y) / data.size.y;
                }
            data.SetHeights(x0, z0, heights);
            seated++;
        }
        EditorUtility.SetDirty(data);
        return $"{seated} frozen ponds seated in the snow";
    }

    [MenuItem("Wulang/Polish/Ice field vampires")]

    public static string Ice()
    {
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("Ice"), OpenSceneMode.Additive);
        var content = scene.GetRootGameObjects().First(g => g.name == "Region content").transform;
        var old = content.Find("Field vampires");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var field = new GameObject("Field vampires").transform;
        field.SetParent(content, false);
        var water = GameObject.Find("Water");
        float shore = water != null ? water.GetComponent<Renderer>().bounds.center.y + 0.6f : float.MinValue;
        int placed = 0;
        Random.InitState(41);
        foreach (var (at, count) in IcePacks)
            for (int i = 0; i < count; i++)
            {
                var offset = count == 1 ? Vector2.zero : new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) * 4f;
                var spot = SceneSplitSetup.Flattest(at + offset, 10f);
                if (spot.y < shore) continue; // not out on the lake
                var vampire = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Combat/Ice vampire.prefab"), field);
                vampire.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Set(vampire.GetComponent<CombatTarget>(), "trainingRespawnDelay", 40f);
                placed++;
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return $"Ice: {placed} field vampires placed.";
    }

    [MenuItem("Wulang/Polish/Volcano rift and entrance")]
    public static string Volcano()
    {
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        string entrance = Entrance();
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);

        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("Volcano"), OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        var rift = roots.SelectMany(g => g.GetComponentsInChildren<RegionTravel>(true)).First();
        var camp = roots.SelectMany(g => g.GetComponentsInChildren<Merchant>(true)).First().transform;
        var spawn = roots.Select(g => g.GetComponent<RegionStart>()).First(s => s != null).Spawn;
        // Beside the camp, on the far side of the fire from where Wulang arrives, so it is passed on the way out.
        var away = Vector3.ProjectOnPlane(camp.position - spawn.position, Vector3.up).normalized;
        var spot = SceneSplitSetup.Flattest(new Vector2(camp.position.x + away.x * 9f, camp.position.z + away.z * 9f), 4f);
        rift.transform.position = spot - Vector3.up * 0.1f;
        rift.transform.rotation = Quaternion.LookRotation(-away);
        RiftBeacon(rift);
        foreach (var list in roots.SelectMany(g => g.GetComponentsInChildren<RegionObjectives>(true)))
        {
            var last = list.Objectives[^1];
            last.marker = rift.transform.position;
            EditorUtility.SetDirty(list);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return $"Volcano: rift moved beside Abe's camp to {spot}; {entrance}";
    }

    // A violet column of light over the rift, tall enough to see from anywhere in the Vale, shown only while the
    // rift is open (its FlagGate).
    public static void RiftBeacon(RegionTravel rift)
    {
        var old = rift.transform.Find("Rift beacon");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var beacon = new GameObject("Rift beacon").transform;
        beacon.SetParent(rift.transform, false);
        foreach (var (height, width, alpha) in new[] { (420f, 3.2f, 0.9f), (420f, 9f, 0.35f) })
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Light beam";
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(beacon, false);
            beam.transform.position = rift.transform.position + Vector3.up * height * 0.5f;
            beam.transform.localScale = new Vector3(width, height * 0.5f, width);
            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BeamMaterial(alpha > 0.5f ? "Rift" : "Rift haze", new Color(0.75f, 0.5f, 1f), alpha);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        var light = new GameObject("Rift light").AddComponent<Light>();
        light.transform.SetParent(beacon, false);
        light.transform.position = rift.transform.position + Vector3.up * 3f;
        light.type = LightType.Point;
        light.color = new Color(0.75f, 0.5f, 1f);
        light.range = 16f;
        light.intensity = 4f;
        var gate = new SerializedObject(rift.GetComponent<FlagGate>());
        var objects = gate.FindProperty("objects");
        objects.arraySize = 1;
        objects.GetArrayElementAtIndex(0).objectReferenceValue = beacon.gameObject;
        gate.ApplyModifiedPropertiesWithoutUndo();
    }

    // The lava tube's mouth, framed so it reads as the way in: a collar of big rocks over the seam where the tube
    // leaves the cone (no more open joints or a bare pipe), two tall spires either side, braziers and a warm glow
    // inside. Built beside the volcano (not inside it), so rebuilding the volcano leaves it alone.
    private static string Entrance()
    {
        var volcano = GameObject.Find("Redwood Lava Cavern");
        if (volcano == null) return "entrance: no volcano";
        var old = GameObject.Find("Lava tube entrance");
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject("Lava tube entrance").transform;
        var root = volcano.transform;
        // Root frame: local -Z runs out of the tube toward the armory; the mouth is at z = -74, floor 6.8 m.
        const float mouth = -74f, floor = 6.8f;
        Vector3 At(float x, float y, float z) => root.TransformPoint(new Vector3(x, floor + y, z));
        float Yaw(float y) => root.eulerAngles.y + y;
        // Collar: overlapping boulders along both sides and over the top of the protruding tube.
        int rocks = 0;
        for (int i = 0; i < 4; i++)
        {
            float z = mouth + 3f + i * 4f;
            foreach (float side in new[] { -1f, 1f })
                Rock(group, "Cliff_infernal", At(side * 8.8f, 4f, z), new Vector3(6f, 11f, 6.5f), Yaw(i * 47f + side * 20f), ref rocks);
            Rock(group, "Rock_infernal", At(0f, 11.2f, z + 1f), new Vector3(15f, 5f, 6.5f), Yaw(i * 31f), ref rocks);
        }
        // Spires flanking the way in.
        foreach (float side in new[] { -1f, 1f })
            Rock(group, "Cliff_infernal", At(side * 12.5f, 7f, mouth - 3f), new Vector3(4.5f, 18f, 4.5f), Yaw(side * 15f), ref rocks);
        // Braziers either side of the walkway, and a glow from inside.
        foreach (float side in new[] { -1f, 1f })
        {
            var brazier = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Torch), group);
            var p = At(side * 5.2f, 0f, mouth - 4f);
            if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            brazier.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Yaw(0f), 0f));
            brazier.transform.localScale = Vector3.one * 1.6f;
            Glow(brazier.transform, Vector3.up * 1.8f, new Color(1f, 0.55f, 0.2f), 12f, 3f);
        }
        Glow(group, At(0f, 4f, mouth + 6f) - group.position, new Color(1f, 0.45f, 0.12f), 24f, 6f);
        return $"entrance framed ({rocks} rocks)";
    }

    private static void Rock(Transform parent, string prefab, Vector3 center, Vector3 size, float yaw, ref int count)
    {
        var rock = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rocks + prefab + ".prefab"), parent);
        rock.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var renderers = rock.GetComponentsInChildren<Renderer>();
        var b = renderers.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z));
        b = renderers.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        rock.transform.position += center - b.center;
        if (rock.GetComponentInChildren<Collider>() == null) rock.AddComponent<MeshCollider>();
        rock.layer = LayerMask.NameToLayer("Ground");
        count++;
    }

    private static void Glow(Transform parent, Vector3 offset, Color color, float range, float intensity)
    {
        var light = new GameObject("Firelight").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = offset;
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    [MenuItem("Wulang/Polish/Sanctum")]
    public static string Sanctum()
    {
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("FinalArena"), OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        var arena = roots.First(g => g.name == "Arena");
        var floor = arena.transform.Find("Floating platform").gameObject;
        floor.layer = LayerMask.NameToLayer("Ground");
        float top = floor.GetComponent<Renderer>().bounds.max.y;
        // The throne's dais: a slab on the platform rather than a column to the ground far below.
        foreach (var slab in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Plate_fancy" && t.parent != null && t.parent.name == "Sanctum dressing"))
        {
            var renderers = slab.GetComponentsInChildren<Renderer>();
            var b = renderers.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            if (b.size.y < 10f) continue;
            float wanted = 2.5f, keepTop = b.max.y;
            var scale = slab.localScale;
            slab.localScale = new Vector3(scale.x, scale.y * wanted / b.size.y, scale.z);
            b = renderers.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            slab.position += Vector3.up * (keepTop - b.max.y);
        }
        var atmosphere = arena.GetComponent<ArenaAtmosphere>() ?? arena.AddComponent<ArenaAtmosphere>();
        var settings = new SerializedObject(atmosphere);
        settings.FindProperty("skybox").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(StormSky);
        settings.FindProperty("shaftMaterial").objectReferenceValue = BeamMaterial("Sanctum shaft", new Color(0.85f, 0.75f, 1f), 0.22f);
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return $"Sanctum: floor on Ground (top {top:0.0}), dais fixed, storm sky and light shafts.";
    }

    // An unlit beam material like the chest beacons' (bright at the foot, fading upward), in its own colour.
    private static Material BeamMaterial(string name, Color color, float strength)
    {
        string path = $"{Landmarks}/{name} beam.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>($"{Landmarks}/Supplies beam.mat"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color * strength);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Set(Object target, string property, float value)
    {
        if (target == null) return;
        var settings = new SerializedObject(target);
        settings.FindProperty(property).floatValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(Object target, string property, Color value)
    {
        if (target == null) return;
        var settings = new SerializedObject(target);
        settings.FindProperty(property).colorValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
