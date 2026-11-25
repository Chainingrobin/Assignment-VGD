using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level transitions for Wulang's streamed chapters (assignment brief, "Level Transition Implementation"; guide
/// Step 8).
/// </summary>
/// <remarks>
/// <para><b>Structure.</b> The World scene (terrain, sky, player, HUD) stays loaded; each story level (Forest, Ice,
/// Volcano, FinalArena) is its own region scene loaded on top of it with <c>SceneManager.LoadSceneAsync</c>, so the
/// next chapter is always visible on the horizon.</para>
/// <para><b>Conditioned on story state, not position.</b> Walking into a region's domain is the transition only if its
/// <c>requiredFlag</c> is set (Ice needs <c>lakeFrozen</c>, Volcano needs <c>bossIceDefeated</c>); the Sanctum is reached
/// only through the rift (<see cref="RegionTravel"/>) once <c>bossFireDefeated</c> opens it. A locked region's elemental
/// seal pushes the player back with a line from Wulang instead of a UI border.</para>
/// <para><b>What the player sees.</b> A landmark flight (or the live view) under the chapter card: the chapter title, a
/// recap of the story so far and the loading percentage, while the region really loads and the finished one unloads.
/// Space skips. Each arrival checkpoints the story (<see cref="StoryStateManager.Save"/>) and raises
/// <see cref="RegionEntered"/>, which refreshes the objective tracker.</para>
/// <para>Also runs the New Game intro (<c>IntroCutscene</c>), the pause menu's Restart Region, and the main menu's live
/// backdrop.</para>
/// </remarks>
[DefaultExecutionOrder(-100)]
public class RegionManager : MonoBehaviour
{
    public const string WorldScene = "World";
    public const string MenuScene = "MainMenu";

    /// <summary>One chapter's scene, its chapter card text, the flag that unlocks it, and where it begins on the map.</summary>
    [Serializable]
    public class Region
    {
        public string scene;
        // Chapter card title and recap, written from the Story Text asset (Wulang > Story > Apply story text).
        public string title;
        [TextArea] public string recap;
        [Tooltip("Story flag needed to enter. Empty = always open.")]
        public string requiredFlag;
        [Tooltip("Flight played while the region loads. Empty = fade instead.")]
        public FreezeRevealCutscene cutscene;
        public bool whiteFade;
        [Tooltip("World XZ rectangle; walking inside it enters the region. Zero size = entered only by script (e.g. the rift).")]
        public Vector2 domainMin;
        public Vector2 domainMax;
        [Tooltip("Seal effect shown while the region is still locked.")]
        public GameObject seal;
        public string lockedLine;
    }

    [SerializeField] private Region[] regions;
    [Tooltip("The opening narration (IntroCutscene): narrator lines, then Abe's wake-up line.")]
    [SerializeField] private DialogueData introNarration;
    [Tooltip("Seconds the chapter card stays up at least, long enough to read its recap.")]
    [SerializeField, Min(0f)] private float minimumCaptionTime = 5.5f;

    // Set by the main menu before loading the World: which region to start in.
    public static string PendingRegion = "Forest";
    // The main menu loads the World behind itself as a living backdrop: no player, HUD or region.
    public static bool MenuPreview;

    public static RegionManager Instance { get; private set; }
    /// <summary>The region scene the player is in (e.g. "Ice").</summary>
    public string Current { get; private set; }
    /// <summary>True during a transition, the intro or a restart; triggers and exits wait for it.</summary>
    public bool Busy { get; private set; }
    /// <summary>Raised with the region's name after every arrival (and its checkpoint).</summary>
    public event Action<string> RegionEntered;

    private Transform player;
    private CharacterController body;
    private float sealCooldown;
    private readonly HashSet<string> warned = new();
    private readonly HashSet<string> visited = new();

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // Loads the starting region (or every region in New Game+), places the player, and plays the intro on a New Game.
    private IEnumerator Start()
    {
        SceneManager.SetActiveScene(gameObject.scene);
        if (MenuPreview)
        {
            HideGameplayForMenu();
            yield break;
        }
        PlayerLock.Reset();
        var controller = FindFirstObjectByType<FirstPersonController>();
        player = controller.transform;
        body = controller.GetComponent<CharacterController>();

        var story = StoryStateManager.Instance;
        var alreadyLoaded = new List<string>();
        foreach (var region in regions)
            if (SceneManager.GetSceneByName(region.scene).isLoaded) alreadyLoaded.Add(region.scene);

        if (alreadyLoaded.Count > 0)
        {
            // Play was pressed on a region scene: it loaded us, so just adopt it.
            Current = alreadyLoaded[0];
        }
        else
        {
            var overlay = TransitionOverlay.Instance;
            StartCoroutine(overlay.FadeTo(1f, 0f));
            if (story.NewGamePlus)
            {
                foreach (var region in regions)
                    if (HasDomain(region) || region.scene == "Forest")
                        yield return SceneManager.LoadSceneAsync(region.scene, LoadSceneMode.Additive);
                Current = "Forest";
            }
            else
            {
                Current = string.IsNullOrEmpty(PendingRegion) ? "Forest" : PendingRegion;
                yield return SceneManager.LoadSceneAsync(Current, LoadSceneMode.Additive);
            }
        }
        yield return null; // let the region's Awake/Start (prerequisites, objectives) run
        MoveToSpawn(Current);
        // A fresh New Game opens on its region's flight under the chapter card (the intro cutscene);
        // the prologue's own beat waits for it, since it holds off while a transition is busy.
        var opening = Array.Find(regions, r => r.scene == Current);
        bool intro = alreadyLoaded.Count == 0 && !story.NewGamePlus && opening?.cutscene != null && !story.HasFlag(Flags.IntroSeen);
        if (intro) story.SetFlag(Flags.IntroSeen); // before the checkpoint, so Continue doesn't replay it
        Arrived();
        if (intro) StartCoroutine(introNarration != null ? Opening(opening) : Enter(opening));
        if (!intro || introNarration == null) yield return TransitionOverlay.Instance.FadeTo(0f, 0.8f);
        RefreshSeals();
    }

    // Walking into another region's domain: enter it if its story flag is set, otherwise the seal pushes back.
    private void Update()
    {
        if (Busy || player == null) return;
        var story = StoryStateManager.Instance;
        if (story.NewGamePlus) return;
        sealCooldown -= Time.deltaTime;
        var p = new Vector2(player.position.x, player.position.z);
        foreach (var region in regions)
        {
            if (!HasDomain(region) || region.scene == Current) continue;
            if (p.x < region.domainMin.x || p.x > region.domainMax.x || p.y < region.domainMin.y || p.y > region.domainMax.y) continue;
            if (story.HasFlag(region.requiredFlag)) { StartCoroutine(Enter(region, walked: true)); return; }
            PushOut(region, p);
        }
    }

    private static bool HasDomain(Region region) =>
        region.domainMax.x > region.domainMin.x && region.domainMax.y > region.domainMin.y;

    // The locked seal: shove the player back out through the nearest edge and singe them a little.
    private void PushOut(Region region, Vector2 p)
    {
        float left = p.x - region.domainMin.x, right = region.domainMax.x - p.x;
        float bottom = p.y - region.domainMin.y, top = region.domainMax.y - p.y;
        float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        var direction = nearest == left ? Vector3.left : nearest == right ? Vector3.right : nearest == bottom ? Vector3.back : Vector3.forward;
        body.Move(direction * (nearest + 1.5f));
        if (sealCooldown > 0f) return;
        sealCooldown = 1f;
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.TakeDamage(5f, ElementType.None);
        GameAudio.Play("Sfx/Seal", null, 0.7f);
        if (!string.IsNullOrEmpty(region.lockedLine) && warned.Add(region.scene)) Subtitle.Show(region.lockedLine);
    }

    private void RefreshSeals()
    {
        var story = StoryStateManager.Instance;
        foreach (var region in regions)
            if (region.seal != null)
                region.seal.SetActive(!story.NewGamePlus && region.scene != Current && !story.HasFlag(region.requiredFlag));
    }

    private void HideGameplayForMenu()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null) player.gameObject.SetActive(false);
        foreach (var hud in FindObjectsByType<ResponsiveHUD>(FindObjectsSortMode.None))
            if (hud.gameObject.scene == gameObject.scene) hud.gameObject.SetActive(false);
        foreach (var events in gameObject.scene.GetRootGameObjects())
            if (events.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) events.SetActive(false);
        foreach (var behaviour in GetComponents<MonoBehaviour>())
            if (behaviour != this) behaviour.enabled = false;
        enabled = false;
    }

    /// <summary>
    /// Starts the transition into a region. Used by scripted exits (the waystone, the rift), which are themselves gated
    /// on story flags. Ignored while another transition is running.
    /// </summary>
    /// <param name="scene">Region scene name, e.g. "FinalArena".</param>
    public void EnterRegion(string scene)
    {
        if (Busy) return;
        foreach (var region in regions)
            if (region.scene == scene) { StartCoroutine(Enter(region)); return; }
        Debug.LogError($"RegionManager: no region named {scene}", this);
    }

    // The transition itself: flight or fade, chapter card with recap, async load with progress, unload the previous
    // region, place the player, checkpoint, then hold the card long enough to read (Space skips).
    private IEnumerator Enter(Region region, bool walked = false)
    {
        Busy = true;
        PlayerLock.Push();
        var overlay = TransitionOverlay.Instance;
        float started = Time.unscaledTime;
        // Walking back into a region already played: load it quietly, no flight or chapter card again.
        bool revisit = region.scene != Current && visited.Contains(region.scene);
        bool flight = region.cutscene != null && !revisit;
        // Walking across a border with no flight (the frozen lake into the Frozen Reach): the chapter
        // card plays over the live view and the player stays where they walked to. No black screen,
        // no teleport, and no fly-over of ground they have just seen.
        bool live = walked && !flight;
        if (flight)
        {
            // Dip to black around the flight: the camera cuts to the drone and back unseen, never swinging across the map.
            yield return overlay.FadeTo(1f, 0.35f, Color.black);
            region.cutscene.PlayTransition();
            StartCoroutine(overlay.FadeTo(0f, 0.7f));
        }
        else if (!live) yield return overlay.FadeTo(1f, 0.8f, region.whiteFade ? Color.white : Color.black);
        if (!revisit) yield return overlay.ShowCaption(region.title, RecapFor(region));

        // The opening flight presents the region that is already loaded; every other entry loads it.
        if (region.scene != Current)
        {
            string previous = Current;
            var load = SceneManager.LoadSceneAsync(region.scene, LoadSceneMode.Additive);
            while (!load.isDone)
            {
                overlay.SetProgress($"{Mathf.RoundToInt(load.progress * 100f)}%");
                yield return null;
            }
            overlay.SetProgress("");
            if (!string.IsNullOrEmpty(previous) && SceneManager.GetSceneByName(previous).isLoaded)
                yield return SceneManager.UnloadSceneAsync(previous);
            Current = region.scene;
            yield return null;
            if (!live) MoveToSpawn(Current);
            Arrived();
        }

        bool closing = false;
        while (flight && region.cutscene.IsPlaying)
        {
            if (!closing && region.cutscene.Remaining < 0.45)
            {
                closing = true;
                StartCoroutine(overlay.FadeTo(1f, 0.4f, Color.black));
            }
            yield return null;
        }
        if (flight) StartCoroutine(overlay.FadeTo(0f, 0.7f));
        if (!revisit)
        {
            // Hold the caption long enough to read, unless the player skips.
            overlay.ShowSkipHint(true);
            while (Time.unscaledTime - started < minimumCaptionTime && !TransitionOverlay.SkipPressed) yield return null;
            overlay.ShowSkipHint(false);
        }
        if (!flight && !live) StartCoroutine(overlay.FadeTo(0f, 0.8f));
        if (!revisit) yield return overlay.HideCaption();
        RefreshSeals();
        PlayerLock.Pop();
        Busy = false;
    }

    // A new game's intro cutscene; nothing else moves the player meanwhile.
    private IEnumerator Opening(Region opening)
    {
        Busy = true;
        yield return IntroCutscene.Play(introNarration, opening.cutscene, opening.title, RecapFor(opening), opening.scene);
        Busy = false;
    }

    /// <summary>Pause menu "Restart Region": reloads the current region scene from its start with full health.
    /// Story flags are kept, so finished beats stay finished.</summary>
    public void RestartRegion()
    {
        if (!Busy && !string.IsNullOrEmpty(Current)) StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        Busy = true;
        PlayerLock.Push();
        var overlay = TransitionOverlay.Instance;
        yield return overlay.FadeTo(1f, 0.6f, Color.black);
        BossHealthBar.Hide();
        if (SceneManager.GetSceneByName(Current).isLoaded) yield return SceneManager.UnloadSceneAsync(Current);
        yield return SceneManager.LoadSceneAsync(Current, LoadSceneMode.Additive);
        yield return null;
        MoveToSpawn(Current);
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.HealFull();
        Arrived();
        yield return overlay.FadeTo(0f, 0.8f);
        PlayerLock.Pop();
        Busy = false;
    }

    // Every arrival in a region: remember it was played and checkpoint the story there.
    private void Arrived()
    {
        visited.Add(Current);
        StoryStateManager.Instance.Save(Current);
        RegionEntered?.Invoke(Current);
    }

    // The chapter recap (written from the Story Text asset).
    private static string RecapFor(Region region) => region.recap;

    /// <summary>Where the player starts in the current region (its <c>RegionStart</c>), or null.</summary>
    public Transform CurrentSpawn
    {
        get
        {
            var start = RegionStart.Find(Current);
            return start != null ? start.Spawn : null;
        }
    }

    /// <summary>Moves the player to a region's start point.</summary>
    public void MoveToSpawn(string scene)
    {
        var start = RegionStart.Find(scene);
        if (start == null || player == null) return;
        Teleport(start.Spawn.position, start.Spawn.rotation);
    }

    /// <summary>Moves the player instantly, keeping only the yaw of <paramref name="rotation"/>.</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        // The CharacterController overrides transform changes while enabled.
        body.enabled = false;
        player.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        body.enabled = true;
    }

    /// <summary>Returns to the main menu (pause menu and end cards), freeing the cursor.</summary>
    public static void LoadMenu()
    {
        PlayerLock.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MenuScene);
    }
}
