using System;
using System.Collections.Generic;
using UnityEngine;

// Holds every story flag for the current playthrough. Triggers set flags; doors, gates,
// dialogue and region transitions read them. Lives across scene loads (DontDestroyOnLoad),
// so progress carries from the World scene into every region scene and back to the menu.
public class StoryStateManager : MonoBehaviour
{
    private static StoryStateManager instance;
    private static bool quitting;

    // Created on first use, so any scene can be played directly from the Editor.
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

    private readonly HashSet<string> flags = new();
    public event Action<string> OnFlagSet;
    public IReadOnlyCollection<string> Flags => flags;

    // New Game+: every region loaded at once, all elements, no gates or story beats.
    public bool NewGamePlus { get; set; }

    // Persisted between sessions: unlocked by finishing either ending.
    public static bool NewGamePlusUnlocked
    {
        get => PlayerPrefs.GetInt("wulang.ngplus", 0) == 1;
        set { PlayerPrefs.SetInt("wulang.ngplus", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += () => quitting = true;
    }

    public void SetFlag(string flag)
    {
        if (!string.IsNullOrEmpty(flag) && flags.Add(flag)) OnFlagSet?.Invoke(flag);
    }

    // An empty requirement always passes, so optional gates can leave it blank.
    public bool HasFlag(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);

    public void ClearAll()
    {
        flags.Clear();
        NewGamePlus = false;
    }
}

// Every flag name in one place so a typo can't silently break a gate.
public static class Flags
{
    public const string BossEarth = "bossEarthDefeated";
    public const string BossIce = "bossIceDefeated";
    public const string BossFire = "bossFireDefeated";
    public const string BossFinal = "bossFinalDefeated";
    public const string LakeFrozen = "lakeFrozen";
    public const string ChoseSubmission = "choseSubmission";
    public const string EndingTruth = "endingTruth";
    public static string Has(ElementType element) => "has" + element;
}
