using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Creates the item data (healing potion, the four elemental tomes), gives the player an
// inventory, makes vampires drop Wubucks, and adds the lake drowning hazard to the World.
// Safe to re-run: assets and components are reused when they already exist.
public static class ItemsSetup
{
    public const string ItemFolder = "Assets/Items";
    public const string PotionPath = ItemFolder + "/Healing Potion.asset";
    public const string CoinPath = "Assets/Prefabs/Pickups/Wubucks coin.prefab";

    [MenuItem("Wulang/Setup/Items, loot and hazards")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        if (!AssetDatabase.IsValidFolder(ItemFolder)) AssetDatabase.CreateFolder("Assets", "Items");

        var potion = Item(PotionPath, "Healing Potion", ItemCategory.Consumable, 5, 40f, ElementType.None);
        var tomes = new[]
        {
            Tome(ElementType.Lightning),
            Tome(ElementType.Earth),
            Tome(ElementType.Ice),
            Tome(ElementType.Fire),
        };

        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>().gameObject;
        var inventory = player.GetComponent<PlayerInventory>() ?? player.AddComponent<PlayerInventory>();
        var settings = new SerializedObject(inventory);
        settings.FindProperty("inputActions").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/PlayerControls.inputactions");
        settings.FindProperty("healingPotion").objectReferenceValue = potion;
        var list = settings.FindProperty("tomes");
        list.arraySize = tomes.Length;
        for (int i = 0; i < tomes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = tomes[i];
        settings.ApplyModifiedPropertiesWithoutUndo();

        AddLakeHazard();
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);

        // Every vampire drops 1-3 Wubucks.
        var coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPath);
        foreach (var element in new[] { "Earth", "Fire", "Ice", "Lightning" })
        {
            string path = $"Assets/Combat/{element} vampire.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var loot = root.GetComponent<LootDrop>() ?? root.AddComponent<LootDrop>();
                var lootSettings = new SerializedObject(loot);
                lootSettings.FindProperty("coinPrefab").objectReferenceValue = coin;
                lootSettings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("ItemsSetup: items, inventory, vampire loot and lake hazard ready.");
    }

    private static ItemData Tome(ElementType element) =>
        Item($"{ItemFolder}/{element} Tome.asset", $"Tome of {element}", ItemCategory.Tome, 0, 0f, element);

    // Game values only: an item's lore text is written on its asset in the Inspector and never overwritten here.
    private static ItemData Item(string path, string name, ItemCategory category, int price, float heal, ElementType element)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.displayName = name;
        item.category = category;
        item.price = price;
        item.healAmount = heal;
        item.element = element;
        EditorUtility.SetDirty(item);
        return item;
    }

    // The unfrozen lake drowns: a hazard box from waist depth down, so shallows and the frozen
    // lake are safe but wading into open water hurts and hauls Wulang back ashore.
    private static void AddLakeHazard()
    {
        var existing = GameObject.Find("Lake drowning hazard");
        if (existing != null) Object.DestroyImmediate(existing);
        var ice = GameObject.Find("WaterFreezeController/Ice").GetComponent<Renderer>().bounds;
        var go = new GameObject("Lake drowning hazard");
        go.layer = 2; // Ignore Raycast: spell aiming must pass through the volume.
        var box = go.AddComponent<BoxCollider>();
        float top = ice.max.y - 1.5f, bottom = -20f;
        go.transform.position = new Vector3(ice.center.x, (top + bottom) / 2f, ice.center.z);
        box.size = new Vector3(ice.size.x, top - bottom, ice.size.z);
        var hazard = go.AddComponent<HazardZone>();
        var settings = new SerializedObject(hazard);
        settings.FindProperty("damagePerSecond").floatValue = 12f;
        settings.FindProperty("element").enumValueIndex = (int)ElementType.Ice;
        settings.FindProperty("returnAfter").floatValue = 1.5f;
        settings.FindProperty("safeWhenFlag").stringValue = Flags.LakeFrozen;
        settings.FindProperty("firstTimeLine").stringValue = StoryScript.LakeLine;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
