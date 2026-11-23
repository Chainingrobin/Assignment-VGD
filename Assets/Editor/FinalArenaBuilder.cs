using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the final arena in the FinalArena region scene: a round rock platform floating high
// above the map (the whole world is visible below), ringed by rocks and four element pillars
// whose lights show Ganfaul's current element. Abe waits at the centre as the disguise of the
// final BossEncounter; Ganfaul is hidden until the reveal. Re-runnable.
public static class FinalArenaBuilder
{
    private const float Radius = 28f;
    private const string Floor = "Assets/Blink/Art/Textures/StylizedLavaTextures/Rock_Ground/Rock_Ground.mat";
    private const string RimRock = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/Rock_fancy.prefab";
    private const string Pillar = "Assets/PBR_Statues_pack/Statues_Viking/Prefab/Stone_02.prefab";
    private const string Reveal = "Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab";

    [MenuItem("Wulang/Scenes/Build final arena")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("FinalArena"), OpenSceneMode.Additive);
        var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Arena");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Arena");
        SceneManager.MoveGameObjectToScene(root, scene);
        var center = SceneSplitSetup.ArenaCenter;
        root.transform.position = center;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Floating platform";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, -1f, 0f);
        floor.transform.localScale = new Vector3(Radius * 2f, 1f, Radius * 2f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = FloorMaterial();
        // The capsule-shaped default collider is wrong for a flat disc; use a mesh collider.
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.AddComponent<MeshCollider>();

        var rocks = new GameObject("Rim rocks").transform;
        rocks.SetParent(root.transform, false);
        for (int i = 0; i < 18; i++)
        {
            float angle = i * Mathf.PI * 2f / 18f + 0.1f;
            var rock = Place(RimRock, rocks, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius - 0.5f),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            rock.transform.localScale = Vector3.one * Random.Range(1.6f, 2.6f);
        }

        // Invisible wall so the fight can't knock Wulang off the edge too easily.
        var walls = new GameObject("Boundary").transform;
        walls.SetParent(root.transform, false);
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2f / 24f;
            var wall = new GameObject("Wall " + i).transform;
            wall.SetParent(walls, false);
            wall.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius + 0.5f) + Vector3.up * 4f;
            wall.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
            wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 1f);
        }

        // One pillar per element; ElementPhases lights the active one.
        var pillars = new Light[4];
        var pillarRoot = new GameObject("Element pillars").transform;
        pillarRoot.SetParent(root.transform, false);
        var elements = new[] { ElementType.Ice, ElementType.Fire, ElementType.Lightning, ElementType.Earth };
        for (int i = 0; i < 4; i++)
        {
            float angle = (45f + i * 90f) * Mathf.Deg2Rad;
            var position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius - 6f);
            var statue = Place(Pillar, pillarRoot, position, Quaternion.LookRotation(center - position));
            statue.name = elements[i] + " pillar";
            statue.transform.localScale *= 2.2f;
            var light = new GameObject(elements[i] + " light").AddComponent<Light>();
            light.transform.SetParent(statue.transform, false);
            light.transform.position = position + Vector3.up * 4f;
            light.type = LightType.Point;
            light.color = ElementVisuals.ColorFor(elements[i]);
            light.range = 18f;
            light.intensity = 0.3f;
            pillars[(int)elements[i] - 1] = light;
        }

        var arena = new GameObject("Final encounter").transform;
        arena.SetParent(root.transform, false);
        arena.position = center;
        // Abe sits at his camps; here he stands to make the offer (still gesturing as he talks).
        var abe = Place(BossSetup.AbePrefab, arena, center + Vector3.up * 0.07f, Quaternion.Euler(0f, 180f, 0f));
        abe.name = "Abe (disguise)";
        abe.GetComponentInChildren<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(BossSetup.StandingAbe);
        Object.DestroyImmediate(abe.GetComponent<Merchant>());
        Object.DestroyImmediate(abe.GetComponent<Interactable>());
        var ganfaul = Place(BossSetup.FinalBoss, arena, center + Vector3.up * 0.1f, Quaternion.Euler(0f, 180f, 0f));
        var brain = new SerializedObject(ganfaul.GetComponent<BossBrain>());
        brain.FindProperty("arenaRadius").floatValue = Radius - 4f;
        brain.ApplyModifiedPropertiesWithoutUndo();
        var phases = new SerializedObject(ganfaul.GetComponent<ElementPhases>());
        var list = phases.FindProperty("pillars");
        list.arraySize = 4;
        for (int i = 0; i < 4; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pillars[i];
        phases.ApplyModifiedPropertiesWithoutUndo();

        var encounter = arena.gameObject.AddComponent<BossEncounter>();
        var settings = new SerializedObject(encounter);
        settings.FindProperty("boss").objectReferenceValue = ganfaul.GetComponent<BossBrain>();
        settings.FindProperty("displayName").stringValue = StoryScript.Bosses[Flags.BossFinal].name;
        settings.FindProperty("defeatFlag").stringValue = Flags.BossFinal;
        settings.FindProperty("autoEngage").boolValue = false;
        settings.FindProperty("disguise").objectReferenceValue = abe;
        settings.FindProperty("revealEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Reveal);
        settings.FindProperty("engageRadius").floatValue = Radius;
        settings.FindProperty("introLine").stringValue = StoryScript.Bosses[Flags.BossFinal].intro;
        settings.FindProperty("dyingLine").stringValue = StoryScript.Bosses[Flags.BossFinal].dying;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var ending = arena.gameObject.AddComponent<EndingController>();
        var endingSettings = new SerializedObject(ending);
        endingSettings.FindProperty("encounter").objectReferenceValue = encounter;
        endingSettings.FindProperty("fallFloor").floatValue = center.y - 30f;
        endingSettings.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log("FinalArenaBuilder: arena built.");
    }

    private static GameObject Place(string path, Transform parent, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.IO.FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    private static Material FloorMaterial()
    {
        // The Blink material uses the Built-in Standard shader (magenta in URP), so rebuild it as URP Lit.
        const string path = "Assets/Bosses/Arena floor.mat";
        AssetDatabase.DeleteAsset(path);
        var source = AssetDatabase.LoadAssetAtPath<Material>(Floor);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Arena floor" };
        material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
        material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
        material.SetTexture("_OcclusionMap", source.GetTexture("_OcclusionMap"));
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_OCCLUSIONMAP");
        material.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
