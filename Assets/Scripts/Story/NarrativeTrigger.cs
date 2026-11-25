using System.Collections;
using UnityEngine;

/// <summary>
/// A story beat placed in a level (assignment guide, Step 4): the narrative trigger that turns "the player did
/// something" into "the story moved on".
/// </summary>
/// <remarks>
/// <para><b>When it fires</b> (<see cref="Mode"/>): the player walks into a zone, presses F on the object (notes,
/// plaques), the region loads, or a story flag is set (e.g. claiming a tome plays its memory).</para>
/// <para><b>What it does</b>, in this order, each part optional: shows a chapter-style caption, replays a
/// <see cref="MemoryEcho"/>, plays a <see cref="DialogueData"/> conversation, shows a one-line subtitle, gives the
/// player an item (a journal page), and finally sets <c>setFlag</c> so objectives, gates and later beats react.</para>
/// <para><b>Fires once, in order.</b> It is skipped unless <c>requiredFlag</c> is set, and its <c>onceFlag</c> is
/// recorded the moment it starts, so a beat never replays when the player walks through again, survives a save and
/// Continue, and can't fire before the beat it depends on (Steps 4 and 11; the guide's "triggers that replay"
/// mistake). Story beats are off in New Game+.</para>
/// <para>Every trigger in the four region scenes is listed with its flags in the GDD's story-beat table (section 6).</para>
/// </remarks>
public class NarrativeTrigger : MonoBehaviour
{
    /// <summary>What makes the beat fire.</summary>
    public enum Mode
    {
        Zone,       // the player walks within radius
        Interact,   // F on this object's Interactable
        OnStart,    // as soon as the region loads (intros)
        OnFlag,     // when watchFlag becomes set (e.g. a tome collected)
    }

    [SerializeField] private Mode mode = Mode.Zone;
    [Tooltip("Zone mode: how close the player must come, in metres.")]
    [SerializeField, Min(0.5f)] private float radius = 8f;
    [Tooltip("OnFlag mode: the flag that fires this beat when it is set.")]
    [SerializeField] private string watchFlag;
    [Tooltip("Story flag that must already be set; empty = no requirement.")]
    [SerializeField] private string requiredFlag;
    [Tooltip("Set when this fires; if already set, it never fires again.")]
    [SerializeField] private string onceFlag;
    [SerializeField] private float delay;
    [Header("What happens")]
    [SerializeField] private string captionTitle;
    [SerializeField, TextArea] private string captionBody;
    [SerializeField] private MemoryEcho memory;
    [SerializeField] private DialogueData dialogue;
    [SerializeField, TextArea] private string subtitle;
    [Tooltip("Item added to the satchel or journal (found notes, memories).")]
    [SerializeField] private ItemData giveItem;
    [Tooltip("Story flag set when the beat finishes, e.g. metAbe.")]
    [SerializeField] private string setFlag;

    private Transform player;
    private bool firing;

    // Hooks up the non-zone modes; zone mode is polled in Update.
    private void Start()
    {
        var story = StoryStateManager.Instance;
        if (mode == Mode.OnStart) StartCoroutine(Fire());
        if (mode == Mode.OnFlag) story.OnFlagSet += OnFlag;
        if (mode == Mode.Interact && TryGetComponent<Interactable>(out var interactable))
            interactable.OnInteract.AddListener(() => StartCoroutine(Fire()));
    }

    private void OnDestroy()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
    }

    private void OnFlag(string flag) { if (flag == watchFlag && isActiveAndEnabled) StartCoroutine(Fire()); }

    // Zone mode: a distance check instead of a trigger collider, so a beat can't be missed by moving fast or
    // spawning inside it. Ignored while the player is locked in a cutscene or conversation.
    private void Update()
    {
        if (mode != Mode.Zone || firing || PlayerLock.IsLocked) return;
        if (player == null)
        {
            var controller = FindFirstObjectByType<StarterAssets.FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }
        if ((player.position - transform.position).sqrMagnitude <= radius * radius) StartCoroutine(Fire());
    }

    // True when the story allows this beat now: not already running, not New Game+, its requirement met, not yet fired.
    private bool Ready
    {
        get
        {
            var story = StoryStateManager.Instance;
            return !firing && !story.NewGamePlus && story.HasFlag(requiredFlag) && (string.IsNullOrEmpty(onceFlag) || !story.HasFlag(onceFlag));
        }
    }

    /// <summary>
    /// Plays the beat if the story allows it (see remarks). Public so cutscenes and other scripts can fire a beat
    /// directly; calling it when the beat isn't ready does nothing.
    /// </summary>
    public IEnumerator Fire()
    {
        if (!Ready) yield break;
        firing = true;
        var story = StoryStateManager.Instance;
        // Marked as fired before anything plays, so a second entry or a save mid-beat can't start it twice.
        story.SetFlag(onceFlag);
        if (delay > 0f) yield return new WaitForSeconds(delay);
        // Wait out anything else holding the screen (a transition, another conversation).
        while (PlayerLock.IsLocked && (DialogueManager.Instance.IsTalking || (RegionManager.Instance != null && RegionManager.Instance.Busy)))
            yield return null;
        if (!string.IsNullOrEmpty(captionTitle))
        {
            PlayerLock.Push();
            // The HUD steps aside for chapter captions so the letterbox reads cleanly.
            var hud = UIKit.HUDRoot() != null ? UIKit.HUDRoot().GetComponentInParent<Canvas>() : null;
            if (hud != null) hud.enabled = false;
            var overlay = TransitionOverlay.Instance;
            yield return overlay.ShowCaption(captionTitle, captionBody);
            overlay.ShowSkipHint(true);
            // Long enough to read (about 20 characters a second), between 2.5 and 7 seconds; Space skips.
            float until = Time.unscaledTime + Mathf.Clamp(captionBody.Length * 0.05f, 2.5f, 7f);
            yield return null;
            while (Time.unscaledTime < until && !TransitionOverlay.SkipPressed) yield return null;
            overlay.ShowSkipHint(false);
            yield return overlay.HideCaption();
            if (hud != null) hud.enabled = true;
            PlayerLock.Pop();
        }
        if (memory != null) yield return memory.Play();
        if (dialogue != null)
        {
            bool done = false;
            DialogueManager.Instance.Play(dialogue, () => done = true);
            while (!done) yield return null;
        }
        if (!string.IsNullOrEmpty(subtitle)) Subtitle.Show(subtitle, 4f);
        if (giveItem != null && PlayerInventory.Instance != null) PlayerInventory.Instance.Add(giveItem);
        // Last, so whatever listens to this flag (objectives, gates, the next beat) reacts after the beat is over.
        story.SetFlag(setFlag);
        firing = false;
    }

    // Shows the zone's radius in the Scene view when the trigger is selected.
    private void OnDrawGizmosSelected()
    {
        if (mode != Mode.Zone) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
