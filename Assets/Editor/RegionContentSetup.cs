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
        Encounter(forestRoot, "Earth Guardian arena", forestArena, BossSetup.EarthBoss, "[The Earth Guardian]", Flags.BossEarth,
            "Earth Book", 20f, 24f,
            "[Earth Guardian: Another book-bearer, sent to take what is mine...]",
            "[Earth Guardian (dying): You carry us... to him? Then you are already lost...]");
        Abe(forestRoot, Ground(566f, 596f), Ground(566f, 604.6f), "[Abe: Wulang! You're awake. Strike the beast with Lightning; earth fears the storm.]");
        // Two vampires guard the trail from the armory to the glade: the tutorial's first fights.
        Vampire(forestRoot, "Earth", SceneSplitSetup.Flattest(new Vector2(546f, 530f), 6f));
        Vampire(forestRoot, "Lightning", SceneSplitSetup.Flattest(new Vector2(520f, 490f), 6f));
        Save(forest);

        var ice = Open("Ice", out var iceRoot);
        var iceSpawn = RegionStartSpawn(ice);
        // The Frozen Amphitheater built by SnowBiomeBuilder.
        var iceArena = SceneSplitSetup.Ground(SnowBiomeBuilder.ArenaCenter);
        FaceSpawn(ice, iceArena);
        Encounter(iceRoot, "Ice Guardian arena", iceArena, BossSetup.IceBoss,
            "[The Ice Guardian]", Flags.BossIce, "Ice Book", 18f, 22f,
            "[Ice Guardian: The cold remembers your face, thief.]",
            "[Ice Guardian (dying): He wears a kind face. He always has...]");
        Abe(iceRoot, iceSpawn + new Vector3(6f, 0f, -4f), iceSpawn,
            "[Abe: Fire would melt it, but you have none. Ground it with Earth, then call the storm.]");
        foreach (var p in new[] { new Vector2(600f, 262f), new Vector2(712f, 252f), new Vector2(770f, 300f) })
            Vampire(iceRoot, "Ice", SceneSplitSetup.Flattest(p, 12f));
        Travel(iceRoot, "Mountain pass waystone", iceSpawn + new Vector3(-6f, 0f, -3f), "Volcano", Flags.BossIce,
            "Press F to cross the mountain pass");
        Save(ice);

        var volcano = Open("Volcano", out var volcanoRoot);
        var volcanoSpawn = RegionStartSpawn(volcano);
        var caldera = GameObject.Find("Redwood Lava Cavern/Boss caldera/Boss spawn point").transform.position;
        Encounter(volcanoRoot, "Fire Guardian arena", new Vector3(795f, caldera.y, 642f), BossSetup.FireBoss, "[The Fire Guardian]",
            Flags.BossFire, "Fire Book", 13f, 14f,
            "[Fire Guardian: Burn, little vessel. Burn before he fills you.]",
            "[Fire Guardian (dying): Four elements... one throne. Do not kneel...]", caldera);
        Abe(volcanoRoot, volcanoSpawn + new Vector3(4f, 0f, 5f), volcanoSpawn,
            "[Abe: The Fire Guardian waits inside the caldera. Ice will tame it. Go, I'll be close.]");
        foreach (var p in new[] { new Vector2(680f, 720f), new Vector2(700f, 780f), new Vector2(665f, 650f) })
            Vampire(volcanoRoot, "Fire", SceneSplitSetup.Flattest(p, 12f));
        Travel(volcanoRoot, "Caldera rift", new Vector3(808f, caldera.y, 653f), "FinalArena", Flags.BossFire,
            "Press F to step into the rift", onTerrain: false);
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

    private static void Abe(Transform parent, Vector3 position, Vector3 lookAt, string greeting)
    {
        position = SceneSplitSetup.Ground(new Vector2(position.x, position.z));
        var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(lookAt - position, Vector3.up));
        var abe = Place(BossSetup.AbePrefab, parent, position, facing);
        var merchant = new SerializedObject(abe.GetComponent<Merchant>());
        merchant.FindProperty("greeting").stringValue = greeting;
        merchant.ApplyModifiedPropertiesWithoutUndo();
    }

    // Field vampires respawn after 90 s, so the player can earn Wubucks for Abe's potions.
    private static void Vampire(Transform parent, string element, Vector3 position)
    {
        var vampire = Place($"Assets/Combat/{element} vampire.prefab", parent, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        var target = new SerializedObject(vampire.GetComponent<CombatTarget>());
        target.FindProperty("trainingRespawnDelay").floatValue = 90f;
        target.ApplyModifiedPropertiesWithoutUndo();
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
            hazard.FindProperty("firstTimeLine").stringValue = "[The lava sears through my boots!]";
            hazard.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
