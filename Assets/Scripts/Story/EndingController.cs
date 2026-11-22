using System.Collections;
using UnityEngine;

// The final arena's climax and the two endings. Approaching Abe plays the offer (a dialogue
// with a choice). Accepting ends the game in Submission: Ganfaul takes the elements back one
// by one and gives Wulang a false past. Refusing reveals Ganfaul and starts the fight; beating
// him plays the true memory and the Truth ending. Dying twice in the fight brings the offer
// back, Souls-style. Either ending unlocks New Game+ and returns to the main menu.
public class EndingController : MonoBehaviour
{
    public const string RefusedOffer = "refusedOffer";

    [SerializeField] private BossEncounter encounter;
    [SerializeField] private DialogueData offer;
    [SerializeField] private DialogueData reOffer;
    [SerializeField] private DialogueData submissionLines;
    [SerializeField] private MemoryEcho trueMemory;
    [SerializeField] private string submissionTitle = "Ending: [The Gilded Past]";
    [SerializeField, TextArea] private string submissionText = "[Placeholder: Wulang kneels. The past Abe gives her is warm, and false.]";
    [SerializeField] private string truthTitle = "Ending: [Elements of the Past]";
    [SerializeField, TextArea] private string truthText = "[Placeholder: Wulang chooses who she is now. The elements answer only to her.]";
    [SerializeField, Min(500f)] private float fallFloor = 660f;

    private int deaths;
    private bool reOffered, ending;

    private void Start()
    {
        var story = StoryStateManager.Instance;
        story.OnFlagSet += OnFlag;
        encounter.Defeated += () => StartCoroutine(Truth());
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.Died += OnPlayerDied;
            PlayerHealth.Instance.Respawned += OnRespawned;
        }
        // Reloading the arena mid-fight (or New Game+) goes straight to the fight.
        if (story.HasFlag(RefusedOffer) || story.NewGamePlus) StartCoroutine(BeginFight());
    }

    private void OnDestroy()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.Died -= OnPlayerDied;
            PlayerHealth.Instance.Respawned -= OnRespawned;
        }
    }

    // Called by the NarrativeTrigger beside Abe (or directly when re-offering).
    public void Offer() => DialogueManager.Instance.Play(offer);

    private void OnFlag(string flag)
    {
        if (ending) return;
        if (flag == Flags.ChoseSubmission) StartCoroutine(Submission());
        else if (flag == RefusedOffer) StartCoroutine(BeginFight());
    }

    private IEnumerator BeginFight()
    {
        yield return new WaitForSeconds(0.5f);
        encounter.Engage();
    }

    private void OnPlayerDied() { if (!ending && StoryStateManager.Instance.HasFlag(RefusedOffer)) deaths++; }

    private void OnRespawned()
    {
        if (ending || !StoryStateManager.Instance.HasFlag(RefusedOffer)) return;
        // The encounter disengaged on death and doesn't auto-engage here, so restart the fight.
        if (reOffered || deaths < 2 || reOffer == null) { encounter.Engage(); return; }
        reOffered = true;
        // The second fall: Ganfaul offers again. Refusing again restarts the fight.
        DialogueManager.Instance.Play(reOffer, () => { if (!StoryStateManager.Instance.HasFlag(Flags.ChoseSubmission)) encounter.Engage(); });
    }

    private void Update()
    {
        // The arena floats in the sky; falling off brings Wulang back to its centre.
        var player = PlayerHealth.Instance;
        // Only while actually over the arena (the player starts elsewhere until the region places her).
        if (player != null && player.transform.position.y < fallFloor && RegionManager.Instance != null && !RegionManager.Instance.Busy
            && Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude < 80f)
        {
            RegionManager.Instance.MoveToSpawn(gameObject.scene.name);
            player.TakeDamage(10f, ElementType.None);
        }
    }

    private IEnumerator Submission()
    {
        ending = true;
        PlayerLock.Push();
        if (submissionLines != null)
        {
            bool done = false;
            DialogueManager.Instance.Play(submissionLines, () => done = true);
            while (!done) yield return null;
        }
        // The elements drain from the HUD one by one.
        var affinity = PlayerMagicAffinity.Instance;
        foreach (var element in new[] { ElementType.Fire, ElementType.Ice, ElementType.Earth, ElementType.Lightning })
        {
            if (affinity == null) break;
            affinity.Revoke(element);
            Subtitle.Show($"[{element} leaves you.]", 1.4f);
            yield return new WaitForSeconds(1.5f);
        }
        yield return EndCard(submissionTitle, submissionText, Color.white);
    }

    private IEnumerator Truth()
    {
        ending = true;
        StoryStateManager.Instance.SetFlag(Flags.EndingTruth);
        yield return new WaitForSeconds(3f);
        if (trueMemory != null) yield return trueMemory.Play();
        PlayerLock.Push();
        yield return EndCard(truthTitle, truthText, Color.black);
    }

    private IEnumerator EndCard(string title, string text, Color fade)
    {
        var overlay = TransitionOverlay.Instance;
        yield return overlay.FadeTo(1f, 2f, fade);
        yield return overlay.ShowCaption(title, text + "\n\n[Credits placeholder: Youssef Yasser, GUC DMET 909]");
        StoryStateManager.NewGamePlusUnlocked = true;
        overlay.ShowSkipHint(true);
        float until = Time.unscaledTime + 7f;
        yield return null;
        while (Time.unscaledTime < until && !TransitionOverlay.SkipPressed) yield return null;
        overlay.ShowSkipHint(false);
        yield return overlay.HideCaption();
        RegionManager.LoadMenu(); // the menu fades the overlay back in
    }
}
