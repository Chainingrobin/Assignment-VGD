using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// The starting menu scene: New Game, chapter select in narrative order, New Game+ once an
// ending has been reached, and Quit. Behind it, the real World scene loads as a living backdrop
// and a drone camera drifts over the forest, the frozen lake and the volcano.
public class MainMenu : MonoBehaviour
{
    private static readonly (string scene, string number, string name)[] Chapters =
    {
        ("Forest", "I", "The Waking Storm"),
        ("Ice", "II", "What the Cold Keeps"),
        ("Volcano", "III", "The Price of Fire"),
        ("FinalArena", "IV", "The Sanctum"),
    };

    // Drone path over the world: (camera position, point it looks at).
    private static readonly (Vector3 position, Vector3 look)[] Flight =
    {
        (new Vector3(610f, 70f, 700f), new Vector3(560f, 10f, 560f)),
        (new Vector3(520f, 75f, 500f), new Vector3(640f, 15f, 330f)),
        (new Vector3(700f, 85f, 400f), new Vector3(650f, 20f, 250f)),
        (new Vector3(690f, 110f, 560f), new Vector3(795f, 60f, 642f)),
    };
    private const float SecondsPerLeg = 18f;

    private RectTransform mainPanel, chapterPanel;
    private CanvasGroup cover;
    private Camera droneCamera;
    private float flightTime;
    private bool worldReady;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Arriving from an ending or the pause menu: clear any fade left on the persistent overlay.
        var overlay = TransitionOverlay.Instance;
        overlay.StartCoroutine(overlay.FadeTo(0f, 1f));
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        droneCamera = Camera.main;
        GameAudio.Mood("Menu");
        BuildUI();
        StartCoroutine(LoadBackdrop());
    }

    private IEnumerator LoadBackdrop()
    {
        RegionManager.MenuPreview = true;
        yield return SceneManager.LoadSceneAsync(RegionManager.WorldScene, LoadSceneMode.Additive);
        yield return null;
        if (droneCamera != null)
        {
            droneCamera.clearFlags = CameraClearFlags.Skybox;
            droneCamera.farClipPlane = 2500f;
            var data = droneCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = true;
        }
        worldReady = true;
        for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
        {
            cover.alpha = 1f - t / 1.5f;
            yield return null;
        }
        cover.alpha = 0f;
    }

    private void Update()
    {
        if (!worldReady || droneCamera == null) return;
        flightTime += Time.deltaTime / SecondsPerLeg;
        int leg = Mathf.FloorToInt(flightTime) % Flight.Length;
        float t = Mathf.SmoothStep(0f, 1f, flightTime - Mathf.Floor(flightTime));
        Vector3 Point(int i) => Flight[(i + Flight.Length) % Flight.Length].position;
        // Catmull-Rom through the waypoints keeps the drone's path smooth through each turn.
        var position = 0.5f * (2f * Point(leg) + (-Point(leg - 1) + Point(leg + 1)) * t
            + (2f * Point(leg - 1) - 5f * Point(leg) + 4f * Point(leg + 1) - Point(leg + 2)) * t * t
            + (-Point(leg - 1) + 3f * Point(leg) - 3f * Point(leg + 1) + Point(leg + 2)) * t * t * t);
        var look = Vector3.Lerp(Flight[leg].look, Flight[(leg + 1) % Flight.Length].look, t);
        droneCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(droneCamera.transform.rotation,
            Quaternion.LookRotation(look - position), 1f - Mathf.Exp(-2f * Time.deltaTime)));
    }

    private void BuildUI()
    {
        var canvas = UIKit.Canvas("MainMenu", 0);
        var root = canvas.transform;
        // Opaque until the world backdrop has loaded, then a left-side shade keeps text readable.
        var coverRect = UIKit.Stretch(root, "Cover");
        UIKit.Fill(coverRect, new Color(0.05f, 0.035f, 0.03f));
        cover = coverRect.gameObject.AddComponent<CanvasGroup>();
        cover.blocksRaycasts = false;
        var shade = UIKit.Rect(root, "Shade", new Vector2(760f, 0f), Vector2.zero, new Vector2(0f, 0.5f));
        shade.anchorMin = new Vector2(0f, 0f);
        shade.anchorMax = new Vector2(0f, 1f);
        shade.sizeDelta = new Vector2(760f, 0f);
        UIKit.Fill(shade, new Color(0.03f, 0.02f, 0.015f, 0.72f));

        var column = UIKit.Rect(root, "Column", new Vector2(620f, 980f), new Vector2(110f, 0f), new Vector2(0f, 0.5f));
        var top = new Vector2(0.5f, 1f);
        var title = UIKit.Text(column, "Title", "WULANG", 112f, new Vector2(620f, 130f), new Vector2(0f, -60f), top,
            FantasyUI.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        title.outlineWidth = 0.12f;
        title.outlineColor = new Color32(40, 22, 8, 255);
        var divider = UIKit.Rect(column, "Divider", new Vector2(420f, 24f), new Vector2(0f, -196f), top).gameObject.AddComponent<UnityEngine.UI.Image>();
        divider.sprite = FantasyUI.Divider;
        divider.raycastTarget = false;
        var subtitle = UIKit.Text(column, "Subtitle", "Elements of the Past", 34f, new Vector2(620f, 44f), new Vector2(0f, -226f), top,
            FantasyUI.Parchment, TextAlignmentOptions.Center);
        subtitle.fontStyle = FontStyles.SmallCaps;

        // The four elements as an emblem row: the game's whole progression at a glance.
        var elements = new[] { ElementType.Lightning, ElementType.Earth, ElementType.Ice, ElementType.Fire };
        for (int i = 0; i < elements.Length; i++)
        {
            var x = (i - 1.5f) * 74f;
            var socket = UIKit.Rect(column, elements[i] + " socket", new Vector2(56f, 56f), new Vector2(x, -294f), top).gameObject.AddComponent<UnityEngine.UI.Image>();
            socket.sprite = FantasyUI.Socket;
            socket.raycastTarget = false;
            var icon = UIKit.Rect(socket.transform, "Icon", new Vector2(28f, 28f), Vector2.zero, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<ElementIcon>();
            icon.Element = elements[i];
            icon.color = ElementVisuals.ColorFor(elements[i]);
            icon.raycastTarget = false;
        }

        var center = new Vector2(0.5f, 1f);
        mainPanel = UIKit.Rect(column, "Main", new Vector2(420f, 400f), new Vector2(0f, -400f), center);
        float y = 0f;
        if (StoryStateManager.HasSave) Button(mainPanel, "Continue", ref y, Continue);
        Button(mainPanel, "New Game", ref y, () => StartAt("Forest", false));
        Button(mainPanel, "Chapters", ref y, () => ShowChapters(true));
        if (StoryStateManager.NewGamePlusUnlocked) Button(mainPanel, "New Game+", ref y, () => StartAt("Forest", true));
        // Story mode is a player option (not a debug switch): enemies hit half as hard, for players who are here
        // for the story more than the fights. The choice is remembered, and a line under it says what it does.
        TMP_Text storyMode = null;
        storyMode = UIKit.Button(mainPanel, StoryModeLabel(), new Vector2(400f, 62f), new Vector2(0f, y), new Vector2(0.5f, 1f), () =>
        {
            PlayerHealth.StoryMode = !PlayerHealth.StoryMode;
            storyMode.text = StoryModeLabel();
        }).GetComponentInChildren<TMP_Text>();
        UIKit.Text(mainPanel, "Story mode help", "Easier fights: enemies deal half damage.", 18f, new Vector2(400f, 24f),
            new Vector2(0f, y - 64f), new Vector2(0.5f, 1f), FantasyUI.Muted, TextAlignmentOptions.Center);
        y -= 104f;

        Button(mainPanel, "Quit", ref y, Quit);

        chapterPanel = UIKit.Rect(column, "Chapters", new Vector2(520f, 460f), new Vector2(0f, -380f), center);
        UIKit.Text(chapterPanel, "Header", "Chapters", 26f, new Vector2(520f, 36f), Vector2.zero, center,
            FantasyUI.Gold, TextAlignmentOptions.Center).fontStyle = FontStyles.SmallCaps;
        y = -48f;
        foreach (var chapter in Chapters)
        {
            var scene = chapter.scene;
            var button = UIKit.Button(chapterPanel, $"<color=#E8C77F>{chapter.number}</color>   {chapter.name}", new Vector2(500f, 60f), new Vector2(0f, y), center,
                () => StartAt(scene, false));
            button.GetComponentInChildren<TMP_Text>().fontSize = 24f;
            y -= 72f;
        }
        UIKit.Button(chapterPanel, "Back", new Vector2(260f, 52f), new Vector2(0f, y - 8f), center, () => ShowChapters(false));
        ShowChapters(false);

        UIKit.Text(root, "Footer", "A prototype for GUC  DMET 909  -  Youssef Yasser", 16f, new Vector2(900f, 24f), new Vector2(40f, 28f),
            Vector2.zero, FantasyUI.Muted);
    }

    private static string StoryModeLabel() => PlayerHealth.StoryMode ? "Story Mode: On" : "Story Mode: Off";

    private static void Button(RectTransform parent, string label, ref float y, System.Action onClick)
    {
        UIKit.Button(parent, label, new Vector2(400f, 62f), new Vector2(0f, y), new Vector2(0.5f, 1f), onClick);
        y -= 76f;
    }

    private void ShowChapters(bool show)
    {
        mainPanel.gameObject.SetActive(!show);
        chapterPanel.gameObject.SetActive(show);
    }

    private static void StartAt(string region, bool newGamePlus)
    {
        var story = StoryStateManager.Instance;
        story.ClearAll();
        story.NewGamePlus = newGamePlus;
        if (newGamePlus)
            foreach (ElementType element in System.Enum.GetValues(typeof(ElementType)))
                if (element != ElementType.None) story.SetFlag(Flags.Has(element));
        Launch(region);
    }

    // Back to the start of the region last entered, with the story as it was then.
    private static void Continue()
    {
        var region = StoryStateManager.Instance.Load();
        Launch(System.Array.Exists(Chapters, c => c.scene == region) ? region : "Forest");
    }

    private static void Launch(string region)
    {
        RegionManager.MenuPreview = false;
        RegionManager.PendingRegion = region;
        SceneManager.LoadScene(RegionManager.WorldScene);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
