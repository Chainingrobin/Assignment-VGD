using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Assignment Step 11, automated: plays the whole story in Play mode with no one at the keyboard.
// Enter Play mode (it opens on the main menu), then run "Wulang > Checks > Full story path (Play mode)".
// It presses New Game, skips the intro, talks to Abe, beats each guardian, takes each tome (the real BookCollect),
// sits through each memory, wakes the statue, walks over the frozen lake, takes the waystone and
// the rift, and in the Sanctum refuses the offer, sits through the reveal and beats Ganfaul for the
// Truth ending. Back at the menu it checks New Game+, then starts the Sanctum from chapter select
// and kneels for the Submission ending, and finally checks Continue. At every step the objective on
// screen must match the story state, and every beat must fire once and in order.
// Dialogue lines are advanced and choices picked for it. Read the outcome from StoryPathCheck.Result.
public static class StoryPathCheck
{
    public static string Result { get; private set; } = "not run";
    private static readonly List<string> log = new();
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int choice = -1;

    [MenuItem("Wulang/Checks/Full story path (Play mode)")]
    public static void Begin()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
        log.Clear();
        Result = "running";
        TransitionOverlay.Instance.StartCoroutine(Run());
        TransitionOverlay.Instance.StartCoroutine(Autopilot());
    }

    // Runs the path's nested steps itself, so a failure inside any of them ends the check with its message.
    private static IEnumerator Run()
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Path());
        while (stack.Count > 0)
        {
            object current;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                current = stack.Peek().Current;
            }
            catch (System.Exception e)
            {
                Result = "FAIL: " + e.Message + "\n" + string.Join("\n", log);
                Debug.LogError("StoryPathCheck " + Result);
                yield break;
            }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
        Result = "PASS\n" + string.Join("\n", log);
        Debug.Log("StoryPathCheck " + Result);
    }

    // Presses on for the player: advances every dialogue line, picks the planned choice, skips the intro.
    private static IEnumerator Autopilot()
    {
        var advance = typeof(DialogueManager).GetField("advance", Hidden);
        var picked = typeof(DialogueManager).GetField("picked", Hidden);
        var skip = typeof(IntroCutscene).GetField("skip", Hidden);
        while (Result == "running")
        {
            skip.SetValue(null, true);
            var dialogue = DialogueManager.Instance;
            if (dialogue.IsTalking)
            {
                advance.SetValue(dialogue, true);
                var ending = Object.FindFirstObjectByType<EndingController>();
                if (choice >= 0 && ending != null && picked.GetValue(dialogue) == null)
                {
                    var offer = (DialogueData)typeof(EndingController).GetField("offer", Hidden).GetValue(ending);
                    picked.SetValue(dialogue, offer.choices[choice]);
                }
            }
            yield return null;
        }
    }

    private static IEnumerator Path()
    {
        // From the main menu, as a player would: New Game.
        if (RegionManager.MenuPreview || SceneManager.GetActiveScene().name == RegionManager.MenuScene)
            typeof(MainMenu).GetMethod("StartAt", Hidden).Invoke(null, new object[] { "Forest", false });
        yield return Until(() => RegionManager.Instance != null && !RegionManager.MenuPreview, 30f, "New Game to load the World");
        var story = StoryStateManager.Instance;
        // The intro sets introSeen as it starts; then wait for it to hand control back.
        yield return Until(() => story.HasFlag(Flags.IntroSeen) && RegionManager.Instance.Current == "Forest" && !RegionManager.Instance.Busy && !PlayerLock.IsLocked,
            90f, "the intro to end in the Forest");
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        PlayerHealth.Instance.Invulnerable = true;

        // ----- Chapter I: the Forest -----
        Expect(0);
        Talk("Forest");
        yield return Until(() => story.HasFlag("metAbe"), 30f, "Abe's first conversation");
        Expect(1);
        yield return Guardian("Forest", ElementType.Lightning, ElementType.Earth);
        Expect(3);
        var freezer = Object.FindFirstObjectByType<WaterFreezer>();
        GameObject.Find("Ice statue").GetComponentInChildren<Interactable>().Interact();
        yield return Until(() => freezer.Progress > 0f, 10f, "the statue to start the freeze");
        freezer.FinishNow();
        yield return Until(() => story.HasFlag(Flags.LakeFrozen) && !PlayerLock.IsLocked, 60f, "the freeze reveal");
        Expect(4);
        RegionManager.Instance.Teleport(SceneSplitSetup.Ground(new Vector2(655f, 330f)) + Vector3.up, Quaternion.identity); // across into the Reach
        yield return Until(() => RegionManager.Instance.Current == "Ice" && !RegionManager.Instance.Busy, 60f, "the walk into the Frozen Reach");
        Note("Chapter II loaded after walking over the frozen lake");

        // ----- Chapter II: the Frozen Reach -----
        yield return Until(() => story.HasFlag("iceArrived"), 10f, "the Ice arrival line");
        Expect(0);
        Talk("Ice");
        yield return Until(() => !DialogueManager.Instance.IsTalking && !PlayerLock.IsLocked, 30f, "Abe's Ice conversation");
        yield return Guardian("Ice", ElementType.Fire, ElementType.Ice);
        Expect(2);
        Object.FindObjectsByType<RegionTravel>(FindObjectsSortMode.None).First(t => t.gameObject.scene.name == "Ice").GetComponent<Interactable>().Interact();
        yield return Until(() => RegionManager.Instance.Current == "Volcano" && !RegionManager.Instance.Busy, 90f, "the waystone to the Burning Peak");
        Note("Chapter III loaded through the waystone");

        // ----- Chapter III: the Burning Peak -----
        yield return Until(() => story.HasFlag("volcanoArrived"), 10f, "the Volcano arrival line");
        Expect(0);
        Talk("Volcano");
        yield return Until(() => !DialogueManager.Instance.IsTalking && !PlayerLock.IsLocked, 30f, "Abe's Volcano conversation");
        yield return Guardian("Volcano", ElementType.Ice, ElementType.Fire);
        Expect(2);
        Object.FindObjectsByType<RegionTravel>(FindObjectsSortMode.None).First(t => t.gameObject.scene.name == "Volcano").GetComponent<Interactable>().Interact();
        yield return Until(() => RegionManager.Instance.Current == "FinalArena" && !RegionManager.Instance.Busy, 90f, "the rift to the Sanctum");
        Note("Chapter IV loaded through the rift");

        // ----- Chapter IV: the Sanctum, refusing -----
        Expect(0);
        choice = 1;
        var ending = Object.FindFirstObjectByType<EndingController>();
        RegionManager.Instance.Teleport(ending.transform.position + Vector3.back * 6f + Vector3.up * 0.5f, Quaternion.identity);
        yield return Until(() => story.HasFlag(EndingController.RefusedOffer), 30f, "the offer and the refusal");
        yield return Until(() => story.HasFlag(BossEncounter.RevealSeen), 20f, "the reveal cutscene");
        Expect(1);
        var ganfaul = Object.FindFirstObjectByType<BossEncounter>().Boss.Target;
        while (!ganfaul.IsDead)
        {
            // Only his current weakness hurts him properly; work through every element.
            foreach (ElementType element in System.Enum.GetValues(typeof(ElementType)))
                if (element != ElementType.None && !ganfaul.IsDead && !ganfaul.Invulnerable) ganfaul.TakeDamage(400f, element);
            yield return new WaitForSeconds(0.5f);
        }
        yield return Until(() => story.HasFlag(Flags.EndingTruth), 15f, "the Truth ending flag");
        yield return Until(() => SceneManager.GetActiveScene().name == RegionManager.MenuScene, 90f, "the true memory, the end card and the menu");
        Check(StoryStateManager.NewGamePlusUnlocked, "An ending must unlock New Game+.");
        Note("Truth ending reached; New Game+ unlocked; back at the main menu");

        // ----- Chapter select: the Sanctum again, kneeling -----
        choice = 0;
        typeof(MainMenu).GetMethod("StartAt", Hidden).Invoke(null, new object[] { "FinalArena", false });
        yield return Until(() => RegionManager.Instance != null && RegionManager.Instance.Current == "FinalArena" && !RegionManager.Instance.Busy, 60f, "chapter select into the Sanctum");
        yield return Until(() => PlayerMagicAffinity.Instance.HasElement(ElementType.Fire), 5f, "chapter select to grant every element");
        Note("Chapter select started the Sanctum with all four elements");
        PlayerHealth.Instance.Invulnerable = true;
        ending = Object.FindFirstObjectByType<EndingController>();
        RegionManager.Instance.Teleport(ending.transform.position + Vector3.back * 6f + Vector3.up * 0.5f, Quaternion.identity);
        yield return Until(() => story.HasFlag(Flags.ChoseSubmission), 30f, "the offer and kneeling");
        yield return Until(() => SceneManager.GetActiveScene().name == RegionManager.MenuScene, 60f, "the Submission ending and the menu");
        Check(!story.HasFlag(BossEncounter.RevealSeen), "Kneeling must not start the fight.");
        Note("Submission ending reached");

        // ----- Continue: back to the start of the last region entered -----
        Check(StoryStateManager.HasSave, "There must be a save to continue from.");
        typeof(MainMenu).GetMethod("Continue", Hidden).Invoke(null, null);
        yield return Until(() => RegionManager.Instance != null && RegionManager.Instance.Current == "FinalArena" && !RegionManager.Instance.Busy, 60f, "Continue");
        Note("Continue reopened the Sanctum");
        choice = -1;
    }

    // A guardian fight: engage, defeat it, take its tome (the real pickup), sit through the memory.
    private static IEnumerator Guardian(string region, ElementType weakness, ElementType gift)
    {
        var story = StoryStateManager.Instance;
        var encounter = Object.FindObjectsByType<BossEncounter>(FindObjectsSortMode.None).First(e => e.gameObject.scene.name == region);
        var flag = (string)typeof(BossEncounter).GetField("defeatFlag", Hidden).GetValue(encounter);
        var boss = encounter.Boss;
        RegionManager.Instance.Teleport(boss.transform.position + boss.transform.forward * 10f + Vector3.up, Quaternion.LookRotation(-boss.transform.forward));
        yield return Until(() => boss.Engaged, 15f, region + " guardian engaging");
        Check(GameAudio.CurrentMood == "Boss", "The boss music must start with the fight.");
        Expect(region == "Forest" ? 1 : 0);
        while (!boss.Target.IsDead)
        {
            boss.Target.TakeDamage(500f, weakness);
            yield return new WaitForSeconds(0.3f);
        }
        yield return Until(() => story.HasFlag(flag), 10f, region + " guardian's defeat flag");
        Expect(region == "Forest" ? 2 : 1);
        var tome = Object.FindObjectsByType<BookCollect>(FindObjectsSortMode.None).FirstOrDefault(b => b.Element == gift);
        Check(tome != null, region + " guardian must drop the " + gift + " tome.");
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>().GetComponent<Collider>();
        typeof(BookCollect).GetMethod("OnTriggerEnter", Hidden).Invoke(tome, new object[] { player });
        Check(story.HasFlag(Flags.Has(gift)), "Taking the tome must unlock " + gift + ".");
        yield return Until(() => Object.FindObjectsByType<MemoryEcho>(FindObjectsSortMode.None).Any(m => m.Playing), 10f, region + " memory echo starting");
        yield return Until(() => !Object.FindObjectsByType<MemoryEcho>(FindObjectsSortMode.None).Any(m => m.Playing) && !PlayerLock.IsLocked, 60f, region + " memory echo");
        Note($"{region}: guardian beaten, {gift} claimed, memory played");
    }

    private static void Talk(string region)
    {
        var abe = Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None).First(m => m.gameObject.scene.name == region);
        abe.GetComponent<Interactable>().Interact();
    }

    // The objective on screen must be the region's objective at this index (StoryScript's wording).
    private static void Expect(int index)
    {
        var region = RegionManager.Instance.Current;
        var list = Object.FindObjectsByType<RegionObjectives>(FindObjectsSortMode.None).First(o => o.gameObject.scene.name == region);
        var manager = Object.FindFirstObjectByType<ObjectiveManager>();
        manager.Refresh();
        var shown = (string)typeof(ObjectiveManager).GetField("shown", Hidden).GetValue(manager);
        Check(shown == list.Objectives[index].text, $"{region}: expected objective \"{list.Objectives[index].text}\", shown \"{shown}\".");
        Note($"{region} objective: {shown}");
    }

    private static IEnumerator Until(System.Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > until) throw new System.TimeoutException("Timed out waiting for " + what + ".");
            yield return null;
        }
    }

    private static void Note(string line) => log.Add(line);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }
}
