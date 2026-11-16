using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Each region scene lists its objectives (RegionObjectives); this tracker shows the current one
// in the top-right of the HUD. The current objective is always derived from story flags: the
// first one whose showWhen is set and whose doneWhen isn't. So it always matches the story
// state, even if the player explores out of order (assignment guide Step 6).
public class ObjectiveManager : MonoBehaviour
{
    private TMP_Text text, header;
    private CanvasGroup flash;
    private RectTransform panel;
    private string shown;

    private void Start()
    {
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        var corner = Vector2.one;
        panel = UIKit.Rect(hud, "ObjectiveTracker", new Vector2(420f, 96f), new Vector2(-24f, -24f), corner);
        FantasyUI.Panel(panel);
        header = UIKit.Text(panel, "Header", "Objective", 14f, new Vector2(380f, 20f), new Vector2(-20f, -12f), corner,
            FantasyUI.Gold, TextAlignmentOptions.TopRight);
        text = UIKit.Text(panel, "Text", "", 20f, new Vector2(380f, 56f), new Vector2(-20f, -34f), corner,
            FantasyUI.Parchment, TextAlignmentOptions.TopRight);
        text.textWrappingMode = TextWrappingModes.Normal;
        var glow = UIKit.Stretch(panel, "UpdatedGlow");
        UIKit.Fill(glow, new Color(1f, 0.85f, 0.45f, 0.25f));
        flash = glow.gameObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        StoryStateManager.Instance.OnFlagSet += OnFlag;
        if (RegionManager.Instance != null) RegionManager.Instance.RegionEntered += OnRegion;
        // The unlock card sits at the top right too; push it below the tracker.
        var card = hud.Find("ElementUnlockNotification") as RectTransform;
        if (card != null) card.anchoredPosition = new Vector2(card.anchoredPosition.x, -132f);
        Refresh();
    }

    private void OnDestroy()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
        if (RegionManager.Instance != null) RegionManager.Instance.RegionEntered -= OnRegion;
    }

    private void OnFlag(string flag) => Refresh();
    private void OnRegion(string region) => Refresh();

    private void Update()
    {
        if (flash != null && flash.alpha > 0f) flash.alpha = Mathf.MoveTowards(flash.alpha, 0f, Time.deltaTime * 0.8f);
    }

    public void Refresh()
    {
        if (text == null) return;
        var current = Current();
        panel.gameObject.SetActive(!string.IsNullOrEmpty(current));
        if (current == shown) return;
        bool changed = !string.IsNullOrEmpty(shown);
        shown = current;
        text.text = current;
        // Grow the panel to fit long objectives.
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 48f + Mathf.Max(28f, text.GetPreferredValues(current, 380f, 0f).y));
        text.rectTransform.sizeDelta = new Vector2(380f, panel.sizeDelta.y - 40f);
        if (changed && !string.IsNullOrEmpty(current))
        {
            flash.alpha = 1f;
            header.text = "New Objective";
            StopAllCoroutines();
            StartCoroutine(ResetHeader());
        }
    }

    private IEnumerator ResetHeader()
    {
        yield return new WaitForSeconds(3f);
        header.text = "Objective";
    }

    private static string Current()
    {
        var story = StoryStateManager.Instance;
        var region = RegionManager.Instance != null ? RegionManager.Instance.Current : null;
        foreach (var list in FindObjectsByType<RegionObjectives>(FindObjectsSortMode.None))
        {
            if (region != null && list.gameObject.scene.name != region) continue;
            foreach (var objective in list.Objectives)
                if (AnySet(story, objective.showWhen, true) && !AnySet(story, objective.doneWhen, false))
                    return objective.text;
        }
        return "";
    }

    // "a|b" means either flag; an empty condition counts as `whenEmpty`.
    private static bool AnySet(StoryStateManager story, string flags, bool whenEmpty)
    {
        if (string.IsNullOrEmpty(flags)) return whenEmpty;
        foreach (var flag in flags.Split('|'))
            if (story.HasFlag(flag.Trim())) return true;
        return false;
    }
}
