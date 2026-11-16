using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Level streaming for Wulang's open world. The World scene (terrain, sky, player, HUD) stays
// loaded; each story level is its own region scene loaded on top of it. Walking into a
// region's domain is the level transition: if its story flag is set, a cutscene plays with
// the chapter title and recap while the region scene really loads (additively) and the
// finished one unloads. If the flag isn't set, the region's elemental seal pushes the
// player back instead, so there are no UI world-border messages.
[DefaultExecutionOrder(-100)]
public class RegionManager : MonoBehaviour
{
    public const string WorldScene = "World";
    public const string MenuScene = "MainMenu";

    [Serializable]
    public class Region
    {
        public string scene;
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
    [SerializeField, Min(0f)] private float minimumCaptionTime = 3f;

    // Set by the main menu before loading the World: which region to start in.
    public static string PendingRegion = "Forest";

    public static RegionManager Instance { get; private set; }
    public string Current { get; private set; }
    public bool Busy { get; private set; }
    public event Action<string> RegionEntered;

    private Transform player;
    private CharacterController body;
    private float sealCooldown;
    private readonly HashSet<string> warned = new();

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private IEnumerator Start()
    {
        SceneManager.SetActiveScene(gameObject.scene);
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
        RegionEntered?.Invoke(Current);
        yield return TransitionOverlay.Instance.FadeTo(0f, 0.8f);
        RefreshSeals();
    }

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
            if (story.HasFlag(region.requiredFlag)) { StartCoroutine(Enter(region)); return; }
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
        if (!string.IsNullOrEmpty(region.lockedLine) && warned.Add(region.scene)) Subtitle.Show(region.lockedLine);
    }

    private void RefreshSeals()
    {
        var story = StoryStateManager.Instance;
        foreach (var region in regions)
            if (region.seal != null)
                region.seal.SetActive(!story.NewGamePlus && region.scene != Current && !story.HasFlag(region.requiredFlag));
    }

    // Used by scripted exits such as the volcano rift.
    public void EnterRegion(string scene)
    {
        if (Busy) return;
        foreach (var region in regions)
            if (region.scene == scene) { StartCoroutine(Enter(region)); return; }
        Debug.LogError($"RegionManager: no region named {scene}", this);
    }

    private IEnumerator Enter(Region region)
    {
        Busy = true;
        PlayerLock.Push();
        var overlay = TransitionOverlay.Instance;
        float started = Time.unscaledTime;
        bool flight = region.cutscene != null;
        if (flight) region.cutscene.PlayTransition();
        else yield return overlay.FadeTo(1f, 0.8f, region.whiteFade ? Color.white : Color.black);
        yield return overlay.ShowCaption(region.title, RecapFor(region));

        string previous = Current;
        var load = SceneManager.LoadSceneAsync(region.scene, LoadSceneMode.Additive);
        while (!load.isDone)
        {
            overlay.SetProgress($"{Mathf.RoundToInt(load.progress * 100f)}%");
            yield return null;
        }
        overlay.SetProgress("");
        if (!string.IsNullOrEmpty(previous) && previous != region.scene && SceneManager.GetSceneByName(previous).isLoaded)
            yield return SceneManager.UnloadSceneAsync(previous);
        Current = region.scene;
        yield return null;
        MoveToSpawn(Current);
        RegionEntered?.Invoke(Current);

        while (flight && region.cutscene.IsPlaying) yield return null;
        while (Time.unscaledTime - started < minimumCaptionTime) yield return null;
        if (!flight) StartCoroutine(overlay.FadeTo(0f, 0.8f));
        yield return overlay.HideCaption();
        RefreshSeals();
        PlayerLock.Pop();
        Busy = false;
    }

    // Placeholder recap built from story flags; the narrative pass replaces the wording.
    private static string RecapFor(Region region) => region.recap;

    public Transform CurrentSpawn
    {
        get
        {
            var start = RegionStart.Find(Current);
            return start != null ? start.Spawn : null;
        }
    }

    public void MoveToSpawn(string scene)
    {
        var start = RegionStart.Find(scene);
        if (start == null || player == null) return;
        Teleport(start.Spawn.position, start.Spawn.rotation);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        body.enabled = false;
        player.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        body.enabled = true;
    }

    public static void LoadMenu()
    {
        PlayerLock.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MenuScene);
    }
}
