using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The objective tracker: shows the current story objective in the top-right of the HUD and raises a pale light beam
/// over it in the world (assignment guide, Step 6; GDD section 5, "objective tracker = plot progression").
/// </summary>
/// <remarks>
/// <para><b>Always derived, never stored.</b> Each region scene lists its objectives (<see cref="RegionObjectives"/>).
/// The current one is recomputed from story flags whenever a flag is set or a region is entered: the first objective
/// whose <c>showWhen</c> flag is set and whose <c>doneWhen</c> flag isn't. Nothing can get out of step, so the tracker
/// always matches the story state, even if the player explores out of order (Step 11).</para>
/// <para><b>Feedback.</b> When the objective changes, a gold "New objective" header and a glow appear for a few seconds,
/// so the player notices the story has moved on. The beam stands over the goal, visible from across the map by day or
/// night, and fades once the goal is within <see cref="ArrivedDistance"/> or the player is in a cutscene. There is no
/// HUD compass: the world itself shows the way.</para>
/// <para>Minimal and icon-first: the scroll icon already says "objective", so only the goal itself is written.</para>
/// </remarks>
public class ObjectiveManager : MonoBehaviour
{
    private TMP_Text text, header;
    private CanvasGroup flash;
    private RectTransform panel;
    [Tooltip("Unlit beam material for the light over the current goal.")]
    [SerializeField] private Material beaconMaterial;
    private string shown;
    private Transform beacon;
    private Vector3 marker;
    // Close enough to see the goal; also keeps the marker off the boss bar during a fight.
    private const float ArrivedDistance = 30f;

    private void Start()
    {
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        // Frameless, top right: the scroll icon says "objective", so only the goal itself is written.
        // "New objective" appears above it briefly when the story moves it on.
        var corner = Vector2.one;
        panel = UIKit.Rect(hud, "ObjectiveTracker", new Vector2(380f, 60f), new Vector2(-32f, -30f), corner);
        UIKit.Backing(panel);
        var glow = UIKit.Stretch(panel, "UpdatedGlow");
        var shine = UIKit.Fill(glow, new Color(1f, 0.85f, 0.45f, 0.22f));
        shine.sprite = FantasyUI.Soft;
        shine.type = UnityEngine.UI.Image.Type.Sliced;
        flash = glow.gameObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        HUDIcon.Create(panel, HUDIcon.Shape.Quest, 24f, new Vector2(0f, -2f), new Vector2(0f, 1f));
        header = UIKit.Text(panel, "Header", "New objective", 15f, new Vector2(340f, 20f), new Vector2(0f, 22f), corner,
            FantasyUI.Gold, TextAlignmentOptions.BottomRight);
        header.fontStyle = FontStyles.SmallCaps;
        header.gameObject.SetActive(false);
        text = UIKit.Text(panel, "Text", "", 21f, new Vector2(340f, 56f), Vector2.zero, corner,
            FantasyUI.Parchment, TextAlignmentOptions.TopRight);
        text.textWrappingMode = TextWrappingModes.Normal;
        if (beaconMaterial != null)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Objective beacon";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.localScale = new Vector3(2.2f, 90f, 2.2f);
            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = beaconMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            beacon = beam.transform;
            beacon.gameObject.SetActive(false);
        }
        StoryStateManager.Instance.OnFlagSet += OnFlag;
        if (RegionManager.Instance != null) RegionManager.Instance.RegionEntered += OnRegion;
        Refresh();
    }

    private void OnDestroy()
    {
        if (beacon != null) Destroy(beacon.gameObject);

        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
        if (RegionManager.Instance != null) RegionManager.Instance.RegionEntered -= OnRegion;
    }

    // Any new flag or region can change which objective is current.
    private void OnFlag(string flag) => Refresh();
    private void OnRegion(string region) => Refresh();

    // Fades the "updated" glow and keeps the beam standing over the current goal while it is far away.
    private void Update()
    {
        if (flash != null && flash.alpha > 0f) flash.alpha = Mathf.MoveTowards(flash.alpha, 0f, Time.deltaTime * 0.8f);
        if (beacon == null) return;
        var view = Camera.main != null ? Camera.main.transform : null;
        var to = view != null ? Vector3.ProjectOnPlane(marker - view.position, Vector3.up) : Vector3.zero;
        bool show = marker != Vector3.zero && view != null && !PlayerLock.IsLocked && to.magnitude > ArrivedDistance;
        if (beacon.gameObject.activeSelf != show) beacon.gameObject.SetActive(show);
        if (show) beacon.position = marker + Vector3.up * (beacon.localScale.y - 2f);
    }

    /// <summary>
    /// Recomputes the current objective from the story flags and updates the tracker. Announces "New objective" when
    /// it changes (but not for the first objective of a session). Safe to call at any time.
    /// </summary>
    public void Refresh()
    {
        if (text == null) return;
        var objective = Current();
        var current = objective != null ? objective.text : "";
        marker = objective != null ? objective.marker : Vector3.zero;
        panel.gameObject.SetActive(!string.IsNullOrEmpty(current));
        if (current == shown) return;
        bool changed = !string.IsNullOrEmpty(shown);
        shown = current;
        text.text = current;
        // Fit the panel to the objective's lines.
        float height = Mathf.Max(28f, text.GetPreferredValues(current, 340f, 0f).y);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
        text.rectTransform.sizeDelta = new Vector2(340f, height);
        if (changed && !string.IsNullOrEmpty(current))
        {
            flash.alpha = 1f;
            StopAllCoroutines();
            StartCoroutine(AnnounceNew());
        }
    }

    // Shows the "New objective" header for three seconds.
    private IEnumerator AnnounceNew()
    {
        header.gameObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        header.gameObject.SetActive(false);
    }

    // The current region's first objective that is revealed and not yet completed, or null when there is none.
    // Only the region the player is in counts, so a still-unloading region's list never leaks into the tracker.
    private static RegionObjectives.Objective Current()
    {
        var story = StoryStateManager.Instance;
        var region = RegionManager.Instance != null ? RegionManager.Instance.Current : null;
        foreach (var list in FindObjectsByType<RegionObjectives>(FindObjectsSortMode.None))
        {
            if (region != null && list.gameObject.scene.name != region) continue;
            foreach (var objective in list.Objectives)
                if (AnySet(story, objective.showWhen, true) && !AnySet(story, objective.doneWhen, false))
                    return objective;
        }
        return null;
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
