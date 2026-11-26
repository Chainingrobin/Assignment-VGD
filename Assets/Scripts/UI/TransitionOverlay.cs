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

    // card: an ending card, centred on the black screen with room for a paragraph and the credits,
    // instead of a chapter caption on the bottom letterbox.
    public IEnumerator ShowCaption(string chapterTitle, string recapLine, float duration = 0.6f, bool card = false)
    {
        Layout(card);
        HudVisible(false);
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

    private void Layout(bool card)
    {
        var anchor = card ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
        // Chapter captions sit just above the 110 px letterbox, so a two-line recap never runs onto the bar's edge.
        Place(title.rectTransform, anchor, card ? new Vector2(0f, 170f) : new Vector2(0f, 175f), new Vector2(1400f, 70f));
        Place(recap.rectTransform, anchor, card ? new Vector2(0f, -40f) : new Vector2(0f, 115f), card ? new Vector2(1100f, 340f) : new Vector2(1300f, 70f));
        recap.fontSize = card ? 30f : 26f;
        recap.lineSpacing = card ? 12f : 0f;
        // An ending card on a white fade (The Gilded Past) needs dark ink; cream on white can't be read.
        bool onWhite = card && fade.color.grayscale > 0.5f;
        title.color = onWhite ? new Color(0.5f, 0.36f, 0.12f) : FantasyUI.Gold;
        recap.color = onWhite ? new Color(0.24f, 0.17f, 0.1f) : FantasyUI.Parchment;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
        HudVisible(true);
    }

    // The HUD steps aside under the letterbox, as it does for dialogue. A CanvasGroup (not Canvas.enabled,
    // which DialogueManager saves and restores) so the two never fight over it.
    private static void HudVisible(bool visible)
    {
        var root = UIKit.HUDRoot();
        if (root == null) return;
        var canvas = root.GetComponentInParent<Canvas>();
        if (!canvas.TryGetComponent(out CanvasGroup group)) group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
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
