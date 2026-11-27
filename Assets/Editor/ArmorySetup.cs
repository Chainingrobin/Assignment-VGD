using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The armory compound, laid out with the Stylized Fantasy Armory pack around the tower's cobbled
// yard so it reads as a working outpost instead of a tower on an empty square:
//  - a fence around the yard, open to the north path (towards where Wulang wakes) and to the
//    south-west trail to the Blossom Glade;
//  - a quartermaster's corner at the front left (shelves of books and bottles, a desk, a chest);
//  - an armourer's table and racks of finished weapons at the front right;
//  - a training ground on the west side by the trail out (dummies, targets, a bow and a weapon rack);
//  - a smithy on the east side (forge oven, anvil on a crate, bellows, cook fire, crafting bench, racks);
//  - a supply corner behind the tower (wagons, chests, crates, barrels);
//  - braziers with firelight at the gate, the arches on the path and the yard's corners, and lamps.
// The tower stands at its natural size and the yard is sized to it (26 x 30 m): it was scaled up 1.36
// times in a 34 x 32 m square, which made the compound huge and empty. The paving outside the new
// fence is painted back to grass. It replaces the loose props that stood in the yard. Abe's camp
// beside the path belongs to the Forest region (RegionContentSetup). Re-runnable: "Armory/Compound"
// is rebuilt from scratch.
public static class ArmorySetup
{
    private const string Pack = "Assets/Stylized Fantasy Armory/Prefabs/";
    private static readonly string[] Loose =
    {
        "Wagon.002", "CombatDummy.001", "CombatDummy.002", "CombatDummy.003", "ChestS.001", "ChestL.001 1", "CauldronandCampfire",
        "Bucket.001 1", "Barrel.001 1", "Barrel.001", "Chair", "WeaponRack.002", "WeaponRack.001 1", "WeaponRack.003",
    };
    // The yard inside the fence, around the tower at (565.2, 547.1).
    public static readonly Rect Yard = new(552.5f, 535.5f, 26f, 30.5f);
    // Where the yard used to reach: its paving is painted back to grass outside Yard.
    private static readonly Rect OldYard = new(546f, 533f, 40f, 37f);

    [MenuItem("Wulang/Forest/Dress the armory compound")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var armory = GameObject.Find("Armory").transform;
        foreach (var name in Loose)
        {
            var loose = armory.Find(name);
            if (loose != null) Object.DestroyImmediate(loose.gameObject);
        }
        var old = armory.Find("Compound");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Compound").transform;
        root.SetParent(armory, false);
        var tower = armory.Find("Armory (1)");
        if (tower != null) tower.localScale = Vector3.one;
        Repave();

        float west = Yard.xMin, east = Yard.xMax, south = Yard.yMin, north = Yard.yMax;
        var fences = Group(root, "Fence");
        Fence(fences, new Vector2(west, 554f), new Vector2(west, north));
        Fence(fences, new Vector2(west, north), new Vector2(560f, north));
        Fence(fences, new Vector2(571f, north), new Vector2(east, north));
        Fence(fences, new Vector2(east, north), new Vector2(east, south));
        Fence(fences, new Vector2(east, south), new Vector2(560f, south));

        var fire = Group(root, "Braziers");
        foreach (var p in new[] { new Vector2(559.3f, north + 1.6f), new Vector2(571.7f, north + 1.6f), new Vector2(561.3f, 581.5f), new Vector2(570.3f, 581.5f),
                     new Vector2(561.5f, 599f), new Vector2(570.2f, 599f), new Vector2(east + 1f, north + 1f), new Vector2(east + 1f, south - 1f), new Vector2(west - 1f, 553f) })
        {
            var brazier = Prop(fire, "Decorative Props/Torch/Torch.prefab", p, 0f);
            Glow(brazier.transform, Vector3.up * 1.3f, new Color(1f, 0.58f, 0.25f), 11f, 2.2f);
        }

        // Quartermaster, front left: shelves against the fence, a desk with a book, a chest by the gate.
        var stores = Group(root, "Quartermaster");
        foreach (var (z, shelf) in new[] { (556.6f, 1), (560.1f, 2), (563.6f, 3) })
            Prop(stores, $"Combinations/Shelve.00{shelf}.prefab", new Vector2(west + 0.7f, z), 90f);
        var desk = Prop(stores, "Decorative Props/Table/Table.prefab", new Vector2(556.4f, 560f), 90f);
        OnTop(stores, "Decorative Props/Books/Book.003.prefab", desk, 20f);
        Prop(stores, "Decorative Props/Chair/Chair.prefab", new Vector2(557.9f, 559.8f), -90f);
        Prop(stores, "Decorative Props/Chest/Chest Parts/ChestL.001.prefab", new Vector2(556.2f, 565f), 180f);
        Prop(stores, "Decorative Props/Barrel/Barrel.002.prefab", new Vector2(553.6f, 565.2f), 0f);
        Prop(stores, "Decorative Props/Lamp/Lamp.002.prefab", new Vector2(559.4f, north - 0.6f), 0f);

        // Armourer, front right: a table of work in hand, weapon racks and a crate of helmets.
        var front = Group(root, "Armourer");
        Prop(front, "Combinations/Table 1.prefab", new Vector2(575.2f, 562.6f), 180f);
        Prop(front, "Combinations/WeaponRack.001.prefab", new Vector2(east - 0.8f, 558.8f), -90f);
        Prop(front, "Combinations/WeaponRack.002.prefab", new Vector2(east - 0.8f, 562.6f), -90f);
        Prop(front, "Combinations/WeaponRack.003.prefab", new Vector2(574.2f, north - 0.8f), 180f);
        var helmets = Prop(front, "Decorative Props/Box/Box.001 1.prefab", new Vector2(572.6f, 559.2f), 10f);
        OnTop(front, "Decorative Props/Helmet/Helmet.001.prefab", helmets, 160f);
        Prop(front, "Decorative Props/Lamp/Lamp.002.prefab", new Vector2(571.6f, north - 0.6f), 0f);

        // Training ground, west by the trail out: dummies face the yard, targets with a bow and arrows beside them.
        var training = Group(root, "Training ground");
        for (int i = 0; i < 3; i++)
            Prop(training, $"Decorative Props/Combat Dummy/CombatDummy.00{i + 1}.prefab", new Vector2(west + 1.2f, 543.5f + i * 3f), 90f);
        Prop(training, "Decorative Props/Target/Target.prefab", new Vector2(554f, 539.6f), 40f);
        Prop(training, "Decorative Props/Target/Target.prefab", new Vector2(556.8f, 538.6f), 10f);
        Prop(training, "Decorative Props/Weapon Racks/WeaponRack.004.prefab", new Vector2(556.4f, 552.6f), 0f);
        var stand = Prop(training, "Decorative Props/Box/Box.001.prefab", new Vector2(554.1f, 552.4f), 15f);
        OnTop(training, "Weapons and Shields/Arrows and Bow/Bow.prefab", stand, 30f);
        Prop(training, "Weapons and Shields/Arrows and Bow/ArrowBag.001.prefab", new Vector2(555.1f, 551.7f), 70f);

        // Smithy, east: the forge oven and crafting bench against the fence, the anvil on a crate, the cook fire.
        var smithy = Group(root, "Smithy");
        Prop(smithy, "Decorative Props/Oven/Oven Parts/Oven.prefab", new Vector2(east - 1.2f, 550.6f), -90f);
        Prop(smithy, "Extra Content/Fantasy Workshops and Crafting Vol2/SFWC2_Crafting_Table.prefab", new Vector2(east - 1.2f, 544.6f), -90f);
        var anvilStand = Prop(smithy, "Decorative Props/Box/Box.001.prefab", new Vector2(574.4f, 549.4f), 10f);
        OnTop(smithy, "Decorative Props/Anvil and Airblower/Anvil.prefab", anvilStand, 100f);
        Prop(smithy, "Decorative Props/Anvil and Airblower/Airblower.prefab", new Vector2(575.9f, 548.4f), -60f, lift: 0.14f);
        Prop(smithy, "Decorative Props/Cauldron and Campfire/CauldronandCampfire.prefab", new Vector2(574.6f, 541.4f), 0f);
        Prop(smithy, "Decorative Props/Weapon Racks/WeaponRack.004.prefab", new Vector2(573.4f, 555.6f), 0f);
        Prop(smithy, "Decorative Props/Weapon Racks/WeaponRack.005.prefab", new Vector2(576.2f, 555.6f), 0f);
        Prop(smithy, "Decorative Props/Barrel/Barrel.002.prefab", new Vector2(east - 0.8f, 556.4f), 0f);
        Prop(smithy, "Decorative Props/Bucket/Bucket Parts/Bucket.001.prefab", new Vector2(576.9f, 553.6f), 30f);
        Prop(smithy, "Decorative Props/Lamp/Lamp.002.prefab", new Vector2(east - 0.5f, 553.4f), -90f);
        // The log pile sits just outside the fence, by the forge.
        Prop(smithy, "Nature/Wooden Logs/WoodenLog.001.prefab", new Vector2(east + 2.2f, 546f), 0f);
        Prop(smithy, "Nature/Wooden Logs/WoodenLog.002.prefab", new Vector2(east + 3.4f, 548.4f), 15f);

        // Supply corner, behind the tower: Abe's suppliers unload here.
        var supply = Group(root, "Supply");
        Prop(supply, "Wagon/Wagon.001.prefab", new Vector2(574.6f, 537.6f), 90f);
        Prop(supply, "Combinations/Wagon.003.prefab", new Vector2(560.2f, 537.7f), 80f);
        Prop(supply, "Decorative Props/Chest/Chest Parts/ChestL.001.prefab", new Vector2(569.6f, 536.6f), 0f);
        var crate = Prop(supply, "Decorative Props/Box/Box.001 2.prefab", new Vector2(567.1f, 536.9f), 8f);
        OnTop(supply, "Decorative Props/Box/Box.001.prefab", crate, 40f);
        var bagStand = Prop(supply, "Decorative Props/Box/Box.002.prefab", new Vector2(565f, 536.7f), -12f);
        OnTop(supply, "Decorative Props/Coin and Bag/MoneyBag.prefab", bagStand, 30f);
        Prop(supply, "Decorative Props/Barrel/Barrel.001.prefab", new Vector2(563.4f, 537.1f), 0f);
        Prop(supply, "Decorative Props/Barrel/Barrel.001 1.prefab", new Vector2(563.6f, 538.5f), 0f);
        Prop(supply, "Decorative Props/Vases/Vase.003.prefab", new Vector2(571.6f, 537f), 0f);
        Prop(supply, "Decorative Props/Chest/ChestS.001.prefab", new Vector2(577.2f, 540.2f), -80f);

        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        Debug.Log("ArmorySetup: compound dressed (fence, braziers, quartermaster, armourer, training ground, smithy, supply corner).");
    }

    // Paints the old yard's paving outside the new fence back to grass (softly, over a metre and a half),
    // keeping the path in from the north gate and the trail out to the south-west.
    private static void Repave()
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        int grass = System.Array.FindIndex(data.terrainLayers, l => l != null && l.name == "Grass_Layer");
        if (grass < 0) return;
        float cell = data.size.x / data.alphamapResolution;
        var origin = terrain.transform.position;
        int x0 = Mathf.FloorToInt((OldYard.xMin - origin.x) / cell), z0 = Mathf.FloorToInt((OldYard.yMin - origin.z) / cell);
        int w = Mathf.CeilToInt(OldYard.width / cell), h = Mathf.CeilToInt(OldYard.height / cell);
        var maps = data.GetAlphamaps(x0, z0, w, h);
        int layers = data.alphamapLayers;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(origin.x + (x0 + x + 0.5f) * cell, origin.z + (z0 + z + 0.5f) * cell);
                bool gate = p.x > 559.5f && p.x < 571.5f && p.y > Yard.yMax - 1f;
                bool trail = p.x < 561f && p.y < 546f;
                if (gate || trail) continue;
                float dx = Mathf.Max(Yard.xMin - p.x, p.x - Yard.xMax), dz = Mathf.Max(Yard.yMin - p.y, p.y - Yard.yMax);
                float outside = Mathf.Max(dx, dz);
                float amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 1.5f, outside));
                if (amount <= 0f) continue;
                for (int l = 0; l < layers; l++) maps[z, x, l] = Mathf.Lerp(maps[z, x, l], l == grass ? 1f : 0f, amount);
            }
        data.SetAlphamaps(x0, z0, maps);
        EditorUtility.SetDirty(data);
    }

    private static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    // Fence panels end to end from a to b, cycling the pack's three panel styles.
    private static void Fence(Transform parent, Vector2 a, Vector2 b)
    {
        var direction = (b - a).normalized;
        int panels = Mathf.CeilToInt((b - a).magnitude / 2.1f);
        float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        for (int i = 0; i < panels; i++)
            Prop(parent, $"Decorative Props/Fences/Fence.00{i % 3 + 1}.prefab", Vector2.Lerp(a, b, (i + 0.5f) / panels), yaw);
    }

    private static GameObject Prop(Transform parent, string path, Vector2 at, float yaw, float lift = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path);
        if (prefab == null) throw new System.IO.FileNotFoundException(Pack + path);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        prop.transform.SetPositionAndRotation(SceneSplitSetup.Ground(at) + Vector3.up * (lift - 0.1f), Quaternion.Euler(0f, yaw, 0f));
        // The pack's wagons carry WheelColliders, which need a rigidbody and only spam warnings on a prop.
        foreach (var wheel in prop.GetComponentsInChildren<WheelCollider>()) Object.DestroyImmediate(wheel);
        return prop;
    }

    // Stands a prop on top of another (the anvil on its crate, a bow on a box).
    private static void OnTop(Transform parent, string path, GameObject below, float yaw)
    {
        var bounds = new Bounds(below.transform.position, Vector3.zero);
        foreach (var renderer in below.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path), parent);
        prop.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.Euler(0f, yaw, 0f));
        // Some pivots sit above the model's base (the anvil's by 14 cm): rest its lowest point on the top.
        float lowest = float.MaxValue;
        foreach (var renderer in prop.GetComponentsInChildren<Renderer>()) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
        prop.transform.position += Vector3.up * (bounds.max.y - lowest);
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
}
