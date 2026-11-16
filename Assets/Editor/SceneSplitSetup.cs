using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Splits the single open-world SampleScene into a persistent World scene plus one scene per
// story region (Forest, Ice, Volcano, FinalArena) and a MainMenu scene. The World keeps the
// terrain, sky, player, HUD and existing cutscenes; each region scene holds that level's
// gameplay and a RegionStart. Run once, from SampleScene.
public static class SceneSplitSetup
{
    public const string WorldPath = "Assets/Scenes/World.unity";
    public const string RegionFolder = "Assets/Scenes/Regions";
    public const string MenuPath = "Assets/Scenes/MainMenu.unity";
    public static readonly string[] Regions = { "Forest", "Ice", "Volcano", "FinalArena" };

    // World XZ rectangles of each region's playable domain (from the top-down terrain map).
    public static readonly Rect IceDomain = Rect.MinMaxRect(420f, 130f, 890f, 360f);
    public static readonly Rect VolcanoDomain = Rect.MinMaxRect(640f, 450f, 900f, 840f);
    // The final arena floats above the middle of the map so the whole world is visible below.
    public static readonly Vector3 ArenaCenter = new(500f, 700f, 500f);

    public static string RegionPath(string region) => $"{RegionFolder}/{region}.unity";

    [MenuItem("Wulang/Scenes/Split world into regions")]
    public static void Split()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldPath) != null)
            throw new System.InvalidOperationException("World.unity already exists; the split has been run.");
        var world = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        if (player == null) throw new System.InvalidOperationException("SampleScene has no player.");
        MakePrefabs();
        ConfigurePlayer(player.gameObject);

        // Save, rename on disk, then reopen by the new path so additive scenes can be created beside it.
        EditorSceneManager.SaveScene(world);
        string moved = AssetDatabase.MoveAsset("Assets/Scenes/SampleScene.unity", WorldPath);
        if (!string.IsNullOrEmpty(moved)) throw new System.InvalidOperationException(moved);
        world = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
        player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();

        if (!AssetDatabase.IsValidFolder(RegionFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "Regions");
        var scenes = new Dictionary<string, Scene>();
        foreach (var region in Regions)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            // Unity refuses a second untitled additive scene, so give each one its path now.
            EditorSceneManager.SaveScene(scene, RegionPath(region));
            scenes[region] = scene;
        }

        // Forest: the starting lowlands keep the training vampires and the coin.
        foreach (var name in new[] { "Combat training", "Yeopjeon" })
        {
            var go = Root(world, name);
            if (go != null) SceneManager.MoveGameObjectToScene(go, scenes["Forest"]);
        }
        // Books are no longer loose pickups: Lightning is the starting element, the rest are boss rewards.
        var books = Root(world, "SpellBooks");
        if (books != null) Object.DestroyImmediate(books);

        var t = player.transform;
        AddRegionStart(scenes["Forest"], t.position, t.eulerAngles.y, Flags.Has(ElementType.Lightning));
        AddRegionStart(scenes["Ice"], Flattest(new Vector2(655f, 330f), 12f), 180f,
            Flags.Has(ElementType.Lightning), Flags.Has(ElementType.Earth), Flags.BossEarth, Flags.LakeFrozen);
        AddRegionStart(scenes["Volcano"], Flattest(new Vector2(655f, 690f), 30f), 90f,
            Flags.Has(ElementType.Lightning), Flags.Has(ElementType.Earth), Flags.BossEarth, Flags.LakeFrozen,
            Flags.Has(ElementType.Ice), Flags.BossIce);
        AddRegionStart(scenes["FinalArena"], ArenaCenter + new Vector3(0f, 1f, -24f), 0f,
            Flags.Has(ElementType.Lightning), Flags.Has(ElementType.Earth), Flags.BossEarth, Flags.LakeFrozen,
            Flags.Has(ElementType.Ice), Flags.BossIce, Flags.Has(ElementType.Fire), Flags.BossFire);

        AddRegionManager(world);
        GateStatue(world);

        foreach (var region in Regions)
            EditorSceneManager.SaveScene(scenes[region], RegionPath(region));
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        foreach (var region in Regions) EditorSceneManager.CloseScene(scenes[region], true);

        CreateMenuScene();
        EditorBuildSettings.scenes = new[] { MenuPath, WorldPath }.Concat(Regions.Select(RegionPath))
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("SceneSplitSetup: World + 4 region scenes + MainMenu created and added to Build Settings.");
    }

    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

    // Book and coin prefabs: bosses drop books, vampires drop coins.
    private static void MakePrefabs()
    {
        const string folder = "Assets/Prefabs/Pickups";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Pickups");
        var books = GameObject.Find("SpellBooks");
        if (books != null)
            foreach (Transform book in books.transform)
                PrefabUtility.SaveAsPrefabAsset(book.gameObject, $"{folder}/{book.name}.prefab");
        var coin = GameObject.Find("Yeopjeon");
        if (coin != null) PrefabUtility.SaveAsPrefabAsset(coin, $"{folder}/Wubucks coin.prefab");
    }

    private static void ConfigurePlayer(GameObject player)
    {
        if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
        var affinity = player.GetComponentInChildren<PlayerMagicAffinity>();
        var settings = new SerializedObject(affinity);
        settings.FindProperty("startingElement").enumValueIndex = (int)ElementType.Lightning;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddRegionStart(Scene scene, Vector3 position, float yaw, params string[] prerequisites)
    {
        var start = new GameObject("RegionStart");
        SceneManager.MoveGameObjectToScene(start, scene);
        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(start.transform, false);
        spawn.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        var component = start.AddComponent<RegionStart>();
        var settings = new SerializedObject(component);
        settings.FindProperty("spawn").objectReferenceValue = spawn;
        var list = settings.FindProperty("prerequisites");
        list.arraySize = prerequisites.Length;
        for (int i = 0; i < prerequisites.Length; i++) list.GetArrayElementAtIndex(i).stringValue = prerequisites[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    // The gentlest ground within a radius, so spawns and arenas never land on a cliff face.
    public static Vector3 Flattest(Vector2 center, float radius)
    {
        var data = Terrain.activeTerrain.terrainData;
        Vector2 best = center;
        float bestScore = float.MaxValue;
        for (float x = -radius; x <= radius; x += 3f)
            for (float z = -radius; z <= radius; z += 3f)
            {
                var p = center + new Vector2(x, z);
                if ((p - center).magnitude > radius) continue;
                float slope = 0f;
                // Worst slope in a 6 m neighbourhood, plus a mild pull toward the requested centre.
                for (int i = 0; i < 8; i++)
                {
                    var q = p + new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * 6f;
                    slope = Mathf.Max(slope, data.GetSteepness(q.x / data.size.x, q.y / data.size.z));
                }
                float score = slope + (p - center).magnitude * 0.05f;
                if (score < bestScore) { bestScore = score; best = p; }
            }
        return Ground(best);
    }

    public static Vector3 Ground(Vector2 xz)
    {
        var terrain = Terrain.activeTerrain;
        var point = new Vector3(xz.x, 0f, xz.y);
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.1f;
        return point;
    }

    private static void AddRegionManager(Scene world)
    {
        var go = new GameObject("RegionManager");
        SceneManager.MoveGameObjectToScene(go, world);
        var manager = go.AddComponent<RegionManager>();
        var settings = new SerializedObject(manager);
        var regions = settings.FindProperty("regions");
        regions.arraySize = 4;
        Region(regions.GetArrayElementAtIndex(0), "Forest", "Chapter I  -  [The Forest]",
            "[Placeholder recap: Wulang wakes in the lowlands with only the spark of Lightning.]", "", null, false, default, "");
        Region(regions.GetArrayElementAtIndex(1), "Ice", "Chapter II  -  [The Frozen Reach]",
            "[Placeholder recap: The Earth Guardian has fallen. The statue froze the lake.]", Flags.BossEarth,
            Cutscene("Ice Biome Preview"), false, IceDomain, "[The ice will not carry me yet.]");
        Region(regions.GetArrayElementAtIndex(2), "Volcano", "Chapter III  -  [The Burning Peak]",
            "[Placeholder recap: With Ice in hand, the Fire Guardian's seal finally weakens.]", Flags.BossIce,
            Cutscene("Volcano Preview"), false, VolcanoDomain, "[The Fire Guardian's seal burns me back. Not yet.]");
        Region(regions.GetArrayElementAtIndex(3), "FinalArena", "Chapter IV  -  [The Sanctum]",
            "[Placeholder recap: Every element answers to Wulang now. The rift leads to the one who sent her.]",
            Flags.BossFire, null, true, default, "");
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static FreezeRevealCutscene Cutscene(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<FreezeRevealCutscene>() : null;
    }

    private static void Region(SerializedProperty region, string scene, string title, string recap, string flag,
        FreezeRevealCutscene cutscene, bool whiteFade, Rect domain, string lockedLine)
    {
        region.FindPropertyRelative("scene").stringValue = scene;
        region.FindPropertyRelative("title").stringValue = title;
        region.FindPropertyRelative("recap").stringValue = recap;
        region.FindPropertyRelative("requiredFlag").stringValue = flag;
        region.FindPropertyRelative("cutscene").objectReferenceValue = cutscene;
        region.FindPropertyRelative("whiteFade").boolValue = whiteFade;
        region.FindPropertyRelative("domainMin").vector2Value = domain.min;
        region.FindPropertyRelative("domainMax").vector2Value = domain.max;
        region.FindPropertyRelative("lockedLine").stringValue = lockedLine;
    }

    // The statue only answers once the Earth Guardian is beaten.
    private static void GateStatue(Scene world)
    {
        var statue = Root(world, "Ice statue");
        var interactable = statue.GetComponentInChildren<Interactable>();
        var gate = statue.AddComponent<FlagGate>();
        var settings = new SerializedObject(gate);
        settings.FindProperty("flag").stringValue = Flags.BossEarth;
        var components = settings.FindProperty("components");
        components.arraySize = 1;
        components.GetArrayElementAtIndex(0).objectReferenceValue = interactable;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.035f, 0.03f);
        new GameObject("MainMenu", typeof(MainMenu));
        EditorSceneManager.SaveScene(scene, MenuPath);
    }
}
