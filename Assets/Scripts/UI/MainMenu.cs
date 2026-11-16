using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

// The starting menu scene: New Game, chapter select in narrative order, New Game+ once an
// ending has been reached, and Quit. Built in code with the same gilded style as the HUD.
public class MainMenu : MonoBehaviour
{
    private static readonly (string scene, string label)[] Chapters =
    {
        ("Forest", "I  -  [The Forest]"),
        ("Ice", "II  -  [The Frozen Reach]"),
        ("Volcano", "III  -  [The Burning Peak]"),
        ("FinalArena", "IV  -  [The Sanctum]"),
    };

    private RectTransform mainPanel, chapterPanel;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvas = UIKit.Canvas("MainMenu", 0);
        var root = canvas.transform;
        UIKit.Fill(UIKit.Stretch(root, "Background"), new Color(0.05f, 0.035f, 0.03f));
        var center = new Vector2(0.5f, 0.5f);
        var title = UIKit.Text(root, "Title", "WULANG", 120f, new Vector2(1200f, 150f), new Vector2(0f, 300f), center,
            FantasyUI.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        UIKit.Text(root, "Subtitle", "Elements of the Past", 36f, new Vector2(1200f, 60f), new Vector2(0f, 200f), center,
            FantasyUI.Parchment, TextAlignmentOptions.Center);

        mainPanel = UIKit.Rect(root, "Main", new Vector2(420f, 420f), new Vector2(0f, -90f), center);
        float y = 150f;
        UIKit.Button(mainPanel, "New Game", new Vector2(380f, 64f), new Vector2(0f, y), center, () => StartAt("Forest", false));
        UIKit.Button(mainPanel, "Chapters", new Vector2(380f, 64f), new Vector2(0f, y -= 84f), center, () => ShowChapters(true));
        if (StoryStateManager.NewGamePlusUnlocked)
            UIKit.Button(mainPanel, "New Game+", new Vector2(380f, 64f), new Vector2(0f, y -= 84f), center, () => StartAt("Forest", true));
        UIKit.Button(mainPanel, "Quit", new Vector2(380f, 64f), new Vector2(0f, y -= 84f), center, Quit);

        chapterPanel = UIKit.Rect(root, "Chapters", new Vector2(560f, 460f), new Vector2(0f, -110f), center);
        y = 180f;
        foreach (var chapter in Chapters)
        {
            var scene = chapter.scene;
            UIKit.Button(chapterPanel, chapter.label, new Vector2(520f, 64f), new Vector2(0f, y), center, () => StartAt(scene, false));
            y -= 80f;
        }
        UIKit.Button(chapterPanel, "Back", new Vector2(300f, 56f), new Vector2(0f, y - 10f), center, () => ShowChapters(false));
        ShowChapters(false);
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
