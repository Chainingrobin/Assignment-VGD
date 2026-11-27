using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Places each region's gameplay: its guardian arena, Abe's camp, the vampires that live there,
// and its exit (the frozen lake for the Forest, the mountain-pass waystone in the Ice region,
// the caldera rift in the Volcano). Also adds lava hazards to the World's lava fields.
// Re-runnable: each region's "Region content" root is rebuilt from scratch.
public static class RegionContentSetup
{
    private const string Books = "Assets/Prefabs/Pickups/";
    private const string Waystone = "Assets/PBR_Statues_pack/Statues_Viking/Prefab/Stone_01.prefab";

    [MenuItem("Wulang/Scenes/Populate regions")]
    public static void Populate()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        AddLavaHazards(world);
        EditorSceneManager.SaveScene(world);

        var forest = Open("Forest", out var forestRoot);
        // A tree-free meadow at the forest's north edge: a short trek from the armory.
        // The Blossom Glade built by ForestBiomeBuilder: between the armory and the lake statue.
        var forestArena = SceneSplitSetup.Ground(ForestBiomeBuilder.ArenaCenter);
        Encounter(forestRoot, "Earth Guardian arena", forestArena, BossSetup.EarthBoss, StoryScript.Bosses[Flags.BossEarth].name, Flags.BossEarth,
            "Earth Book", 20f, 24f, StoryScript.Bosses[Flags.BossEarth].intro, StoryScript.Bosses[Flags.BossEarth].dying);
        // Abe's camp sits beside the armory path, between its two arches: walking in from where
        // Wulang wakes, the player meets his face across the fire.
        Camp(forestRoot, new Vector2(556f, 586f), new Vector2(566f, 588f),
            StoryScript.Greetings["Forest"], wagon: true);
        // Two vampires guard the trail from the armory to the glade (the tutorial's first fights),
        // and Earth packs roam the meadows, with Lightning strays near the glade.
        Vampire(forestRoot, "Earth", SceneSplitSetup.Flattest(new Vector2(546f, 530f), 6f));
        Vampire(forestRoot, "Lightning", SceneSplitSetup.Flattest(new Vector2(520f, 490f), 6f));
        Pack(forestRoot, "Earth", new Vector2(590f, 515f), 2);
        Pack(forestRoot, "Earth", new Vector2(470f, 565f), 2);
        Pack(forestRoot, "Earth", new Vector2(430f, 640f), 2);
        Pack(forestRoot, "Earth", new Vector2(600f, 660f), 1);
        Pack(forestRoot, "Lightning", new Vector2(450f, 480f), 1);
        Pack(forestRoot, "Lightning", new Vector2(505f, 705f), 1);
        Loot(forestRoot, new Rect(340f, 380f, 290f, 460f), coinPiles: 16, bags: 4, potions: 3, seed: 11);
        TrainingPair(forest);
        Save(forest);

        var ice = Open("Ice", out var iceRoot);
        var iceSpawn = RegionStartSpawn(ice);
        // The Frozen Amphitheater built by SnowBiomeBuilder.
        var iceArena = SceneSplitSetup.Ground(SnowBiomeBuilder.ArenaCenter);
        FaceSpawn(ice, iceArena);
        Encounter(iceRoot, "Ice Guardian arena", iceArena, BossSetup.IceBoss,
            StoryScript.Bosses[Flags.BossIce].name, Flags.BossIce, "Ice Book", 18f, 22f,
            StoryScript.Bosses[Flags.BossIce].intro, StoryScript.Bosses[Flags.BossIce].dying);
        Camp(iceRoot, new Vector2(iceSpawn.x + 6f, iceSpawn.z - 4f), new Vector2(iceSpawn.x, iceSpawn.z),
            StoryScript.Greetings["Ice"]);
        foreach (var (p, count) in PolishSetup.IcePacks) Pack(iceRoot, "Ice", p, count);
        Loot(iceRoot, new Rect(450f, 140f, 430f, 215f), coinPiles: 8, bags: 2, potions: 2, seed: 23, spacing: 14f);
        Travel(iceRoot, "Mountain pass waystone", iceSpawn + new Vector3(-6f, 0f, -3f), "Volcano", Flags.BossIce,
            "Press F to cross the mountain pass");
        Save(ice);

        var volcano = Open("Volcano", out var volcanoRoot);
        var volcanoSpawn = RegionStartSpawn(volcano);
        var caldera = GameObject.Find("Redwood Lava Cavern/Boss caldera/Boss spawn point").transform.position;
        Encounter(volcanoRoot, "Fire Guardian arena", new Vector3(795f, caldera.y, 642f), BossSetup.FireBoss, StoryScript.Bosses[Flags.BossFire].name,
            Flags.BossFire, "Fire Book", 13f, 14f, StoryScript.Bosses[Flags.BossFire].intro, StoryScript.Bosses[Flags.BossFire].dying, caldera);
        Camp(volcanoRoot, new Vector2(volcanoSpawn.x + 4f, volcanoSpawn.z + 5f), new Vector2(volcanoSpawn.x, volcanoSpawn.z),
            StoryScript.Greetings["Volcano"]);
        foreach (var p in new[] { new Vector2(680f, 720f), new Vector2(700f, 780f), new Vector2(665f, 650f) })
            Pack(volcanoRoot, "Fire", p, 2);
        foreach (var p in new[] { new Vector2(700f, 560f), new Vector2(660f, 805f), new Vector2(725f, 690f) })
            Pack(volcanoRoot, "Fire", p, 1);
        Loot(volcanoRoot, new Rect(650f, 470f, 240f, 360f), coinPiles: 7, bags: 2, potions: 2, seed: 37);
        // The rift opens beside Abe's last camp, so the player can stock up before the point of no return.
        var away = new Vector3(4f, 0f, 5f).normalized;
        var riftAt = SceneSplitSetup.Flattest(new Vector2(volcanoSpawn.x + 4f + away.x * 9f, volcanoSpawn.z + 5f + away.z * 9f), 4f);
        Travel(volcanoRoot, "Caldera rift", riftAt, "FinalArena", Flags.BossFire, StoryScript.Text.riftPrompt);
        PolishSetup.RiftBeacon(volcanoRoot.Find("Caldera rift").GetComponent<RegionTravel>());
        Save(volcano);

        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        Debug.Log("RegionContentSetup: regions populated.");
    }

    private static Scene Open(string region, out Transform root)
    {
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(region), OpenSceneMode.Additive);
        var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Region content");
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("Region content");
        SceneManager.MoveGameObjectToScene(go, scene);
        root = go.transform;
        return scene;
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static Vector3 RegionStartSpawn(Scene scene) =>
        scene.GetRootGameObjects().Select(go => go.GetComponent<RegionStart>()).First(s => s != null).Spawn.position;

    // Arrive looking toward the region's guardian, so the way forward is obvious.
    private static void FaceSpawn(Scene scene, Vector3 target)
    {
        var spawn = scene.GetRootGameObjects().Select(go => go.GetComponent<RegionStart>()).First(s => s != null).Spawn;
        spawn.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - spawn.position, Vector3.up));
    }

    private static Vector3 Ground(float x, float z) => SceneSplitSetup.Ground(new Vector2(x, z));

    private static GameObject Place(string prefabPath, Transform parent, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new System.IO.FileNotFoundException(prefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    private static void Encounter(Transform parent, string name, Vector3 center, string bossPrefab, string displayName, string flag,
        string book, float engageRadius, float arenaRadius, string intro, string dying, Vector3? bossPosition = null)
    {
        var arena = new GameObject(name).transform;
        arena.SetParent(parent, false);
        arena.position = center;
        var boss = Place(bossPrefab, arena, (bossPosition ?? center) + Vector3.up * 0.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        var brain = new SerializedObject(boss.GetComponent<BossBrain>());
        brain.FindProperty("arenaRadius").floatValue = arenaRadius;
        brain.ApplyModifiedPropertiesWithoutUndo();
        var encounter = arena.gameObject.AddComponent<BossEncounter>();
        var settings = new SerializedObject(encounter);
        settings.FindProperty("boss").objectReferenceValue = boss.GetComponent<BossBrain>();
        settings.FindProperty("displayName").stringValue = displayName;
        settings.FindProperty("defeatFlag").stringValue = flag;
        settings.FindProperty("rewardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Books + book + ".prefab");
        settings.FindProperty("engageRadius").floatValue = engageRadius;
        settings.FindProperty("introLine").stringValue = intro;
        settings.FindProperty("dyingLine").stringValue = dying;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string Armory = "Assets/Stylized Fantasy Armory/Prefabs/";

    // Abe's camp: he sits on the ground by a fire (his Sitting Idle pose), a log at his back and a
    // table at his side ("Camp table", where StorySetup puts the camp's lore note), with a lamp, a
    // crate, a barrel and firelight. He faces `toward` (the way the player arrives) so they meet his
    // face across the fire. F talks to him first, then trades (Merchant).
    private static void Camp(Transform parent, Vector2 at, Vector2 toward, string greeting, bool wagon = false)
    {
        var seat = SceneSplitSetup.Ground(at) - Vector3.up * 0.1f; // Ground() lifts 0.1 m for loose props
        var forward = new Vector3(toward.x - at.x, 0f, toward.y - at.y).normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        var facing = Quaternion.LookRotation(forward);
        ClearTrees(seat, 6f);
        var camp = new GameObject("Abe's camp").transform;
        camp.SetParent(parent, false);
        camp.SetPositionAndRotation(seat, facing);
        var fire = Prop(camp, "Decorative Props/Cauldron and Campfire/Campfire.prefab", seat + forward * 1.45f, facing);
        AddLight(fire.transform, Vector3.up * 0.5f, new Color(1f, 0.55f, 0.2f), 10f, 3f);
        Prop(camp, "Nature/Wooden Logs/WoodenLog.001.prefab", seat - forward * 1.0f, facing);
        var table = Prop(camp, "Decorative Props/Table/Table.prefab", seat + right * 1.9f + forward * 0.2f, Quaternion.LookRotation(right));
        table.name = "Camp table";
        var lamp = Prop(camp, "Decorative Props/Lamp/Lamp.001 1.prefab", table.transform.position + Vector3.up * 0.87f + forward * 0.8f, facing, snap: false);
        AddLight(lamp.transform, Vector3.up * 0.2f, new Color(1f, 0.8f, 0.5f), 5f, 1.5f);
        Prop(camp, "Decorative Props/Box/Box.001.prefab", seat - right * 1.7f - forward * 0.9f, Quaternion.Euler(0f, 25f, 0f) * facing);
        Prop(camp, "Decorative Props/Barrel/Barrel.001.prefab", seat - right * 2.5f - forward * 0.2f, facing);
        if (wagon) Prop(camp, "Wagon/Wagon.003.prefab", seat - right * 4.5f - forward * 2.5f, Quaternion.LookRotation(-right));

        var abe = Place(BossSetup.AbePrefab, camp, seat + Vector3.up * 0.02f, facing);
        var merchant = new SerializedObject(abe.GetComponent<Merchant>());
        merchant.FindProperty("greeting").stringValue = greeting;
        merchant.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Prop(Transform parent, string path, Vector3 position, Quaternion rotation, bool snap = true)
    {
        if (snap) position = SceneSplitSetup.Ground(new Vector2(position.x, position.z)) - Vector3.up * 0.1f;
        var prop = Place(Armory + path, parent, position, rotation);
        // The pack's wagons carry WheelColliders, which need a rigidbody and only spam warnings on a prop.
        foreach (var wheel in prop.GetComponentsInChildren<WheelCollider>()) Object.DestroyImmediate(wheel);
        return prop;
    }

    // The armory's training vampires (one per element, from Assignment 2) stood across the gate. Only the
    // forest's own elements stay, Earth and Lightning, in the yard's training ground by the dummies, and they
    // only fight inside the yard, so the walk to Abe's fire is quiet.
    // Beside the dummies on the yard's west side (ArmorySetup).
    public static readonly Vector2[] TrainingSpots = { new(555.8f, 545f), new(555.8f, 549.2f) };

    private static void TrainingPair(Scene forest)
    {
        var training = forest.GetRootGameObjects().FirstOrDefault(go => go.name == "Combat training");
        if (training == null) return;
        var spots = TrainingSpots;
        int next = 0;
        foreach (var target in training.GetComponentsInChildren<CombatTarget>(true).ToArray())
        {
            if ((target.Affinity != ElementType.Earth && target.Affinity != ElementType.Lightning) || next >= spots.Length)
            {
                Object.DestroyImmediate(target.gameObject);
                continue;
            }
            target.transform.position = SceneSplitSetup.Ground(spots[next++]);
            var melee = target.GetComponent<VampireMelee>();
            if (melee == null) continue;
            var settings = new SerializedObject(melee);
            settings.FindProperty("pursuitRadius").floatValue = 12f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddLight(Transform parent, Vector3 offset, Color color, float range, float intensity)
    {
        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = offset;
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    // Terrain trees too close to a camp would grow through the fire or the table.
    private static void ClearTrees(Vector3 center, float radius)
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        var trees = data.treeInstances.Where(tree =>
        {
            var world = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
            return new Vector2(world.x - center.x, world.z - center.z).magnitude > radius;
        }).ToArray();
        if (trees.Length != data.treeInstances.Length) data.treeInstances = trees;
    }

    // Field vampires of the biome's element: a pack of `count` around a point, respawning after 40 s
    // so there is always something to fight and Wubucks to earn for Abe's potions.
    internal static void Pack(Transform parent, string element, Vector2 near, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var offset = count == 1 ? Vector2.zero : new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) * 4f;
            if (SafeSpot(near + offset, 10f, out var spot)) Vampire(parent, element, spot);
        }
    }

    private static void Vampire(Transform parent, string element, Vector3 position)
    {
        var vampire = Place($"Assets/Combat/{element} vampire.prefab", parent, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        var target = new SerializedObject(vampire.GetComponent<CombatTarget>());
        target.FindProperty("trainingRespawnDelay").floatValue = 40f;
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    // The flattest ground near a point that isn't lava, water or a hazard (lake drowning, lava burns).
    private static bool SafeSpot(Vector2 near, float radius, out Vector3 spot)
    {
        spot = SceneSplitSetup.Flattest(near, radius);
        foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Collide))
        {
            // Only water or hazards at the ground count: the snow region's lake plane and its drowning
            // zone lie under the whole region, below the snow, and used to reject every spot there.
            if (hit.point.y < spot.y - 0.5f) continue;
            string name = hit.collider.name.ToLowerInvariant();

            if (hit.collider.GetComponentInParent<HazardZone>() != null || name.Contains("lava") || name.Contains("molten") || name.Contains("pond")
                || name.Contains("water") || name.Contains("lake")) return false;
        }
        return true;
    }

    private static readonly (Vector2 at, float radius)[] KeepClear =
    {
        (ForestBiomeBuilder.ArenaCenter, 32f), (SnowBiomeBuilder.ArenaCenter, 30f), (new Vector2(795f, 642f), 48f), // guardian arenas
        (new Vector2(565f, 560f), 26f), (new Vector2(556f, 586f), 10f),                                            // armory and its camp
    };

    // Rewards for wandering off the path: piles of 3-5 coins, Wubucks bags worth 10 and the odd
    // healing potion, scattered over safe, open ground in the area, spaced apart. Seeded, so a
    // rebuild puts them in the same places.
    private static void Loot(Transform parent, Rect area, int coinPiles, int bags, int potions, int seed, float spacing = 22f)
    {
        Pickups(out var coin, out var bag, out var potion);
        var root = new GameObject("Loot").transform;
        root.SetParent(parent, false);
        var state = Random.state;
        Random.InitState(seed);
        // The reward at each spot in shuffled order, so a crowded area that fits fewer spots still gets a mix.
        var kinds = Enumerable.Repeat(0, coinPiles).Concat(Enumerable.Repeat(1, bags)).Concat(Enumerable.Repeat(2, potions))
            .OrderBy(_ => Random.value).ToArray();
        var placed = new System.Collections.Generic.List<Vector2>();
        int wanted = kinds.Length;
        for (int tries = 0; placed.Count < wanted && tries < wanted * 60; tries++)
        {
            var p = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
            if (placed.Any(q => (q - p).magnitude < spacing) || KeepClear.Any(k => (k.at - p).magnitude < k.radius)) continue;
            if (!SafeSpot(p, 4f, out var spot)) continue;
            int kind = kinds[placed.Count];
            placed.Add(new Vector2(spot.x, spot.z));
            if (kind == 0)
            {
                int coins = Random.Range(3, 6);
                for (int i = 0; i < coins; i++)
                {
                    var ring = new Vector2(Mathf.Cos(i * 2.1f), Mathf.Sin(i * 2.1f)) * 0.9f;
                    var at = SceneSplitSetup.Ground(new Vector2(spot.x, spot.z) + ring);
                    Place(coin, root, at + Vector3.up * 0.35f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                }
            }
            else Place(kind == 1 ? bag : potion, root, spot + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }
        Random.state = state;
        Debug.Log($"RegionContentSetup: {placed.Count}/{wanted} loot spots in {parent.gameObject.scene.name}.");
    }

    // The pickup prefabs: the coin gets a trigger big enough to walk into without pixel-hunting, and
    // a Wubucks bag (worth 10) and a potion bottle are made from the armory pack's money bag and bottle.
    private static void Pickups(out string coin, out string bag, out string potion)
    {
        coin = ItemsSetup.CoinPath;
        bag = "Assets/Prefabs/Pickups/Wubucks bag.prefab";
        potion = "Assets/Prefabs/Pickups/Healing potion.prefab";
        var coinRoot = PrefabUtility.LoadPrefabContents(coin);
        try
        {
            var box = coinRoot.GetComponent<BoxCollider>();
            var scale = coinRoot.transform.lossyScale;
            box.size = new Vector3(1f / scale.x, 1.6f / scale.y, 1f / scale.z);
            PrefabUtility.SaveAsPrefabAsset(coinRoot, coin);
        }
        finally { PrefabUtility.UnloadPrefabContents(coinRoot); }
        MakePickup(bag, "Decorative Props/Coin and Bag/MoneyBag.prefab", new Color(1f, 0.85f, 0.35f), pickup => pickup.AddComponent<CoinCollect>(), "amount", 10);
        MakePickup(potion, "Decorative Props/Bottles/Bottle.004.prefab", new Color(1f, 0.35f, 0.45f), pickup =>
        {
            var item = new SerializedObject(pickup.AddComponent<ItemPickup>());
            item.FindProperty("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(ItemsSetup.PotionPath);
            item.ApplyModifiedPropertiesWithoutUndo();
            return null;
        });
    }

    private static void MakePickup(string path, string model, Color glow, System.Func<GameObject, Component> behaviour, string intField = null, int intValue = 0)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Armory + model), root.transform);
            foreach (var collider in mesh.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.3f;
            trigger.size = new Vector3(1f, 1.6f, 1f);
            var floating = new SerializedObject(root.AddComponent<FloatingPickup>());
            floating.FindProperty("lightColor").colorValue = glow;
            floating.FindProperty("rotationSpeed").floatValue = 45f;
            floating.ApplyModifiedPropertiesWithoutUndo();
            var added = behaviour(root);
            if (added != null && intField != null)
            {
                var settings = new SerializedObject(added);
                settings.FindProperty(intField).intValue = intValue;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Travel(Transform parent, string name, Vector3 position, string region, string flag, string prompt, bool onTerrain = true)
    {
        // The caldera floor is a mesh above the terrain, so the rift keeps its given height.
        if (onTerrain) position = SceneSplitSetup.Ground(new Vector2(position.x, position.z));
        var stone = Place(Waystone, parent, position, Quaternion.identity);
        stone.name = name;
        var interactable = stone.AddComponent<Interactable>();
        var settings = new SerializedObject(interactable);
        settings.FindProperty("prompt").stringValue = prompt;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var travel = new SerializedObject(stone.AddComponent<RegionTravel>());
        travel.FindProperty("region").stringValue = region;
        travel.ApplyModifiedPropertiesWithoutUndo();
        var gate = new SerializedObject(stone.AddComponent<FlagGate>());
        gate.FindProperty("flag").stringValue = flag;
        var components = gate.FindProperty("components");
        components.arraySize = 1;
        components.GetArrayElementAtIndex(0).objectReferenceValue = interactable;
        gate.ApplyModifiedPropertiesWithoutUndo();
        if (stone.GetComponentInChildren<Collider>() == null) stone.AddComponent<BoxCollider>();
    }

    // Wading into the lava fields burns (the GDD: lava damages rather than kills).
    private static void AddLavaHazards(Scene world)
    {
        var old = world.GetRootGameObjects().FirstOrDefault(go => go.name == "Lava hazards");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Lava hazards");
        foreach (var name in new[] { "Lava fields/Tube lava river", "Lava fields/Flank lava river", "Lava fields/Lava lake", "Lava tube/Tube lava channel" })
        {
            var lava = GameObject.Find("Redwood Lava Cavern/" + name);
            var surface = lava.GetComponent<MeshCollider>() ?? lava.AddComponent<MeshCollider>();
            var bounds = lava.GetComponent<Renderer>().bounds;
            var go = new GameObject(lava.name + " hazard") { layer = 2 };
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(bounds.center.x, bounds.max.y - 2f, bounds.center.z);
            go.AddComponent<BoxCollider>().size = new Vector3(bounds.size.x, 8f, bounds.size.z);
            var hazard = new SerializedObject(go.AddComponent<HazardZone>());
            hazard.FindProperty("damagePerSecond").floatValue = 20f;
            hazard.FindProperty("element").enumValueIndex = (int)ElementType.Fire;
            hazard.FindProperty("surface").objectReferenceValue = surface;
            hazard.FindProperty("firstTimeLine").stringValue = StoryScript.LavaLine;
            hazard.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
