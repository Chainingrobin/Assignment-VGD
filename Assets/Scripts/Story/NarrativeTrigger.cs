using System.Collections;
using UnityEngine;

// A story beat: when it fires it can show a chapter caption, replay a memory echo, play a
// conversation, say a one-line subtitle, give the player an item and set a story flag.
// It fires once per playthrough (remembered by its onceFlag) and only if requiredFlag is set,
// so beats never replay and can't fire out of order (assignment guide Steps 4 and 11).
public class NarrativeTrigger : MonoBehaviour
{
    public enum Mode
    {
        Zone,       // the player walks within radius
        Interact,   // F on this object's Interactable
        OnStart,    // as soon as the region loads (intros)
        OnFlag,     // when watchFlag becomes set (e.g. a tome collected)
    }

    [SerializeField] private Mode mode = Mode.Zone;
    [SerializeField, Min(0.5f)] private float radius = 8f;
    [SerializeField] private string watchFlag;
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
    [SerializeField] private ItemData giveItem;
    [SerializeField] private string setFlag;

    private Transform player;
    private bool firing;

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

    private bool Ready
    {
        get
        {
            var story = StoryStateManager.Instance;
            return !firing && !story.NewGamePlus && story.HasFlag(requiredFlag) && (string.IsNullOrEmpty(onceFlag) || !story.HasFlag(onceFlag));
        }
    }

    public IEnumerator Fire()
    {
        if (!Ready) yield break;
        firing = true;
        var story = StoryStateManager.Instance;
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
        story.SetFlag(setFlag);
        firing = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (mode != Mode.Zone) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
