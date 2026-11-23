using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Full-screen layer above the HUD used by every transition: a fade (black or white),
// cinematic letterbox bars, and a chapter caption (title, recap line, progress) shown like
// film subtitles over region-transition cutscenes. Survives scene loads.
public class TransitionOverlay : MonoBehaviour
{
    private static TransitionOverlay instance;
    private CanvasGroup fadeGroup;
    private UnityEngine.UI.Image fade;
    private CanvasGroup captionGroup;
    private RectTransform topBar, bottomBar;
    private TMP_Text title, recap, progress;
    private RectTransform skipHint;
    private int skipHolders;

    // Space skips any cutscene, caption or ending card (the assignment guide: always let the player skip).
    public static bool SkipPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

    // Cutscenes call this while they play; the hint shows while anyone needs it.
    public void ShowSkipHint(bool show)
    {
        skipHolders = Mathf.Max(0, skipHolders + (show ? 1 : -1));
        skipHint.gameObject.SetActive(skipHolders > 0);
    }

    public static TransitionOverlay Instance
    {
        get
        {
            if (instance == null) instance = Build();
            return instance;
        }
    }

    private static TransitionOverlay Build()
    {
        var canvas = UIKit.Canvas("TransitionOverlay", 500, true);
        var overlay = canvas.gameObject.AddComponent<TransitionOverlay>();
        var root = canvas.transform;

        var fadeRect = UIKit.Stretch(root, "Fade");
        overlay.fade = UIKit.Fill(fadeRect, Color.black);
        overlay.fadeGroup = fadeRect.gameObject.AddComponent<CanvasGroup>();
        overlay.fadeGroup.alpha = 0f;
        overlay.fadeGroup.blocksRaycasts = false;

        var captionRoot = UIKit.Stretch(root, "Caption");
        overlay.captionGroup = captionRoot.gameObject.AddComponent<CanvasGroup>();
        overlay.captionGroup.alpha = 0f;
        overlay.captionGroup.blocksRaycasts = false;
        overlay.topBar = UIKit.Rect(captionRoot, "Letterbox Top", new Vector2(0f, 0f), Vector2.zero, new Vector2(0.5f, 1f));
        overlay.bottomBar = UIKit.Rect(captionRoot, "Letterbox Bottom", new Vector2(0f, 0f), Vector2.zero, new Vector2(0.5f, 0f));
        foreach (var bar in new[] { overlay.topBar, overlay.bottomBar })
        {
            bar.anchorMin = new Vector2(0f, bar.anchorMin.y);
            bar.anchorMax = new Vector2(1f, bar.anchorMax.y);
            UIKit.Fill(bar, Color.black);
        }
        var center = new Vector2(0.5f, 0f);
        overlay.title = UIKit.Text(captionRoot, "Title", "", 52f, new Vector2(1400f, 70f), new Vector2(0f, 150f), center,
            FantasyUI.Gold, TextAlignmentOptions.Center);
        overlay.title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        overlay.recap = UIKit.Text(captionRoot, "Recap", "", 26f, new Vector2(1300f, 70f), new Vector2(0f, 90f), center,
            FantasyUI.Parchment, TextAlignmentOptions.Top);
        overlay.recap.textWrappingMode = TextWrappingModes.Normal;
        overlay.progress = UIKit.Text(captionRoot, "Progress", "", 18f, new Vector2(400f, 30f), new Vector2(0f, 30f), center,
            FantasyUI.Muted, TextAlignmentOptions.Center);
        overlay.skipHint = UIKit.Hint(root, "Space", "Skip", new Vector2(-40f, 40f), new Vector2(1f, 0f), 20f);
        overlay.skipHint.gameObject.SetActive(false);
        overlay.SetBars(0f);
        return overlay;
    }

    private void SetBars(float height)
    {
        topBar.sizeDelta = bottomBar.sizeDelta = new Vector2(0f, height);
    }

    public IEnumerator FadeTo(float alpha, float duration, Color? color = null)
    {
        if (color.HasValue) fade.color = color.Value;
        fadeGroup.blocksRaycasts = alpha > 0f;
        float start = fadeGroup.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            fadeGroup.alpha = Mathf.Lerp(start, alpha, t / duration);
            yield return null;
        }
        fadeGroup.alpha = alpha;
    }

    public IEnumerator ShowCaption(string chapterTitle, string recapLine, float duration = 0.6f)
    {
        title.text = chapterTitle;
        recap.text = recapLine;
        progress.text = "";
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            captionGroup.alpha = k;
            SetBars(110f * k);
            yield return null;
        }
        captionGroup.alpha = 1f;
        SetBars(110f);
    }

    public IEnumerator HideCaption(float duration = 0.6f)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / duration);
            captionGroup.alpha = k;
            SetBars(110f * k);
            yield return null;
        }
        captionGroup.alpha = 0f;
        SetBars(0f);
    }

    // Changes the caption's lines in place, the letterbox staying up: the text fades out and back in.
    public IEnumerator SwapCaption(string chapterTitle, string recapLine, float duration = 0.6f)
    {
        if (captionGroup.alpha < 1f) { yield return ShowCaption(chapterTitle, recapLine, duration); yield break; }
        for (float t = 0f; t < duration * 0.5f; t += Time.unscaledDeltaTime)
        {
            title.alpha = recap.alpha = 1f - t / (duration * 0.5f);
            yield return null;
        }
        title.text = chapterTitle;
        recap.text = recapLine;
        for (float t = 0f; t < duration * 0.5f; t += Time.unscaledDeltaTime)
        {
            title.alpha = recap.alpha = t / (duration * 0.5f);
            yield return null;
        }
        title.alpha = recap.alpha = 1f;
    }

    public void SetProgress(string text) => progress.text = text;
}
