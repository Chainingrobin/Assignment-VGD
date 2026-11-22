using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The armory compound, laid out with the Stylized Fantasy Armory pack around the tower's cobbled
// yard so it reads as a working outpost instead of a tower on an empty square:
//  - a fence around the yard, open to the north path (towards where Wulang wakes) and to the
//    south-west trail to the Blossom Glade;
//  - a training ground on the west side (dummies, targets, a bow and a weapon rack);
//  - a smithy on the east side (forge oven, anvil on a crate, bellows, crafting bench, racks);
//  - a supply corner behind the tower (wagon, chests, crates, barrels);
//  - braziers with firelight at the gate, the arches on the path and the yard's corners.
// It replaces the loose props that stood in the yard. Abe's camp beside the path belongs to the
// Forest region (RegionContentSetup). Re-runnable: "Armory/Compound" is rebuilt from scratch.
public static class ArmorySetup
{
    private const string Pack = "Assets/Stylized Fantasy Armory/Prefabs/";
    private static readonly string[] Loose =
    {
        "Wagon.002", "CombatDummy.001", "CombatDummy.002", "CombatDummy.003", "ChestS.001", "ChestL.001 1", "CauldronandCampfire",
        "Bucket.001 1", "Barrel.001 1", "Barrel.001", "Chair", "WeaponRack.002", "WeaponRack.001 1", "WeaponRack.003",
    };

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

        // Fence: the yard is about x 549-583, z 536-568 around the tower at (565, 547).
        var fences = Group(root, "Fence");
        Fence(fences, new Vector2(549f, 556f), new Vector2(549f, 568f));
        Fence(fences, new Vector2(549f, 568f), new Vector2(560f, 568f));
        Fence(fences, new Vector2(572f, 568f), new Vector2(583f, 568f));
        Fence(fences, new Vector2(583f, 568f), new Vector2(583f, 536f));
        Fence(fences, new Vector2(583f, 536f), new Vector2(560f, 536f));

        var fire = Group(root, "Braziers");
        foreach (var p in new[] { new Vector2(559.3f, 569.6f), new Vector2(572.7f, 569.6f), new Vector2(561.3f, 581.5f), new Vector2(570.3f, 581.5f),
                     new Vector2(561.5f, 599f), new Vector2(570.2f, 599f), new Vector2(583.8f, 568.8f), new Vector2(583.8f, 535.2f), new Vector2(548.2f, 554.5f) })
        {
            var brazier = Prop(fire, "Decorative Props/Torch/Torch.prefab", p, 0f);
            Glow(brazier.transform, Vector3.up * 1.3f, new Color(1f, 0.58f, 0.25f), 11f, 2.2f);
        }

        // Training ground, west: dummies face the yard, targets with a bow and arrows beside them.
        var training = Group(root, "Training ground");
        for (int i = 0; i < 3; i++)
            Prop(training, $"Decorative Props/Combat Dummy/CombatDummy.00{i + 1}.prefab", new Vector2(552.3f, 558f + i * 3f), 90f);
        Prop(training, "Decorative Props/Target/Target.prefab", new Vector2(551.8f, 566.4f), 120f);
        Prop(training, "Decorative Props/Target/Target.prefab", new Vector2(554.3f, 566.9f), 160f);
        Prop(training, "Decorative Props/Weapon Racks/WeaponRack.004.prefab", new Vector2(557.5f, 566.9f), 180f);
        var stand = Prop(training, "Decorative Props/Box/Box.001.prefab", new Vector2(551.7f, 555.2f), 15f);
        OnTop(training, "Weapons and Shields/Arrows and Bow/Bow.prefab", stand, 30f);
        Prop(training, "Weapons and Shields/Arrows and Bow/ArrowBag.001.prefab", new Vector2(552.8f, 554.6f), 70f);

        // Smithy, east: the forge oven and crafting bench against the fence, the anvil on a crate.
        var smithy = Group(root, "Smithy");
        Prop(smithy, "Decorative Props/Oven/Oven Parts/Oven.prefab", new Vector2(581.4f, 552f), -90f);
        Prop(smithy, "Extra Content/Fantasy Workshops and Crafting Vol2/SFWC2_Crafting_Table.prefab", new Vector2(581.2f, 545f), -90f);
        var anvilStand = Prop(smithy, "Decorative Props/Box/Box.001.prefab", new Vector2(577.6f, 550.4f), 10f);
        OnTop(smithy, "Decorative Props/Anvil and Airblower/Anvil.prefab", anvilStand, 100f);
        Prop(smithy, "Decorative Props/Anvil and Airblower/Airblower.prefab", new Vector2(579.2f, 549.6f), -60f, lift: 0.14f);
        Prop(smithy, "Decorative Props/Weapon Racks/WeaponRack.004.prefab", new Vector2(575.6f, 557.4f), 0f);
        Prop(smithy, "Decorative Props/Weapon Racks/WeaponRack.005.prefab", new Vector2(578.4f, 557.4f), 0f);
        Prop(smithy, "Decorative Props/Barrel/Barrel.002.prefab", new Vector2(581.9f, 557.9f), 0f);
        Prop(smithy, "Decorative Props/Bucket/Bucket Parts/Bucket.001.prefab", new Vector2(580.7f, 558.6f), 30f);

        // Supply corner, behind the tower: Abe's suppliers unload here.
        var supply = Group(root, "Supply");
        Prop(supply, "Wagon/Wagon.001.prefab", new Vector2(576.8f, 539.4f), 90f);
        Prop(supply, "Decorative Props/Chest/Chest Parts/ChestL.001.prefab", new Vector2(571.2f, 537.9f), 0f);
        var crate = Prop(supply, "Decorative Props/Box/Box.001 2.prefab", new Vector2(568.2f, 538.3f), 8f);
        OnTop(supply, "Decorative Props/Box/Box.001.prefab", crate, 40f);
        Prop(supply, "Decorative Props/Barrel/Barrel.001.prefab", new Vector2(565.6f, 537.8f), 0f);
        Prop(supply, "Decorative Props/Barrel/Barrel.002.prefab", new Vector2(564.6f, 538.9f), 0f);
        Prop(supply, "Decorative Props/Vases/Vase.003.prefab", new Vector2(562.8f, 537.9f), 0f);

        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        Debug.Log("ArmorySetup: compound dressed (fence, braziers, training ground, smithy, supply corner).");
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
