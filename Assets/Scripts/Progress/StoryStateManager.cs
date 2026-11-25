using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The story's memory: the set of story flags for the current playthrough (assignment guide, Step 5).
/// </summary>
/// <remarks>
/// <para><b>Role.</b> Narrative triggers, conversations, boss deaths and item pickups <i>write</i> flags with
/// <see cref="SetFlag"/>. Everything that depends on the story <i>reads</i> them with <see cref="HasFlag"/> or
/// listens to <see cref="OnFlagSet"/>: the objective tracker (<c>ObjectiveManager</c>), gates and seals
/// (<c>FlagGate</c>, <c>RegionManager</c>), level exits (<c>RegionTravel</c>), the endings and the save.</para>
/// <para><b>Lifetime.</b> One instance lives across scene loads (<c>DontDestroyOnLoad</c>), so progress carries from the
/// World scene into every region scene and back to the main menu (the guide's "common mistake" of losing progress
/// between levels cannot happen).</para>
/// <para><b>Flags are strings</b> such as <c>metAbe</c>, <c>bossEarthDefeated</c> or <c>refusedOffer</c>. The ones code
/// refers to are constants in <see cref="Flags"/>; the rest are typed on the trigger and dialogue assets that set them
/// (see the flag list in GDD section 6).</para>
/// <para><b>Saving.</b> Arriving in a region checkpoints the region name and every flag to PlayerPrefs, which is
/// what the main menu's Continue restores.</para>
/// </remarks>
public class StoryStateManager : MonoBehaviour
{
    private static StoryStateManager instance;
    private static bool quitting;

    /// <summary>
    /// The single story state. Created on first use, so any scene can be played directly from the Editor without a
    /// bootstrap object; returns null only while the application is quitting.
    /// </summary>
    public static StoryStateManager Instance
    {
        get
        {
            if (instance == null && !quitting)
                instance = new GameObject(nameof(StoryStateManager)).AddComponent<StoryStateManager>();
            return instance;
        }
    }

    // Static state must reset when Enter Play Mode skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; quitting = false; }

    // A set, so setting a flag twice is harmless and OnFlagSet fires only the first time (beats never replay).
    private readonly HashSet<string> flags = new();

    /// <summary>Raised once per flag, the moment it is first set. Objectives, gates and watchers update from this.</summary>
    public event Action<string> OnFlagSet;

    /// <summary>Every flag set so far (read-only; used by the save and the story-path check).</summary>
    public IReadOnlyCollection<string> Flags => flags;

    /// <summary>New Game+: every region loaded at once, all elements, no gates or story beats.</summary>
    public bool NewGamePlus { get; set; }

    /// <summary>Whether New Game+ is offered on the main menu. Persisted between sessions: unlocked by finishing either ending.</summary>
    public static bool NewGamePlusUnlocked
    {
        get => PlayerPrefs.GetInt("wulang.ngplus", 0) == 1;
        set { PlayerPrefs.SetInt("wulang.ngplus", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Checkpoint between sessions: the region last entered and every flag at that moment.
    // shortcut: coins, potions and journal entries aren't saved; add them if playtests need it.
    private const string SaveKey = "wulang.save";

    /// <summary>True when a checkpoint exists, so the main menu shows Continue.</summary>
    public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

    /// <summary>
    /// Checkpoints the story: stores <paramref name="region"/> and every flag. Called by <c>RegionManager</c> on each
    /// arrival in a region. New Game+ runs are never saved.
    /// </summary>
    /// <param name="region">Region scene the player has just entered (e.g. "Ice").</param>
    public void Save(string region)
    {
        if (NewGamePlus || string.IsNullOrEmpty(region)) return;
        // Format: the region on the first line, then one flag per line.
        PlayerPrefs.SetString(SaveKey, region + "\n" + string.Join("\n", flags));
        PlayerPrefs.Save();
    }

    /// <summary>Restores the checkpoint's flags (replacing the current ones) and returns its region.</summary>
    /// <returns>The region scene to load, for the main menu's Continue.</returns>
    public string Load()
    {
        ClearAll();
        var lines = PlayerPrefs.GetString(SaveKey).Split('\n');
        // SetFlag (not a raw Add) so anything already listening hears about each restored flag.
        for (int i = 1; i < lines.Length; i++) SetFlag(lines[i]);
        return lines[0];
    }

    private void Awake()
    {
        // Singleton: a second copy (e.g. one saved in a scene) removes itself.
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += () => quitting = true;
    }

    /// <summary>
    /// Records a story event. Empty names are ignored, so optional "flag to set" fields on triggers and dialogue can be
    /// left blank. <see cref="OnFlagSet"/> fires only the first time a flag is set.
    /// </summary>
    /// <param name="flag">Flag name, e.g. "metAbe".</param>
    public void SetFlag(string flag)
    {
        if (!string.IsNullOrEmpty(flag) && flags.Add(flag)) OnFlagSet?.Invoke(flag);
    }

    /// <summary>
    /// Whether a story event has happened. An empty requirement always passes, so optional gates can leave it blank.
    /// </summary>
    /// <param name="flag">Flag name; empty or null means "no requirement".</param>
    public bool HasFlag(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);

    /// <summary>Forgets every flag and leaves New Game+ (New Game, chapter select and Load start from here).</summary>
    public void ClearAll()
    {
        flags.Clear();
        NewGamePlus = false;
    }
}

/// <summary>
/// The story flags that code refers to, in one place so a typo can't silently break a gate. Flags used only by
/// assets (dialogue, triggers, objectives) are typed on those assets instead.
/// </summary>
public static class Flags
{
    /// <summary>Earth Guardian (Forest, Chapter I) defeated; its tome drops and the ice statue wakes.</summary>
    public const string BossEarth = "bossEarthDefeated";
    /// <summary>Ice Guardian (Frozen Reach, Chapter II) defeated; unlocks the mountain-pass waystone to the volcano.</summary>
    public const string BossIce = "bossIceDefeated";
    /// <summary>Fire Guardian (Burning Peak, Chapter III) defeated; opens the rift to the Sanctum.</summary>
    public const string BossFire = "bossFireDefeated";
    /// <summary>Ganfaul defeated: the Truth ending.</summary>
    public const string BossFinal = "bossFinalDefeated";
    /// <summary>The statue froze the lake; the Frozen Reach's seal lifts.</summary>
    public const string LakeFrozen = "lakeFrozen";
    /// <summary>The New Game intro cutscene has played, so Continue never replays it.</summary>
    public const string IntroSeen = "introSeen";
    /// <summary>Wulang knelt to Abe's offer: the Submission ("Gilded Past") ending.</summary>
    public const string ChoseSubmission = "choseSubmission";
    /// <summary>The Truth ("Freely Given") ending finished.</summary>
    public const string EndingTruth = "endingTruth";

    /// <summary>The flag set when an element is claimed, e.g. "hasEarth"; it also triggers that element's memory echo.</summary>
    public static string Has(ElementType element) => "has" + element;
}
