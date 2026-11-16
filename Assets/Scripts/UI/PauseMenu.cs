using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Esc pauses the game: Resume, Restart Region (back to the region's start), Main Menu or Quit.
// Ignored while a shop, inventory or conversation already owns the screen.
public class PauseMenu : MonoBehaviour
{
    private Canvas canvas;
    private int lastLockedFrame;

    private void Start()
    {
        canvas = UIKit.Canvas("Pause", 450);
        canvas.transform.SetParent(transform, false);
        var center = new Vector2(0.5f, 0.5f);
        UIKit.Fill(UIKit.Stretch(canvas.transform, "Dim"), new Color(0f, 0f, 0f, 0.6f));
        var panel = UIKit.Rect(canvas.transform, "Panel", new Vector2(460f, 470f), Vector2.zero, center);
        FantasyUI.Panel(panel);
        UIKit.Text(panel, "Title", "Paused", 40f, new Vector2(400f, 50f), new Vector2(0f, -24f), new Vector2(0.5f, 1f),
            FantasyUI.Gold, TMPro.TextAlignmentOptions.Center);
        float y = 110f;
        UIKit.Button(panel, "Resume", new Vector2(360f, 60f), new Vector2(0f, y), center, Resume);
        UIKit.Button(panel, "Restart Region", new Vector2(360f, 60f), new Vector2(0f, y -= 76f), center, Restart);
        UIKit.Button(panel, "Main Menu", new Vector2(360f, 60f), new Vector2(0f, y -= 76f), center, Menu);
        UIKit.Button(panel, "Quit", new Vector2(360f, 60f), new Vector2(0f, y -= 76f), center, Quit);
        canvas.enabled = false;
    }

    private void Update()
    {
        // Esc that just closed a shop or conversation must not also open the pause menu.
        if (PlayerLock.IsLocked && !canvas.enabled) lastLockedFrame = Time.frameCount;
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (canvas.enabled) Resume();
        else if (!PlayerLock.IsLocked && Time.frameCount - lastLockedFrame > 1) Pause();
    }

    private void Pause()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            DontDestroyOnLoad(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));
        canvas.enabled = true;
        Time.timeScale = 0f;
        PlayerLock.Push(showCursor: true);
    }

    private void Resume()
    {
        if (!canvas.enabled) return;
        canvas.enabled = false;
        Time.timeScale = 1f;
        PlayerLock.Pop(showCursor: true);
    }

    private void Restart()
    {
        Resume();
        if (RegionManager.Instance != null) RegionManager.Instance.RestartRegion();
    }

    private void Menu()
    {
        Time.timeScale = 1f;
        RegionManager.LoadMenu();
    }

    private void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
