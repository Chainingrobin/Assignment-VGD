using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor bridge between the story data and the scenes. It holds no story text: every line is data
/// that can be edited in the Inspector without touching code (assignment guide, Step 2).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Conversations: one <see cref="DialogueData"/> asset each in <c>Assets/Story/Dialogue</c>.</description></item>
/// <item><description>Journal pages, memories and item lore: <see cref="ItemData"/> assets in <c>Assets/Story/Journal</c>
/// and <c>Assets/Items</c>.</description></item>
/// <item><description>Guardian lines, chapter cards and recaps, objectives, barks and ending cards: the
/// <see cref="StoryText"/> asset at <see cref="TextPath"/>.</description></item>
/// </list>
/// The game reads conversations and journal pages straight from their assets. The Story Text lines are copied onto
/// the scene objects that show them (boss encounters, the region manager, objectives, triggers, endings), so after
/// editing that asset run <b>Wulang &gt; Story &gt; Apply story text</b>. The scene builders (StorySetup,
/// RegionContentSetup, FinalArenaBuilder, SceneSplitSetup, LandmarksBuilder, ItemsSetup) read their wording here too.
///
/// The twist (Abe is Ganfaul) is foreshadowed, never stated: Abe is always at the next camp first, never touches a
/// tome, prices everything, and every memory he "returns" has a slip in it (the soup, the warm water, the song) that
/// the true memory pays off.
/// </remarks>
public static class StoryScript
{
    public const string TextPath = "Assets/Story/Story Text.asset";
    private static readonly string[] ItemFolders = { "Assets/Story/Journal", "Assets/Items" };

    /// <summary>The Story Text asset. A missing asset is an error, not a scene full of blank lines.</summary>
    public static StoryText Text => AssetDatabase.LoadAssetAtPath<StoryText>(TextPath)
        ?? throw new System.InvalidOperationException($"StoryScript: {TextPath} is missing.");

    // ---------- journal pages and item lore (ItemData assets, by asset name) ----------

    private static IEnumerable<ItemData> Items() => AssetDatabase.FindAssets("t:ItemData", ItemFolders)
        .Select(guid => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)));

    /// <summary>Each journal page's and item's lore text, by asset name.</summary>
    public static Dictionary<string, string> Lore => Items().ToDictionary(item => item.name, item => item.description);

    /// <summary>The title shown in the journal for an asset, or the asset name if it has none.</summary>
    public static string Title(string asset)
    {
        var item = Items().FirstOrDefault(i => i.name == asset);
        return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : asset;
    }

    // ---------- everything else (the Story Text asset) ----------

    /// <summary>Guardians by defeat flag: health-bar name, line on engaging, last words.</summary>
    public static Dictionary<string, (string name, string intro, string dying)> Bosses =>
        Text.guardians.ToDictionary(g => g.defeatFlag, g => (name: g.displayName, intro: g.introLine, dying: g.dyingLine));

    /// <summary>Chapters by region scene: numeral, title, recap on arrival, line at a closed seal.</summary>
    public static Dictionary<string, (string chapter, string title, string recap, string locked)> Regions =>
        Text.chapters.ToDictionary(c => c.scene, c => (chapter: c.number, title: c.title, recap: c.recap, locked: c.lockedLine));

    public static string ChapterTitle(string scene) => $"Chapter {Regions[scene].chapter}  -  {Regions[scene].title}";

    /// <summary>Objectives by region scene, in story order.</summary>
    public static Dictionary<string, string[]> Objectives => Text.chapters.ToDictionary(c => c.scene, c => c.objectives);

    /// <summary>NarrativeTrigger subtitles, by region scene and trigger name.</summary>
    public static Dictionary<(string scene, string trigger), string> Subtitles =>
        Text.barks.ToDictionary(b => (scene: b.scene, trigger: b.trigger), b => b.text);

    /// <summary>Abe's first line when his shop opens, by region scene.</summary>
    public static Dictionary<string, string> Greetings =>
        Text.chapters.Where(c => !string.IsNullOrEmpty(c.abeGreeting)).ToDictionary(c => c.scene, c => c.abeGreeting);

    public static string LakeLine => Text.lakeLine;
    public static string LavaLine => Text.lavaLine;
    public static string MemoryTitle => Text.memoryTitle;
    public static string TrueMemoryTitle => Text.trueMemoryTitle;
    public static string SubmissionTitle => Text.submissionTitle;
    public static string SubmissionText => Text.submissionText;
    public static string TruthTitle => Text.truthTitle;
    public static string TruthText => Text.truthText;

    // ---------- applying the Story Text asset to the scenes ----------

    /// <summary>Copies every Story Text line onto the scene objects that show it, in the World scene and all four
    /// region scenes. Layout, flags and wiring are left as they are; only wording changes.</summary>
    [MenuItem("Wulang/Story/Apply story text")]
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        ApplyWorld();
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        foreach (var region in Regions.Keys)
        {
            var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(region), OpenSceneMode.Additive);
            ApplyRegion(scene, region);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }
        return "StoryScript: Story Text applied to all five scenes (conversations and journal pages are read from their own assets).";
    }

    // The World holds the chapter cards and the lake and lava hazards.
    private static void ApplyWorld()
    {
        var manager = Object.FindFirstObjectByType<RegionManager>();
        var settings = new SerializedObject(manager);
        var regions = settings.FindProperty("regions");
        for (int i = 0; i < regions.arraySize; i++)
        {
            var region = regions.GetArrayElementAtIndex(i);
            string scene = region.FindPropertyRelative("scene").stringValue;
            if (!Regions.TryGetValue(scene, out var text)) continue;
            region.FindPropertyRelative("title").stringValue = ChapterTitle(scene);
            region.FindPropertyRelative("recap").stringValue = text.recap;
            region.FindPropertyRelative("lockedLine").stringValue = text.locked;
        }
        settings.FindProperty("introNarration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Story/Dialogue/Intro.asset");
        settings.ApplyModifiedPropertiesWithoutUndo();
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        if (player.GetComponent<Footsteps>() == null) player.gameObject.AddComponent<Footsteps>();
        foreach (var hazard in Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Set(hazard, "firstTimeLine", (ElementType)new SerializedObject(hazard).FindProperty("element").enumValueIndex == ElementType.Fire ? LavaLine : LakeLine);
        foreach (var inventory in Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Set(inventory, "noPotionsLine", Text.noPotionsLine);
            Set(inventory, "fullHealthLine", Text.fullHealthLine);
        }
    }

    private static void ApplyRegion(Scene scene, string region)
    {
        var all = scene.GetRootGameObjects();
        T[] Get<T>() where T : Component => all.SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();

        // Objectives keep their flags and compass markers; only the wording changes.
        foreach (var list in Get<RegionObjectives>())
            for (int i = 0; i < list.Objectives.Length && i < Objectives[region].Length; i++) list.Objectives[i].text = Objectives[region][i];
        foreach (var list in Get<RegionObjectives>()) EditorUtility.SetDirty(list);

        foreach (var encounter in Get<BossEncounter>())
        {
            var flag = new SerializedObject(encounter).FindProperty("defeatFlag").stringValue;
            if (!Bosses.TryGetValue(flag, out var boss)) continue;
            Set(encounter, "displayName", boss.name);
            Set(encounter, "introLine", boss.intro);
            Set(encounter, "dyingLine", boss.dying);
        }
        foreach (var merchant in Get<Merchant>())
        {
            Set(merchant, "npcName", "Abe");
            if (Greetings.TryGetValue(region, out var greeting)) Set(merchant, "greeting", greeting);
        }
        foreach (var trigger in Get<NarrativeTrigger>())
        {
            if (Subtitles.TryGetValue((region, trigger.name), out var line)) Set(trigger, "subtitle", line);
            // Found notes: "Added to your journal" instead of the placeholder bracket.
            var give = new SerializedObject(trigger).FindProperty("giveItem").objectReferenceValue as ItemData;
            if (give != null && trigger.GetComponent<Interactable>() != null)
            {
                Set(trigger, "subtitle", $"Added to your journal: {Title(give.name)}");
                // Pages are titled by the memory they stir, not by what they are, so the prompt stays plain.
                Set(trigger.GetComponent<Interactable>(), "prompt", "Press F to look closer");
            }
        }
        // The rift into the Sanctum: its prompt, and the warning that there is no coming back.
        foreach (var travel in Get<RegionTravel>())
        {
            if (new SerializedObject(travel).FindProperty("region").stringValue != "FinalArena") continue;
            Set(travel, "warning", Text.riftWarning);
            Set(travel.GetComponent<Interactable>(), "prompt", Text.riftPrompt);
        }

        // The prologue caption is now the intro cutscene.
        foreach (var trigger in Get<NarrativeTrigger>().Where(t => t.name == "Prologue")) Object.DestroyImmediate(trigger.gameObject);
        foreach (var echo in Get<MemoryEcho>())
        {
            bool truth = echo.name == "True memory";
            Set(echo, "title", truth ? TrueMemoryTitle : MemoryTitle);
            // Her true memory has no Abe in it.
            if (truth) foreach (var actor in echo.GetComponentsInChildren<Animator>(true)) if (actor.name.Contains("Abe")) Object.DestroyImmediate(actor.gameObject);
        }
        foreach (var phases in Get<ElementPhases>()) Set(phases, "phaseLine", Text.ganfaulPhaseLine);
        foreach (var ending in Get<EndingController>())
        {
            Set(ending, "submissionTitle", SubmissionTitle);
            Set(ending, "submissionText", SubmissionText);
            Set(ending, "truthTitle", TruthTitle);
            Set(ending, "truthText", TruthText);
        }
        // Abe's horned shadow only at the last camp, where the twist is close: earlier it gave him away.
        if (region != "Volcano")
            foreach (var shadow in all.SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Abe's true shadow").ToArray())
            {
                foreach (var merchant in Get<Merchant>())
                    foreach (var renderer in merchant.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
                Object.DestroyImmediate(shadow.gameObject);
            }
        // The wreckage wagon's WheelColliders need a rigidbody and only printed warnings.
        foreach (var wheel in Get<WheelCollider>()) Object.DestroyImmediate(wheel);
    }

    private static void Set(Object target, string property, string value)
    {
        var s = new SerializedObject(target);
        s.FindProperty(property).stringValue = value;
        s.ApplyModifiedPropertiesWithoutUndo();
    }
}
