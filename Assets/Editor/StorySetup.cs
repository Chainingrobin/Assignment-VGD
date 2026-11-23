using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Lays the story onto the regions: dialogue assets, speaker portraits
// rendered from the character models, journal entries, the memory-echo look (ghost material
// and desaturation volume), and per region the objectives, narrative triggers, a found lore
// note and a memory echo. Also adds the objective tracker, inventory and pause menu to the
// World. The wording lives in StoryScript. Re-runnable.
public static class StorySetup
{
    private const string Folder = "Assets/Story";
    private const string DialogueFolder = Folder + "/Dialogue";
    private const string JournalFolder = Folder + "/Journal";
    private const string PortraitFolder = Folder + "/Portraits";
    private const string NoteProps = "Assets/Stylized Fantasy Armory/Prefabs/Decorative Props/";
    private const string Wulang = "Assets/Characters/Protagonist/Protagonist.fbx";

    private static Sprite abeFace, wulangFace, ganfaulFace;

    [MenuItem("Wulang/Story/Lay story onto regions")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        foreach (var folder in new[] { Folder, DialogueFolder, JournalFolder, PortraitFolder })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        RenderPortraits();
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
        Debug.Log("StorySetup: story laid onto all regions.");
    }

    // Only the three dialogue portraits. Same file paths, so every dialogue asset keeps its sprite.
    // All three use the idle pose: sampling the talk clip left Abe and Wulang in their bind pose.
    [MenuItem("Wulang/Story/Re-render portraits")]
    public static void RenderPortraits()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        abeFace = Portrait("Abe", BossSetup.AbePrefab, "standing idle", 0.25f);
        wulangFace = Portrait("Wulang", Wulang, "standing idle", 0.25f);
        ganfaulFace = Portrait("Ganfaul", BossSetup.FinalBoss, "standing idle", 0.3f);
        // Re-point every existing line at the new sprites, by speaker.
        foreach (var guid in AssetDatabase.FindAssets("t:DialogueData", new[] { DialogueFolder }))
        {
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var line in data.lines)
                line.portrait = line.speaker.Contains("Abe") ? abeFace : line.speaker.Contains("Ganfaul") ? ganfaulFace
                    : line.speaker.Contains("Wulang") ? wulangFace : line.portrait;
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }

    // ---------- regions ----------
    // All wording comes from StoryScript; this only places it.

    private static void Forest(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Forest", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root, "Forest",
            ("", "metAbe", abe.position),
            ("metAbe", Flags.BossEarth, arena),
            (Flags.BossEarth, Flags.Has(ElementType.Earth), arena),
            (Flags.Has(ElementType.Earth), Flags.LakeFrozen, GameObject.Find("Ice statue").transform.position),
            (Flags.LakeFrozen, "", new Vector3(655f, 0f, 330f))); // the Ice spawn (SceneSplitSetup)
        // The opening is the intro cutscene (IntroCutscene), played by RegionManager on a new game.
        Talk(abe, Dialogue("Forest - Meet Abe", "metAbe"));
        Note(root, abe, "Scroll/Scroll.001", "Torn ledger page");
        Memory(root, "Memory I", arena, ghost, profile, Flags.Has(ElementType.Earth), Journal("Memory I - The Forest Home"));
        Trigger(root, "Statue awakens", GameObject.Find("Ice statue").transform.position, NarrativeTrigger.Mode.Zone, radius: 14f,
            once: "statueHint", required: Flags.BossEarth, subtitle: StoryScript.Subtitles[("Forest", "Statue awakens")]);
        Mood(root, "Forest", new Color(1f, 0.95f, 0.85f), 8f, 6f, 0f);
        Wreckage(root);
        Save(scene);
    }

    private static void Ice(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Ice", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root, "Ice",
            ("", Flags.BossIce, arena),
            (Flags.BossIce, Flags.Has(ElementType.Ice), arena),
            (Flags.Has(ElementType.Ice), "", Find<RegionTravel>(scene).transform.position));
        Trigger(root, "Arrival", abe.position, NarrativeTrigger.Mode.OnStart, once: "iceArrived", delay: 0.5f,
            subtitle: StoryScript.Subtitles[("Ice", "Arrival")]);
        Talk(abe, Dialogue("Ice - Abe's camp", ""));
        Note(root, abe, "Books/Book.003", "Frozen traveller's journal");
        Memory(root, "Memory II", arena, ghost, profile, Flags.Has(ElementType.Ice), Journal("Memory II - The Frozen Lesson"));
        Mood(root, "Ice", new Color(0.84f, 0.92f, 1f), -22f, 10f, 0.15f);
        FrozenTravellers(root);
        Save(scene);
    }

    private static void Volcano(Material ghost, VolumeProfile profile)
    {
        var scene = Open("Volcano", out var root);
        var arena = Find<BossEncounter>(scene).transform.position;
        var abe = Find<Merchant>(scene).transform;
        Objectives(root, "Volcano",
            ("", Flags.BossFire, arena),
            (Flags.BossFire, Flags.Has(ElementType.Fire), arena),
            (Flags.Has(ElementType.Fire), "", Find<RegionTravel>(scene).transform.position));
        Trigger(root, "Arrival", abe.position, NarrativeTrigger.Mode.OnStart, once: "volcanoArrived", delay: 0.5f,
            subtitle: StoryScript.Subtitles[("Volcano", "Arrival")]);
        Talk(abe, Dialogue("Volcano - Abe's camp", ""));
        Note(root, abe, "Scroll/Scroll.002", "Scorched decree");
        Memory(root, "Memory III", arena + new Vector3(-6f, 0f, 0f), ghost, profile, Flags.Has(ElementType.Fire), Journal("Memory III - The Promise"));
        Mood(root, "Volcano", new Color(1f, 0.82f, 0.68f), 6f, 16f, 0.25f);
        // Only at the last camp, by the fire: earlier it gave the twist away.
        HornedShadow(root, abe);
        RiftShrine(root, Find<RegionTravel>(scene).transform.position);
        Save(scene);
    }

    private static void Arena(Material ghost, VolumeProfile profile)
    {
        var scene = Open("FinalArena", out var root);
        var ending = Find<EndingController>(scene);
        var center = ending.transform.position;
        Objectives(root, "FinalArena",
            ("", Flags.ChoseSubmission + "|" + EndingController.RefusedOffer, center),
            (EndingController.RefusedOffer, Flags.BossFinal, Vector3.zero));
        var choices = new[]
        {
            new DialogueData.Choice { text = StoryScript.OfferChoices[0], setFlag = Flags.ChoseSubmission },
            new DialogueData.Choice { text = StoryScript.OfferChoices[1], setFlag = EndingController.RefusedOffer },
        };
        var offer = Dialogue("Arena - The Offer", "");
        offer.choices = choices;
        EditorUtility.SetDirty(offer);
        var reOffer = Dialogue("Arena - The Offer Again", "");
        reOffer.choices = choices;
        EditorUtility.SetDirty(reOffer);
        var submission = Dialogue("Arena - Submission", "");
        var truth = Memory(root, "True memory", center + new Vector3(0f, 0f, 6f), ghost, profile, null, Journal("The True Memory"), withAbe: false);
        Trigger(root, "The offer", center, NarrativeTrigger.Mode.Zone, radius: 12f, once: "offerHeard", dialogue: offer);
        var settings = new SerializedObject(ending);
        settings.FindProperty("offer").objectReferenceValue = offer;
        settings.FindProperty("reOffer").objectReferenceValue = reOffer;
        settings.FindProperty("submissionLines").objectReferenceValue = submission;
        settings.FindProperty("trueMemory").objectReferenceValue = truth;
        settings.FindProperty("submissionTitle").stringValue = StoryScript.SubmissionTitle;
        settings.FindProperty("submissionText").stringValue = StoryScript.SubmissionText;
        settings.FindProperty("truthTitle").stringValue = StoryScript.TruthTitle;
        settings.FindProperty("truthText").stringValue = StoryScript.TruthText;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Mood(root, "FinalArena", new Color(0.88f, 0.82f, 1f), -12f, 12f, 0.3f);
        Save(scene);
    }

    // ---------- wordless story: what each level says without text ----------

    private const string Armory = "Assets/Stylized Fantasy Armory/Prefabs/";
    private const string Statues = "Assets/PBR_Statues_pack/";
    private const string GanfaulModel = "Assets/Characters/Bosses/Ganfaul/Ganfaul.fbx";

    // The chapter's colour grade lives in its region scene, so loading the level visibly changes
    // the world. Only the parameters set here override the World's grade.
    private static void Mood(Transform root, string region, Color filter, float saturation, float contrast, float vignette)
    {
        const string folder = Folder + "/Atmosphere";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Folder, "Atmosphere");
        string path = $"{folder}/{region}.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var color = profile.Add<ColorAdjustments>();
        color.colorFilter.Override(filter);
        color.saturation.Override(saturation);
        color.contrast.Override(contrast);
        if (vignette > 0f)
        {
            var edge = profile.Add<Vignette>();
            edge.intensity.Override(vignette);
            edge.smoothness.Override(0.5f);
        }
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);

        var go = new GameObject("Atmosphere");
        go.transform.SetParent(root, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f; // above the World's grade, below a memory echo (50)
        volume.weight = 0f;    // RegionMood eases it in
        volume.sharedProfile = profile;
        go.AddComponent<RegionMood>();
    }

    // Abe casts Ganfaul's horned shadow instead of his own, at every camp: the lie in plain sight,
    // echoed by the first memory ("his shadow has horns") and paid off in the Sanctum.
    private static void HornedShadow(Transform root, Transform abe)
    {
        foreach (var renderer in abe.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        var shadow = new GameObject("Abe's true shadow");
        shadow.transform.SetParent(root, false);
        Ghost(shadow.transform, GanfaulModel, abe.position, abe.eulerAngles.y, null, "Sitting Idle", snap: false); // seated, like Abe
    }

    // The Earth Guardian's rampage: armory racks, a cart and barrels strewn beside the trail to the
    // Blossom Glade. It shows what came this way and leads the player there without a marker.
    private static void Wreckage(Transform root)
    {
        var trail = ForestBiomeBuilder.TrailToArena;
        var parent = Group(root, "Rampage wreckage");
        Prop(parent, Armory + "Decorative Props/Weapon Racks/WeaponRack.002.prefab", OnTerrain(Beside(trail, 0, 3.5f)), 30f, 85f);
        Prop(parent, Armory + "Wagon/Wagon.003.prefab", OnTerrain(Beside(trail, 1, -5f)), 120f, 14f);
        Prop(parent, Armory + "Decorative Props/Barrel/Barrel.001.prefab", OnTerrain(Beside(trail, 1, 3f)), 0f);
        Prop(parent, Armory + "Decorative Props/Box/Box.002.prefab", OnTerrain(Beside(trail, 2, -3.5f)), 47f, 25f);
        Prop(parent, Armory + "Decorative Props/Barrel/Barrel.002.prefab", OnTerrain(Beside(trail, 2, 3.2f)), 0f);
        Prop(parent, Armory + "Decorative Props/Weapon Racks/WeaponRack.004.prefab", OnTerrain(Beside(trail, 3, 4f)), 210f, 80f);
    }

    // Travellers frozen solid on the approach to the Frozen Amphitheater, all facing away from it:
    // whatever waits inside caught them as they ran.
    private static void FrozenTravellers(Transform root)
    {
        var ice = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Frozen shell.mat");
        var trail = SnowBiomeBuilder.TrailToArena;
        var arena = SnowBiomeBuilder.ArenaCenter;
        var parent = Group(root, "Frozen travellers");
        var spots = new[] { Beside(trail, 0, 3f), Beside(trail, 1, -3.5f), Beside(trail, 1, 4f) + new Vector2(0f, -6f), Beside(trail, 2, -3f) };
        for (int i = 0; i < spots.Length; i++)
        {
            var away = spots[i] - arena;
            float yaw = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg + (i % 2 == 0 ? 12f : -15f);
            string statue = i % 2 == 0 ? "Statue_Woman/Prefab/Statue_Women_Stone.prefab" : "Statue_Woman/Prefab/Statue_Women_Granite.prefab";
            Prop(parent, Statues + statue, OnTerrain(spots[i]), yaw, 0f, ice);
        }
    }

    // At the rift: Ganfaul's toppled idol, the chains of those dragged through before her, and a
    // red glow from beyond, so the way to the Sanctum reads as his before any word is said.
    private static void RiftShrine(Transform root, Vector3 rift)
    {
        var parent = Group(root, "Rift shrine");
        Prop(parent, Statues + "Statue_God/Prefab/Statue_God_stone1.prefab", rift + new Vector3(3.5f, 0f, 2.5f), 200f, 80f);
        Prop(parent, Armory + "Armory Structure and Attachables/Armory Attachables/Chain.001.prefab", rift + new Vector3(-2.5f, 0f, 1.5f), 30f, 90f);
        Prop(parent, Armory + "Armory Structure and Attachables/Armory Attachables/Chain.002.prefab", rift + new Vector3(-3f, 0f, -1.2f), 110f, 90f);
        var glow = new GameObject("Rift glow").AddComponent<Light>();
        glow.transform.SetParent(parent, false);
        glow.transform.position = rift + Vector3.up * 2f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.25f, 0.1f);
        glow.range = 14f;
        glow.intensity = 4f;
    }

    private static Transform Group(Transform root, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(root, false);
        return group;
    }

    // A point `offset` metres to the side of a path's i-th point (positive = left of travel).
    private static Vector2 Beside(Vector2[] path, int i, float offset)
    {
        var along = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
        return path[i] + new Vector2(-along.y, along.x) * offset;
    }

    private static Vector3 OnTerrain(Vector2 xz) => SceneSplitSetup.Ground(xz);

    // A prop set on the floor near `near`, turned by yaw and tipped over sideways by tip degrees,
    // optionally re-skinned (e.g. frozen).
    private static void Prop(Transform parent, string path, Vector3 near, float yaw, float tip = 0f, Material skin = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException(path);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        prop.transform.SetPositionAndRotation(FloorAt(near), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, tip));
        // The pack's wagons carry WheelColliders, which need a rigidbody and only spam warnings on a prop.
        foreach (var wheel in prop.GetComponentsInChildren<WheelCollider>()) Object.DestroyImmediate(wheel);
        if (skin == null) return;
        foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = Enumerable.Repeat(skin, renderer.sharedMaterials.Length).ToArray();
    }

    // ---------- builders ----------

    private static DialogueData Dialogue(string name, string flag)
    {
        var lines = StoryScript.Conversations[name];
        string path = $"{DialogueFolder}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.lines = lines.Select(l => new DialogueData.Line
        {
            speaker = StoryScript.Speaker(l.who),
            portrait = l.who == StoryScript.Abe ? abeFace : l.who == StoryScript.Gan ? ganfaulFace : wulangFace,
            text = l.text,
        }).ToArray();
        data.choices = null;
        data.flagToSetWhenFinished = flag;
        EditorUtility.SetDirty(data);
        return data;
    }

    // A journal entry, its wording from StoryScript.Lore (LandmarksBuilder's chest pages included).
    internal static ItemData Journal(string name)
    {
        string path = $"{JournalFolder}/{name}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.displayName = StoryScript.Title(name);
        item.description = StoryScript.Lore[name];
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

    private static void Objectives(Transform root, string region, params (string show, string done, Vector3 marker)[] list)
    {
        var objectives = root.gameObject.AddComponent<RegionObjectives>();
        objectives.Set(list.Select((o, i) => new RegionObjectives.Objective
            { text = StoryScript.Objectives[region][i], showWhen = o.show, doneWhen = o.done, marker = o.marker }).ToArray());
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
    // Abe's camp conversation: played the first time the player presses F on him (then he trades),
    // instead of a trigger zone stopping the player as they walk past.
    private static void Talk(Transform abe, DialogueData conversation)
    {
        var merchant = new SerializedObject(abe.GetComponent<Merchant>());
        merchant.FindProperty("conversation").objectReferenceValue = conversation;
        merchant.FindProperty("portrait").objectReferenceValue = abeFace;
        merchant.ApplyModifiedPropertiesWithoutUndo();
    }

    // A found document lying on the camp table beside Abe (an armory-pack scroll or book): F reads it
    // into the journal. Without a camp table it lies on the ground a few metres from him.
    private static void Note(Transform root, Transform abe, string model, string name)
    {
        var table = abe.parent != null ? abe.parent.Find("Camp table") : null;
        var position = FloorAt(table != null ? table.position + table.right * 0.45f : abe.position + abe.right * 4f + abe.forward * 2f);
        var book = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NoteProps + model + ".prefab"), root);
        book.name = name;
        book.transform.SetPositionAndRotation(position + Vector3.up * 0.01f, Quaternion.Euler(0f, abe.eulerAngles.y + 20f, 0f));
        // A generous box so the look-at ray finds it easily on the table.
        var aim = book.AddComponent<BoxCollider>();
        aim.center = Vector3.up * 0.1f;
        aim.size = new Vector3(0.5f, 0.25f, 0.5f);
        var interactable = book.AddComponent<Interactable>();
        var s = new SerializedObject(interactable);
        s.FindProperty("prompt").stringValue = "Press F to read the " + StoryScript.Title(name).ToLowerInvariant();
        s.ApplyModifiedPropertiesWithoutUndo();
        var trigger = book.AddComponent<NarrativeTrigger>();
        var t = new SerializedObject(trigger);
        t.FindProperty("mode").enumValueIndex = (int)NarrativeTrigger.Mode.Interact;
        t.FindProperty("onceFlag").stringValue = "read " + name;
        t.FindProperty("subtitle").stringValue = "Added to your journal: " + StoryScript.Title(name);
        t.FindProperty("giveItem").objectReferenceValue = Journal(name);
        t.ApplyModifiedPropertiesWithoutUndo();
    }

    // A memory echo at a spot: ghostly Wulang and Abe acting it out under a desaturating volume.
    // With watchFlag set, it plays when that flag is set (e.g. a tome collected).
    // The true memory has no Abe in it.
    private static MemoryEcho Memory(Transform root, string name, Vector3 spot, Material ghost, VolumeProfile profile, string watchFlag,
        ItemData journal, bool withAbe = true)
    {
        var echo = new GameObject(name);
        echo.transform.SetParent(root, false);
        echo.transform.position = spot;
        var actors = new GameObject("Ghost actors");
        actors.transform.SetParent(echo.transform, false);
        Ghost(actors.transform, Wulang, spot + new Vector3(-1.2f, 0f, 0f), 90f, ghost, "standing idle");
        if (withAbe) Ghost(actors.transform, BossSetup.AbePrefab, spot + new Vector3(1.2f, 0f, 0f), -90f, ghost, "Talk01");
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
        s.FindProperty("lines").objectReferenceValue = Dialogue($"{name} lines", "");
        s.FindProperty("title").stringValue = withAbe ? StoryScript.MemoryTitle : StoryScript.TrueMemoryTitle;
        s.FindProperty("volume").objectReferenceValue = volume;
        s.FindProperty("journalEntry").objectReferenceValue = journal;
        s.ApplyModifiedPropertiesWithoutUndo();
        if (!string.IsNullOrEmpty(watchFlag))
            Trigger(root, name + " trigger", spot, NarrativeTrigger.Mode.OnFlag, once: name + " seen", watch: watchFlag, delay: 1.5f, memory: memory);
        return memory;
    }

    // A posed character copy: a see-through memory ghost, or with no ghost material an invisible
    // body that only casts its shadow.
    private static void Ghost(Transform parent, string source, Vector3 position, float yaw, Material ghost, string clip, bool snap = true)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        var model = asset.GetComponentInChildren<Animator>().gameObject;
        var actor = (GameObject)Object.Instantiate(model, parent);
        actor.name = asset.name + (ghost != null ? " (ghost)" : " (shadow)");
        if (snap) position = FloorAt(position);
        actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        actor.transform.localScale = Vector3.one;
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
        {
            if (ghost != null) renderer.sharedMaterials = Enumerable.Repeat(ghost, renderer.sharedMaterials.Length).ToArray();
            renderer.shadowCastingMode = ghost != null ? ShadowCastingMode.Off : ShadowCastingMode.ShadowsOnly;
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
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // a shadow-only body is never "visible"
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

    // A 1024px bust portrait for the dialogue box: the character is posed with a frame of its idle
    // animation (not the T-pose), centred on the head from the top of the head to the upper chest,
    // lit with a warm key and a cool rim, and rendered on a transparent background.
    private static Sprite Portrait(string name, string source, string pose, float at)
    {
        const int size = 1024;
        string path = $"{PortraitFolder}/{name}.png";
        var instance = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        instance.transform.SetPositionAndRotation(new Vector3(0f, 3000f, 0f), Quaternion.identity);
        foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        var animator = instance.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // otherwise the off-screen pose never reaches the bones
        var idle = AssetDatabase.LoadAllAssetsAtPath($"Assets/Characters/Bosses/Animations/{pose}.fbx").OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));
        var graph = UnityEngine.Playables.PlayableGraph.Create("Portrait pose");
        var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
        var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, idle);
        playable.SetApplyFootIK(false);
        output.SetSourcePlayable(playable);
        playable.SetTime(idle.length * at);
        graph.Evaluate();

        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;
        var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        float shoulders = Vector3.Distance(left.position, right.position);
        // Bust crop: from the top of the posed mesh (hair, horns) down to the upper chest, centred on the
        // head so every portrait sits the same way in the round socket, whatever the rig's build.
        float chest = neck.position.y - shoulders * 0.5f;
        float crown = float.MinValue;
        var baked = new Mesh();
        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.gameObject.activeInHierarchy) continue;
            skin.BakeMesh(baked, true);
            foreach (var vertex in baked.vertices) crown = Mathf.Max(crown, skin.transform.TransformPoint(vertex).y);
        }
        Object.DestroyImmediate(baked);
        crown += (crown - chest) * 0.06f; // a little headroom
        var focus = new Vector3(head.position.x, (crown + chest) * 0.5f, head.position.z);
        var forward = instance.transform.forward;

        var camera = new GameObject("Portrait camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.fieldOfView = 24f;
        float distance = (crown - chest) * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        // Face the head (not the hips), slightly off-axis for a three-quarter portrait.
        var faceForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).sqrMagnitude > 0.1f && Vector3.Dot(head.forward, forward) > 0.3f
            ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : forward;
        camera.transform.position = focus + Quaternion.Euler(0f, 12f, 0f) * faceForward * distance + Vector3.up * shoulders * 0.1f;
        camera.transform.LookAt(focus);
        var key = new GameObject("Key light").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.9f, 0.78f);
        key.intensity = 1.3f;
        key.transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.4f + Vector3.right * 0.5f);
        var rim = new GameObject("Rim light").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(0.55f, 0.7f, 1f);
        rim.intensity = 1.1f;
        rim.transform.rotation = Quaternion.LookRotation(forward + Vector3.down * 0.2f - Vector3.right * 0.6f);

        var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        graph.Destroy();
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(rim.gameObject);
        Object.DestroyImmediate(instance);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        // One sprite covering the whole render (an old 256px slice showed only the corner of each portrait).
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
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
