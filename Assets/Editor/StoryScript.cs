using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// The whole story script in one place: every conversation, journal page, boss line, chapter
// recap and objective. The builders (StorySetup, RegionContentSetup, FinalArenaBuilder,
// SceneSplitSetup, LandmarksBuilder, ItemsSetup) read their text from here, and
// "Wulang > Story > Apply story script" writes it into the existing assets and scenes without
// rebuilding anything, so the wording can change without touching level layout.
//
// The twist (Abe is Ganfaul) is foreshadowed, never stated: Abe is always at the next camp first,
// never touches a tome, prices everything, and every memory he "returns" has a slip in it (the
// soup, the warm water, the song) that the true memory pays off.
public static class StoryScript
{
    public const int Abe = 0, Wu = 1, Gan = 2, Young = 3, Narrator = 4;

    public static string Speaker(int who) => who switch { Abe => "Abe", Gan => "Ganfaul", Young => "Young Wulang", Narrator => "", _ => "Wulang" };

    // ---------- conversations (one DialogueData asset each, by asset name) ----------

    public static readonly Dictionary<string, (int who, string text)[]> Conversations = new()
    {
        // The intro cutscene (IntroCutscene): narration over establishing shots, then Abe waking her.
        ["Intro"] = new[]
        {
            (Narrator, "Before the sickness, the Vale had four guardians, and not one of them ever asked for anything."),
            (Narrator, "Earth kept the roots and the harvest. Frost kept the long rest of winter."),
            (Narrator, "Flame kept every hearth from the hills to the sea. And the Storm brought the rain."),
            (Narrator, "Then someone began to count what they were worth. One by one, the guardians went mad."),
            (Narrator, "The Storm alone slipped free. It fled across the sky, looking for somewhere to hide..."),
            (Narrator, "...and it found a girl walking home in the rain."),
            (Abe, "Easy. Easy, spark. Open your eyes."),
        },
        ["Forest - Meet Abe"] = new[]
        {
            (Abe, "There she is. Sit, sit. You took a storm to the head, spark. Most people don't get up from that."),
            (Wu, "You know me?"),
            (Abe, "Abe. I keep this old armory, and I've kept an eye on you since you were knee-high. ...You don't remember. That's all right. It'll come back."),
            (Wu, "There's lightning in my hands. Is that normal?"),
            (Abe, "Not in the least. The Storm chose you. The elements are proud: they only go where they're given. You can't steal them, and you can't buy them."),
            (Abe, "That's why the other guardians went sour. The Earth guardian has been tearing up the Blossom Glade for a month. Put it to rest, and its gift goes to whoever freed it."),
            (Wu, "And you're sending me, because...?"),
            (Abe, "Because I'm old, and magic and I had a falling out years ago. Earth hates the storm. Hit it with what you've got."),
            (Abe, "Bring me Wubucks off those vampires and I'll keep you in potions. Nothing's free, spark. Not even me."),
        },
        ["Ice - Abe's camp"] = new[]
        {
            (Wu, "You're here already. And your boots are dry."),
            (Abe, "Old men know the long way round. Tea? It's terrible, but it's hot."),
            (Wu, "Something came back to me at the glade. A memory. You were in it."),
            (Abe, "Was I? Good. They'll keep coming, one for every gift you carry. Don't rush them."),
            (Abe, "The Ice guardian laughs at one element at a time. Pin it with Earth, then bring the storm down on it. Together they'll shake it apart."),
            (Wu, "How do you know all their weaknesses?"),
            (Abe, "I've had a long life and very little else to do with it."),
        },
        ["Volcano - Abe's camp"] = new[]
        {
            (Abe, "Last one, spark. After this I'll tell you everything you were. I promise you'll like it."),
            (Wu, "You could tell me now."),
            (Abe, "And spoil it? A past is better earned."),
            (Wu, "In the memories, I always say yes to you. Was I ever any good at saying no?"),
            (Abe, "Ha. Never once. Go on. Ice tames fire, and I'll be close."),
        },
        // The memories Abe "returns". Each one has a slip in it.
        ["Memory I lines"] = new[]
        {
            (Abe, "Hands flat on the soil, spark. Now listen. What does it say?"),
            (Young, "It says... the soup's boiling over."),
            (Abe, "Ha! No. Listen again. It says the land needs someone to carry it."),
            (Young, "...It says the land needs someone to carry it."),
            (Wu, "I don't remember any soup. Why would I remember the soup?"),
        },
        ["Memory II lines"] = new[]
        {
            (Abe, "Breathe, spark. I've got you. I've always got you."),
            (Young, "I fell through. I'm sorry. I'm sorry."),
            (Abe, "Don't be sorry. Be grateful. One day I'll ask you for something, and you'll remember this."),
            (Wu, "The water was warm. Ice water isn't warm."),
        },
        ["Memory III lines"] = new[]
        {
            (Abe, "Say it with me, spark. What is given freely cannot be taken."),
            (Young, "What is given freely cannot be taken."),
            (Abe, "And when the time comes, you'll give them to the one who raised you."),
            (Young, "Rain on the roof, rain on the hill..."),
            (Abe, "What's that tune? Never mind it. Say it again."),
            (Wu, "That song. He didn't teach me that song. Someone else did."),
        },
        ["True memory lines"] = new[]
        {
            (Young, "Rain on the roof, rain on the hill, the kettle's warm and the world is still..."),
            (Wu, "Nan's song. She sang it whenever it rained. I was walking home from the market with her bread, singing it badly."),
            (Wu, "That's all it was. No destiny. No debt. Just a girl on the hill road, and a storm that needed somewhere to hide."),
            (Wu, "It was a small life. It was mine. And it isn't for sale."),
        },
        // The Sanctum. Abe's face holds until she refuses.
        ["Arena - The Offer"] = new[]
        {
            (Abe, "There you are. Look at you: earth, frost, flame and storm, all in one pair of hands. I knew you could carry them."),
            (Wu, "You said you'd tell me who I was."),
            (Abe, "I'll do better. I'll give it back. A name people bow to. A life that mattered. Every memory so far was a down payment."),
            (Wu, "A down payment on what?"),
            (Abe, "The elements can't be taken, spark. Only given. So give them to me, freely, and I'll write you a past worth having."),
            (Wu, "And if I say no?"),
            (Abe, "Then I stop pretending to be Abe."),
        },
        ["Arena - The Offer Again"] = new[]
        {
            (Gan, "Still standing? Kneel, spark, and the hurting stops. Your past is right here, waiting."),
        },
        ["Arena - Submission"] = new[]
        {
            (Wu, "...Take them. Just tell me I was someone."),
            (Gan, "You were whoever I needed. Hush now. Here is your past."),
        },
    };

    public static readonly string[] OfferChoices = { "Kneel and give him the elements", "Refuse. My past isn't for sale." };

    // ---------- journal pages, notes and items (ItemData, by asset name) ----------

    // Display names that differ from the asset's file name.
    public static readonly Dictionary<string, string> Titles = new()
    {
        ["Memory I - The Forest Home"] = "Memory I: The Apprentice",
        ["Memory II - The Frozen Lesson"] = "Memory II: Through the Ice",
        ["Memory III - The Promise"] = "Memory III: The Oath",
        ["Scorched decree"] = "Scorched rhyme sheet",
        ["Healing Potion"] = "Healing Potion",
    };

    public static string Title(string asset) => Titles.TryGetValue(asset, out var title) ? title : asset;

    public static readonly Dictionary<string, string> Lore = new()
    {
        // Found beside Abe at each camp.
        ["Torn ledger page"] = "A page from the armory's ledger, in a neat and patient hand. Iron, forty bars. Lamp oil, six casks. Rain on the hill road: not for sale. Someone has struck out 'not for sale' and written beside it, very small: 'yet.'",
        ["Frozen traveller's journal"] = "A book-bearer's journal, frozen shut at the last page. 'Our patron met us at the armory and paid in old silver. He would not touch the tome, not even to count the pages. Said he and books had an understanding. Cold tonight. The tome is heavier than it looks.'",
        ["Scorched decree"] = "A children's rhyme, half burnt. 'The Tallyman, the Tallyman, he weighs the rain, he counts the sand. He'll buy your coat, he'll buy your name, he'll wear a friend's face when he came. But gifts are gifts and can't be bought, so give him nothing. Give him naught.'",
        // The memories.
        ["Memory I - The Forest Home"] = "A summer evening at the armory. Abe teaches a young Wulang to hear the ground speak. She remembers it exactly the way he tells it, down to the words.",
        ["Memory II - The Frozen Lesson"] = "Winter. Young Wulang falls through the ice, and Abe pulls her out. He tells her that one day he will ask for something in return. In the memory, the water is warm.",
        ["Memory III - The Promise"] = "An oath at the armory forge: what is given freely cannot be taken. Halfway through it, young Wulang hums a song that Abe does not know.",
        ["The True Memory"] = "No armory and no Abe. A wet evening on the hill road above Lowmere, bread under her coat, her grandmother's rain song in her mouth. The Storm came down because it needed somewhere to hide, and she was the only one out walking. It was a small life. It was hers.",
        // Chest pages, Forest.
        ["Runestone inscription"] = "Carved in the old script: FOUR GUARDIANS, FOUR GIFTS, GIVEN FREELY TO THE VALE. LET NO ONE HAND HOLD THEM ALL.",
        ["Book-bearer's diary"] = "Third trip this month. The patron pays in old silver for every tome we carry into a guardian's glade, and he never asks for one back. Marta says the tomes are ledgers: he writes down what the land is worth, and the land hates being counted. Marta talks too much. Tomorrow the Earth tome goes north.",
        ["Shrine plaque"] = "Raised by the people of Lowmere for those the Storm took on the hill road: the miller's boy, the Fenn sisters, and the baker's granddaughter, who went out for bread and was never found.",
        // Chest pages, Frozen Reach.
        ["Caravan manifest"] = "Cargo: one tome, sealed, for the Amphitheater. Paid in full, in old silver. Do not open it. Do not read it. Do not stop for anything in the snow that calls you by name.",
        ["Ice-carved verse"] = "Cut deep into the ice: THE COLD KEEPS WHAT THE WARM FORGET. Beneath it, in a child's scratches: 'Nan says winter is just the world remembering to rest.'",
        ["Gate inscription"] = "Here the Frost guardian slept through the long winters, and the travellers' road ran safe beneath it. Then the book-bearers came, carrying gifts.",
        // Chest pages, Burning Peak.
        ["Forge ledger"] = "Order, paid in old silver: forty chains, black iron, extra long. Purpose, in the buyer's words: 'in case of a refusal.' The smith has added underneath: 'Refusal of WHAT? He only smiled.'",
        ["Scorched prayer"] = "Scratched into the bronze: Forgive us, guardian of the flame. We were cold, and he was kind, and he paid us well for the road to your heart.",
        ["Watchman's last note"] = "The guardian came down the mountain screaming, all fire. Behind it, in no hurry at all, came an old man carrying a kettle.",
        // Items.
        ["Healing Potion"] = "Abe's own brew, bitter as bark. He swears by it and charges accordingly. Restores 40 vitality.",
        ["Lightning Tome"] = "The Storm's gift. It came to her without any book, on a wet road, and it has never once felt like it belonged to anyone else.",
        ["Earth Tome"] = "Pried from the Earth guardian's grip. The ink inside has faded to nothing, and the pages smell of rain and turned soil.",
        ["Ice Tome"] = "Cold enough to burn the fingers. Its last pages are columns of figures, every line struck through as if a debt had been paid.",
        ["Fire Tome"] = "Still warm. Someone has written a price on the flyleaf and underlined it twice.",
    };

    // ---------- bosses (by defeat flag): bar name, line on engaging, last words ----------

    public static readonly Dictionary<string, (string name, string intro, string dying)> Bosses = new()
    {
        [Flags.BossEarth] = ("The Earth Guardian",
            "Earth Guardian: Another bearer? Another book? No more. NO MORE.",
            "Earth Guardian: ...Lighter. The ink is gone. Carry it gently, little storm, and give it back when you are done."),
        [Flags.BossIce] = ("The Ice Guardian",
            "Ice Guardian: Warm thing. Go home, before the cold remembers you.",
            "Ice Guardian: The cold keeps... what warm hands forget. Ask who paid for the books, child."),
        [Flags.BossFire] = ("The Fire Guardian",
            "Fire Guardian: Ash on the road. Ink on the stone. Turn back, storm-girl.",
            "Fire Guardian: When they offer you a past... ask what it costs."),
        [Flags.BossFinal] = ("Ganfaul, the Tallyman",
            "Ganfaul: Then I'll take them the slow way.",
            "Ganfaul: Freely... given. I never could afford that."),
    };

    // ---------- chapters (by region scene): card title, recap on arrival, line at a closed seal ----------

    public static readonly Dictionary<string, (string chapter, string title, string recap, string locked)> Regions = new()
    {
        ["Forest"] = ("I", "The Spark",
            "Something drove the guardians of the Vale mad. The Storm alone got away, and hid in a girl on the hill road. She wakes with lightning in her hands and nothing else.",
            ""),
        ["Ice"] = ("II", "What the Cold Keeps",
            "The Earth guardian rests at last, and the statue has frozen the lake. Abe says the Frozen Reach is next, and that he will meet her there.",
            "Wulang: The lake won't hold me. Not yet."),
        ["Volcano"] = ("III", "The Price of Fire",
            "Frost answers to Wulang now. Past the mountain pass, the Fire guardian has sealed the Burning Peak, and Abe is already waiting at its foot.",
            "Wulang: The heat throws me back. The mountain is sealed."),
        ["FinalArena"] = ("IV", "The Sanctum",
            "Three guardians rest. Four gifts burn in one pair of hands. The rift opens on a sanctum in the sky, and Abe has gone ahead, the way he always does.",
            ""),
    };

    public static string ChapterTitle(string scene) => $"Chapter {Regions[scene].chapter}  -  {Regions[scene].title}";

    // ---------- objectives (by region scene, in story order) ----------

    public static readonly Dictionary<string, string[]> Objectives = new()
    {
        ["Forest"] = new[]
        {
            "Speak with the old man by the armory fire",
            "Put the Earth guardian to rest in the Blossom Glade",
            "Take up the Earth tome",
            "Wake the ice statue by the lake",
            "Cross the frozen lake into the Frozen Reach",
        },
        ["Ice"] = new[]
        {
            "Find the Ice guardian in the Frozen Amphitheater",
            "Take up the Ice tome",
            "Cross the mountain pass at the waystone by Abe's camp",
        },
        ["Volcano"] = new[]
        {
            "Follow the lava tube into the caldera and face the Fire guardian",
            "Take up the Fire tome",
            "Step into the rift at the back of the caldera",
        },
        ["FinalArena"] = new[]
        {
            "Find Abe at the heart of the Sanctum",
            "Defeat Ganfaul: answer each element he takes up with its weakness",
        },
    };

    // ---------- one-line beats (NarrativeTrigger subtitles, by region scene and trigger name) ----------

    public static readonly Dictionary<(string scene, string trigger), string> Subtitles = new()
    {
        [("Forest", "Statue awakens")] = "Wulang: The statue is humming. It wants the Earth I'm carrying. (F)",
        [("Ice", "Arrival")] = "Wulang: The wind has stopped. Everything here is holding its breath.",
        [("Volcano", "Arrival")] = "Wulang: The air itself is burning. I'll have to keep the cold close.",
    };

    // Abe's first word when his shop opens in each region.
    public static readonly Dictionary<string, string> Greetings = new()
    {
        ["Forest"] = "Abe: Potions, fresh this morning. Prices are fair. Mostly.",
        ["Ice"] = "Abe: Tea's free. Potions are not.",
        ["Volcano"] = "Abe: Last shop before the mountain. I'd stock up, spark.",
    };

    public const string LakeLine = "Wulang: The water's like knives. I can't swim this.";
    public const string LavaLine = "Wulang: Lava. Not my best idea.";

    // ---------- the opening and the endings ----------

    public const string MemoryTitle = "A Memory Returns";
    public const string TrueMemoryTitle = "The Rain on the Hill";

    public const string SubmissionTitle = "Ending: The Gilded Past";
    public const string SubmissionText = "Wulang knelt, and the four gifts left her one by one. In return she was given a past: a great name, a grateful people, a teacher she owed everything. It was warm, and it fitted perfectly, and none of it was hers. Over the Vale, the rain stopped falling for free.";
    public const string TruthTitle = "Ending: Freely Given";
    public const string TruthText = "Wulang walked back down through the Vale and gave the gifts back where she had found them: the earth to the glade, the cold to the reach, the fire to the mountain. She kept only the Storm, because it asked to stay. Then she went looking for a hill village, a kettle, and a woman who sang about the rain.";

    // ---------- applying it to the existing assets and scenes ----------

    [MenuItem("Wulang/Story/Apply story script")]
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        int dialogues = 0, items = 0;
        var faces = Faces();
        foreach (var (name, lines) in Conversations)
        {
            string path = $"Assets/Story/Dialogue/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null) AssetDatabase.CreateAsset(data = ScriptableObject.CreateInstance<DialogueData>(), path);
            data.lines = lines.Select(l => new DialogueData.Line
                { speaker = Speaker(l.who), portrait = l.who == Narrator ? null : faces[l.who == Young ? Wu : l.who], text = l.text }).ToArray();
            if (data.choices != null) for (int i = 0; i < data.choices.Length && i < OfferChoices.Length; i++) data.choices[i].text = OfferChoices[i];
            EditorUtility.SetDirty(data);
            dialogues++;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Story/Journal", "Assets/Items" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!Lore.TryGetValue(item.name, out var lore)) continue;
            item.description = lore;
            if (item.category != ItemCategory.Tome) item.displayName = Title(item.name);
            EditorUtility.SetDirty(item);
            items++;
        }
        AssetDatabase.SaveAssets();

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
        return $"StoryScript: {dialogues} conversations, {items} journal/item entries and all five scenes updated.";
    }

    private static Dictionary<int, Sprite> Faces() => new()
    {
        [Abe] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Story/Portraits/Abe.png"),
        [Wu] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Story/Portraits/Wulang.png"),
        [Gan] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Story/Portraits/Ganfaul.png"),
    };

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
                Set(trigger.GetComponent<Interactable>(), "prompt", $"Press F to read the {Title(give.name).ToLowerInvariant()}");
            }
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
