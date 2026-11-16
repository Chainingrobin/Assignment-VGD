using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Lays the story onto the regions with placeholder text: dialogue assets, speaker portraits
// rendered from the character models, journal entries, the memory-echo look (ghost material
// and desaturation volume), and per region the objectives, narrative triggers, a found lore
// note and a memory echo. Also adds the objective tracker, inventory and pause menu to the
// World. Every line is a [placeholder] for the narrative pass. Re-runnable.
public static class StorySetup
{
    private const string Folder = "Assets/Story";
    private const string DialogueFolder = Folder + "/Dialogue";
    private const string JournalFolder = Folder + "/Journal";
    private const string PortraitFolder = Folder + "/Portraits";
    private const string NoteBook = "Assets/Stylized magic books/Prefab/Necromancy book.prefab";
    private const string Wulang = "Assets/Characters/Protagonist/Protagonist.fbx";

    private static Sprite abeFace, wulangFace, ganfaulFace;

    [MenuItem("Wulang/Story/Lay placeholder story onto regions")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        foreach (var folder in new[] { Folder, DialogueFolder, JournalFolder, PortraitFolder })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        abeFace = Portrait("Abe", BossSetup.AbePrefab);
        wulangFace = Portrait("Wulang", Wulang);
        ganfaulFace = Portrait("Ganfaul", BossSetup.FinalBoss);
        var ghost = GhostMaterial();
        var profile = MemoryProfile();
        WorldSystems(world);
        EditorSceneManager.SaveScene(world);

        Forest(ghost, profile);
        Ice(ghost, profile);
        Volcano(ghost, profile);
        Arena(ghost, profile);
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("StorySetup: placeholder story laid onto all regions.");
    }

    // ---------- regions ----------

    private static void Forest(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Forest", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root,
            ("Speak with [Abe] at the armory", "", "metAbe"),
            ("Defeat the Earth Guardian in the northern meadow", "metAbe", Flags.BossEarth),
            ("Claim the Tome of Earth from the Guardian", Flags.BossEarth, Flags.Has(ElementType.Earth)),
            ("Wake the ice statue by the lake", Flags.Has(ElementType.Earth), Flags.LakeFrozen),
            ("Cross the frozen lake to the Frozen Reach", Flags.LakeFrozen, ""));
        Trigger(root, "Prologue", abe.position, NarrativeTrigger.Mode.OnStart, once: "prologueSeen", delay: 1f,
            captionTitle: "[Wulang: Elements of the Past]",
            captionBody: "[Placeholder prologue: the land is sick with elemental corruption. A young woman wakes in the forest with no memory, a spark of Lightning in her hand, and a stranger's voice calling her name.]");
        Trigger(root, "Meet Abe", abe.position, NarrativeTrigger.Mode.Zone, radius: 7f, once: "metAbeTrigger", dialogue: Dialogue("Forest - Meet Abe", "metAbe",
            (Abe, "[Abe: Wulang! Thank the stars, you're awake. Do you remember me? No... the corruption took that too.]"),
            (Wu, "[Wulang: I remember... nothing. Only this spark.]"),
            (Abe, "[Abe: That spark is Lightning, and it's yours. A guardian gone mad holds the Earth north of here. Earth fears the storm: strike it with Lightning.]"),
            (Abe, "[Abe: Bring me Wubucks from those vampires and I'll keep you in potions. Press H to drink one.]"),
            (Wu, "[Wulang: And you? Will you fight with me?]"),
            (Abe, "[Abe: Me? I can't touch the elements, child. That's why I need you.]")));
        Note(root, abe.position + abe.right * 4f + abe.forward * 2f, "Torn ledger page",
            "[Placeholder lore: a ledger in Abe's hand, listing four guardians and a fifth line scratched out: 'the vessel'.]");
        Memory(root, "Memory I", arena, ghost, profile, Flags.Has(ElementType.Earth), Journal("Memory I - The Forest Home",
            "[Placeholder memory: Wulang as a child, Abe teaching her to listen to the soil. His shadow looks wrong in it.]"),
            (Wu, "[Young Wulang: Master Abe, the ground is singing!]"),
            (Abe, "[Abe: Of course it is. It sings for you, and you will give its song to me.]"),
            (Wu, "[Wulang: ...Did he say 'to me'? The memory flickers, and his shadow has horns.]"));
        Trigger(root, "Statue awakens", GameObject.Find("Ice statue").transform.position, NarrativeTrigger.Mode.Zone, radius: 14f,
            once: "statueHint", required: Flags.BossEarth, subtitle: "[Wulang: The statue hums... It answers to the guardian's power now. (Press F)]");
        Save(scene);
    }

    private static void Ice(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Ice", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root,
            ("Find the Ice Guardian at the heart of the island", "", Flags.BossIce),
            ("Claim the Tome of Ice", Flags.BossIce, Flags.Has(ElementType.Ice)),
            ("Cross the mountain pass (the waystone by Abe's camp)", Flags.Has(ElementType.Ice), ""));
        Trigger(root, "Arrival", abe.position, NarrativeTrigger.Mode.OnStart, once: "iceArrived", delay: 0.5f,
            subtitle: "[Wulang: So cold... and so quiet. Even the wind is holding its breath.]");
        Trigger(root, "Abe's ice camp", abe.position, NarrativeTrigger.Mode.Zone, radius: 7f, once: "iceAbeMet", dialogue: Dialogue("Ice - Abe's camp", "",
            (Abe, "[Abe: You made it across. I came ahead to scout. Don't ask how.]"),
            (Abe, "[Abe: The Ice Guardian shrugs off a single element. Ground it with Earth, then call the storm, or the other way round. Earth and Lightning together shake it apart.]"),
            (Wu, "[Wulang: You always seem to know the guardians' weaknesses.]"),
            (Abe, "[Abe: I've had a long time to study them.]")));
        Note(root, abe.position + abe.right * -4f + abe.forward * 2f, "Frozen traveller's journal",
            "[Placeholder lore: a book-bearer's journal. 'The old man paid well for the Ice tome. He never touched it himself. Strange.']");
        Memory(root, "Memory II", arena, ghost, profile, Flags.Has(ElementType.Ice), Journal("Memory II - The Frozen Lesson",
            "[Placeholder memory: Abe pulling Wulang from the frozen lake. He says she owes him her life.]"),
            (Abe, "[Abe: Breathe, Wulang. I pulled you out. Remember that: you owe me everything.]"),
            (Wu, "[Young Wulang: I'll repay you, I promise. Whatever you ask.]"),
            (Wu, "[Wulang: ...But I was never on that lake. Was I?]"));
        Save(scene);
    }

    private static void Volcano(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Volcano", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root,
            ("Enter the caldera through the lava tube and defeat the Fire Guardian", "", Flags.BossFire),
            ("Claim the Tome of Fire", Flags.BossFire, Flags.Has(ElementType.Fire)),
            ("Step into the rift at the back of the caldera", Flags.Has(ElementType.Fire), ""));
        Trigger(root, "Arrival", abe.position, NarrativeTrigger.Mode.OnStart, once: "volcanoArrived", delay: 0.5f,
            subtitle: "[Wulang: The air burns. Ice will have to carry me through this.]");
        Trigger(root, "Abe's volcano camp", abe.position, NarrativeTrigger.Mode.Zone, radius: 7f, once: "volcanoAbeMet", dialogue: Dialogue("Volcano - Abe's camp", "",
            (Abe, "[Abe: The last guardian. Once you hold all four, everything will be as it should be.]"),
            (Wu, "[Wulang: As it should be for whom, Abe?]"),
            (Abe, "[Abe: For us, of course. Go. Ice tames fire.]")));
        Note(root, abe.position + abe.right * 4f, "Scorched decree",
            "[Placeholder lore: a decree sealed with horns: 'When the vessel holds all four, bring her to the Sanctum.']");
        Memory(root, "Memory III", arena + new Vector3(-6f, 0f, 0f), ghost, profile, Flags.Has(ElementType.Fire), Journal("Memory III - The Promise",
            "[Placeholder memory: Wulang swearing an oath to Abe before a gilded throne. The throne is the one in her nightmares.]"),
            (Abe, "[Abe: Swear it, Wulang. When the time comes, you will kneel.]"),
            (Wu, "[Young Wulang: I swear.]"),
            (Wu, "[Wulang: That throne... I've seen it in the sky. These memories... are they even mine?]"));
        Save(scene);
    }

    private static void Arena(Material ghost, VolumeProfile profile)
    {
        var scene = Open("FinalArena", out var root);
        var ending = Find<EndingController>(scene);
        var center = ending.transform.position;
        Objectives(root,
            ("Face [Abe] at the heart of the Sanctum", "", Flags.ChoseSubmission + "|" + EndingController.RefusedOffer),
            ("Defeat Ganfaul: match each element he draws on with its weakness", EndingController.RefusedOffer, Flags.BossFinal));
        var choices = new[]
        {
            new DialogueData.Choice { text = "[Kneel and give him the elements]", setFlag = Flags.ChoseSubmission },
            new DialogueData.Choice { text = "[Refuse. Your past is a lie]", setFlag = EndingController.RefusedOffer },
        };
        var offer = Dialogue("Arena - The Offer", "",
            (Abe, "[Abe: You've done well, Wulang. All four elements, gathered in one vessel. Just as I raised you to.]"),
            (Wu, "[Wulang: The guardians warned me. Every memory you gave me had you at its centre.]"),
            (Abe, "[Abe: Because I am your past. Give me the elements and I'll give you all of it back: a home, a family, a life where you mattered.]"),
            (Wu, "[Wulang: And if I refuse?]"),
            (Gan, "[Ganfaul: Then you lose everything you never had.]"));
        offer.choices = choices;
        EditorUtility.SetDirty(offer);
        var reOffer = Dialogue("Arena - The Offer Again", "",
            (Gan, "[Ganfaul: Still standing? Kneel, and the pain ends. Your past is waiting.]"));
        reOffer.choices = choices;
        EditorUtility.SetDirty(reOffer);
        var submission = Dialogue("Arena - Submission", "",
            (Wu, "[Wulang: ...Take them. Just give me back who I was.]"),
            (Gan, "[Ganfaul: Good girl. You were never anyone. Now you never will be.]"));
        var truth = Memory(root, "True memory", center + new Vector3(0f, 0f, 6f), ghost, profile, null, Journal("The True Memory",
            "[Placeholder memory: no ghosts, no glitches. A village girl who chose to stand between Ganfaul and the guardians. She was someone all along.]"),
            (Wu, "[Wulang: I remember now. Not his version. Mine.]"),
            (Wu, "[Wulang: I wasn't his vessel. I was the one who tried to stop him, and lost my memory doing it.]"),
            (Wu, "[Wulang: Whoever I was, I choose who I am now.]"));
        Trigger(root, "The offer", center, NarrativeTrigger.Mode.Zone, radius: 12f, once: "offerHeard", dialogue: offer);
        var settings = new SerializedObject(ending);
        settings.FindProperty("offer").objectReferenceValue = offer;
        settings.FindProperty("reOffer").objectReferenceValue = reOffer;
        settings.FindProperty("submissionLines").objectReferenceValue = submission;
        settings.FindProperty("trueMemory").objectReferenceValue = truth;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Save(scene);
    }

    // ---------- builders ----------

    private const int Abe = 0, Wu = 1, Gan = 2;

    private static DialogueData Dialogue(string name, string flag, params (int who, string text)[] lines)
    {
        string path = $"{DialogueFolder}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.lines = lines.Select(l => new DialogueData.Line
        {
            speaker = l.who == Abe ? "[Abe]" : l.who == Wu ? "Wulang" : "[Ganfaul]",
            portrait = l.who == Abe ? abeFace : l.who == Wu ? wulangFace : ganfaulFace,
            // Speaker names are shown in the box, so strip the "[Name: ...]" prefix used for readability here.
            text = StripSpeaker(l.text),
        }).ToArray();
        data.choices = null;
        data.flagToSetWhenFinished = flag;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static string StripSpeaker(string line)
    {
        int colon = line.IndexOf(':');
        return line.StartsWith("[") && colon > 0 && colon < 24 ? "[" + line.Substring(colon + 1).TrimStart() : line;
    }

    private static ItemData Journal(string name, string lore)
    {
        string path = $"{JournalFolder}/{name}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.displayName = name;
        item.description = lore;
        item.category = name.StartsWith("Memory") || name.StartsWith("The True") ? ItemCategory.Memory : ItemCategory.Lore;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static Scene Open(string region, out Transform root)
    {
        var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(region), OpenSceneMode.Additive);
        var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Story content");
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("Story content");
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

    private static T Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).First();

    private static void Objectives(Transform root, params (string text, string show, string done)[] list)
    {
        var objectives = root.gameObject.AddComponent<RegionObjectives>();
        objectives.Set(list.Select(o => new RegionObjectives.Objective { text = o.text, showWhen = o.show, doneWhen = o.done }).ToArray());
    }

    private static NarrativeTrigger Trigger(Transform root, string name, Vector3 position, NarrativeTrigger.Mode mode, float radius = 8f,
        string once = "", string required = "", string watch = "", float delay = 0f, string captionTitle = "", string captionBody = "",
        DialogueData dialogue = null, string subtitle = "", MemoryEcho memory = null, ItemData give = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = position;
        var trigger = go.AddComponent<NarrativeTrigger>();
        var s = new SerializedObject(trigger);
        s.FindProperty("mode").enumValueIndex = (int)mode;
        s.FindProperty("radius").floatValue = radius;
        s.FindProperty("onceFlag").stringValue = once;
        s.FindProperty("requiredFlag").stringValue = required;
        s.FindProperty("watchFlag").stringValue = watch;
        s.FindProperty("delay").floatValue = delay;
        s.FindProperty("captionTitle").stringValue = captionTitle;
        s.FindProperty("captionBody").stringValue = captionBody;
        s.FindProperty("dialogue").objectReferenceValue = dialogue;
        s.FindProperty("subtitle").stringValue = subtitle;
        s.FindProperty("memory").objectReferenceValue = memory;
        s.FindProperty("giveItem").objectReferenceValue = give;
        s.ApplyModifiedPropertiesWithoutUndo();
        return trigger;
    }

    // A found document: a tome on the ground; F reads it into the journal (environmental story).
    private static void Note(Transform root, Vector3 position, string name, string lore)
    {
        position = FloorAt(position) + Vector3.up * 0.3f;
        var book = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NoteBook), root);
        book.name = name;
        book.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        if (book.GetComponentInChildren<Collider>() == null) book.AddComponent<BoxCollider>().size = Vector3.one * 0.6f;
        var interactable = book.AddComponent<Interactable>();
        var s = new SerializedObject(interactable);
        s.FindProperty("prompt").stringValue = "Press F to read " + name;
        s.ApplyModifiedPropertiesWithoutUndo();
        var trigger = book.AddComponent<NarrativeTrigger>();
        var t = new SerializedObject(trigger);
        t.FindProperty("mode").enumValueIndex = (int)NarrativeTrigger.Mode.Interact;
        t.FindProperty("onceFlag").stringValue = "read " + name;
        t.FindProperty("subtitle").stringValue = "[Added to journal: " + name + "]";
        t.FindProperty("giveItem").objectReferenceValue = Journal(name, lore);
        t.ApplyModifiedPropertiesWithoutUndo();
    }

    // A memory echo at a spot: ghostly Wulang and Abe acting it out under a desaturating volume.
    // With watchFlag set, it plays when that flag is set (e.g. a tome collected).
    private static MemoryEcho Memory(Transform root, string name, Vector3 spot, Material ghost, VolumeProfile profile, string watchFlag,
        ItemData journal, params (int who, string text)[] lines)
    {
        var echo = new GameObject(name);
        echo.transform.SetParent(root, false);
        echo.transform.position = spot;
        var actors = new GameObject("Ghost actors");
        actors.transform.SetParent(echo.transform, false);
        Ghost(actors.transform, Wulang, spot + new Vector3(-1.2f, 0f, 0f), 90f, ghost, "standing idle");
        Ghost(actors.transform, BossSetup.AbePrefab, spot + new Vector3(1.2f, 0f, 0f), -90f, ghost, "Talk01");
        var volumeObject = new GameObject("Memory volume");
        volumeObject.transform.SetParent(echo.transform, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
        var memory = echo.AddComponent<MemoryEcho>();
        var s = new SerializedObject(memory);
        s.FindProperty("actors").objectReferenceValue = actors;
        s.FindProperty("lines").objectReferenceValue = Dialogue($"{name} lines", "", lines);
        s.FindProperty("volume").objectReferenceValue = volume;
        s.FindProperty("journalEntry").objectReferenceValue = journal;
        s.ApplyModifiedPropertiesWithoutUndo();
        if (!string.IsNullOrEmpty(watchFlag))
            Trigger(root, name + " trigger", spot, NarrativeTrigger.Mode.OnFlag, once: name + " seen", watch: watchFlag, delay: 1.5f, memory: memory);
        return memory;
    }

    private static void Ghost(Transform parent, string source, Vector3 position, float yaw, Material ghost, string clip)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        var model = asset.GetComponentInChildren<Animator>().gameObject;
        var actor = (GameObject)Object.Instantiate(model, parent);
        actor.name = asset.name + " (ghost)";
        position = FloorAt(position);
        actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        actor.transform.localScale = Vector3.one;
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = Enumerable.Repeat(ghost, renderer.sharedMaterials.Length).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        foreach (var component in actor.GetComponentsInChildren<Component>().Where(c => c is MonoBehaviour).ToArray())
            Object.DestroyImmediate(component);
        var animator = actor.GetComponent<Animator>();
        string controllerPath = $"{Folder}/Ghost {asset.name} {clip}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var state = controller.layers[0].stateMachine.AddState(clip);
        state.motion = AssetDatabase.LoadAllAssetsAtPath($"Assets/Characters/Bosses/Animations/{clip}.fbx").OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
    }

    // The walkable surface under a point: the caldera floor and the floating arena are meshes
    // above or away from the terrain, so raycast first and fall back to the terrain height.
    private static Vector3 FloorAt(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return SceneSplitSetup.Ground(new Vector2(point.x, point.z));
    }

    private static Material GhostMaterial()
    {
        const string path = Folder + "/Memory ghost.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Memory ghost" };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        material.SetColor("_BaseColor", new Color(0.75f, 0.9f, 1f, 0.35f));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.35f, 0.55f, 0.9f) * 1.5f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static VolumeProfile MemoryProfile()
    {
        const string path = Folder + "/Memory volume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-80f);
        color.postExposure.Override(-0.3f);
        color.colorFilter.Override(new Color(0.8f, 0.88f, 1f));
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.45f);
        vignette.smoothness.Override(0.6f);
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // A 256px head-and-shoulders render of a character for the dialogue box.
    private static Sprite Portrait(string name, string source)
    {
        string path = $"{PortraitFolder}/{name}.png";
        var instance = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        instance.transform.position = new Vector3(0f, -500f, 0f);
        var animator = instance.GetComponentInChildren<Animator>();
        var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : instance.transform;
        var camera = new GameObject("Portrait camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.08f, 0.06f, 1f);
        var forward = instance.transform.forward;
        float size = Mathf.Max(0.35f, head.lossyScale.y * 0.35f) * instance.transform.localScale.y;
        camera.transform.position = head.position + forward * size * 4.5f + Vector3.up * size * 0.2f;
        camera.transform.LookAt(head.position - Vector3.up * size * 0.3f);
        camera.fieldOfView = 28f;
        var light = new GameObject("Portrait light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.5f + Vector3.right * 0.4f);
        var texture = new RenderTexture(256, 256, 24);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(instance);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The tracker, satchel and pause menu live with the player in the World scene.
    private static void WorldSystems(Scene world)
    {
        var manager = Object.FindFirstObjectByType<RegionManager>().gameObject;
        if (manager.GetComponent<ObjectiveManager>() == null) manager.AddComponent<ObjectiveManager>();
        if (manager.GetComponent<InventoryUI>() == null) manager.AddComponent<InventoryUI>();
        if (manager.GetComponent<PauseMenu>() == null) manager.AddComponent<PauseMenu>();
        EditorSceneManager.MarkSceneDirty(world);
    }
}
