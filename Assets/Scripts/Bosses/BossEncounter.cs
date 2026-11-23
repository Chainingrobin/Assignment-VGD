using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// One guardian fight in its arena. Walking into the arena starts it (optionally removing a
// disguise first: Abe becoming Ganfaul, as a short skippable cutscene) and shows the boss bar. When the boss falls it says
// its last words, drops its element tome and sets the story flag that opens the next region.
// If the player dies mid-fight, the boss resets. A boss already beaten in this playthrough
// stays gone when its region loads again.
public class BossEncounter : MonoBehaviour
{
    [SerializeField] private BossBrain boss;
    [SerializeField] private string displayName = "Guardian";
    [SerializeField] private string defeatFlag;
    [SerializeField] private GameObject rewardPrefab;
    [SerializeField, Min(1f)] private float engageRadius = 22f;
    [Tooltip("Engage automatically on arena entry. Off for the final boss, whose fight starts from the choice.")]
    [SerializeField] private bool autoEngage = true;
    [SerializeField] private GameObject disguise;
    [SerializeField] private GameObject revealEffect;
    [SerializeField, TextArea] private string introLine;
    [SerializeField, TextArea] private string dyingLine;

    private Transform player;
    private bool engaged, finished;

    public BossBrain Boss => boss;
    public bool Finished => finished;
    public event System.Action Defeated;

    // New Game+ refights every boss, so it tracks its own defeat flags.
    private string Flag => StoryStateManager.Instance.NewGamePlus ? defeatFlag + "NG" : defeatFlag;

    private void Start()
    {
        if (StoryStateManager.Instance.HasFlag(Flag) && !string.IsNullOrEmpty(defeatFlag))
        {
            finished = true;
            boss.gameObject.SetActive(false);
            if (disguise != null) disguise.SetActive(false);
            return;
        }
        if (disguise != null) boss.gameObject.SetActive(false);
        if (StoryStateManager.Instance.NewGamePlus)
            boss.GetComponent<CombatTarget>().SetMaxHealthScale(1.5f);
        boss.Target.Died += OnBossDied;
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (boss != null) boss.Target.Died -= OnBossDied;
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.Died -= OnPlayerDied;
    }

    private void Update()
    {
        if (!autoEngage || engaged || finished || PlayerLock.IsLocked) return;
        if (player == null)
        {
            var controller = FindFirstObjectByType<StarterAssets.FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }
        if (Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).magnitude <= engageRadius) Engage();
    }

    public const string RevealSeen = "ganfaulRevealed";

    public void Engage()
    {
        if (engaged || finished) return;
        engaged = true;
        if (disguise != null && disguise.activeSelf) { StartCoroutine(Reveal()); return; }
        boss.gameObject.SetActive(true);
        boss.Engage();
        Begin();
    }

    private void Begin()
    {
        BossHealthBar.Show(boss.Target, displayName);
        if (!string.IsNullOrEmpty(introLine)) Subtitle.Show(introLine, 4f);
        GameAudio.Mood("Boss", 1f);
    }

    // The climax cutscene (assignment Step 7): the camera cuts to Abe, a swell rises, the disguise
    // burns away and Ganfaul stands in his place and roars while the shot pulls back and shakes.
    // Space cuts it short. Sets RevealSeen when it ends.
    private IEnumerator Reveal()
    {
        PlayerLock.Push();
        var overlay = TransitionOverlay.Instance;
        overlay.ShowSkipHint(true);
        var hudRoot = UIKit.HUDRoot();
        var hud = hudRoot != null ? hudRoot.GetComponentInParent<Canvas>() : null;
        if (hud != null) hud.enabled = false;
        var abe = disguise.transform;
        var face = abe.position + Vector3.up * 1.6f;
        var shot = new GameObject("Reveal camera").AddComponent<CinemachineCamera>();
        SceneManager.MoveGameObjectToScene(shot.gameObject, gameObject.scene);
        shot.Priority = 100;
        var close = face + abe.forward * 3.2f + abe.right * 0.8f;
        var wide = abe.position + abe.forward * 9f + abe.right * 2.5f + Vector3.up * 3f;
        shot.transform.SetPositionAndRotation(close, Quaternion.LookRotation(face - close));
        GameAudio.Mood("", 1.5f);
        GameAudio.Play("Sfx/Reveal", abe.position, 1f);
        bool skip = false;
        // Hold on Abe, pushing in slowly as the swell rises.
        for (float t = 0f; t < 2.8f && !skip; t += Time.deltaTime)
        {
            skip = TransitionOverlay.SkipPressed;
            var p = Vector3.Lerp(close, face + abe.forward * 2.4f, t / 2.8f);
            shot.transform.SetPositionAndRotation(p, Quaternion.LookRotation(face - p));
            yield return null;
        }
        if (revealEffect != null)
        {
            var fx = Instantiate(revealEffect, abe.position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(fx, gameObject.scene);
            Destroy(fx, 6f);
        }
        boss.transform.SetPositionAndRotation(abe.position, abe.rotation);
        disguise.SetActive(false);
        boss.gameObject.SetActive(true);
        boss.Engage(); // opens on its roar
        // Pull back to take him in, the frame shaking with the roar.
        for (float t = 0f; t < 3.4f && !skip; t += Time.deltaTime)
        {
            skip = TransitionOverlay.SkipPressed;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 1.2f));
            var look = Vector3.Lerp(face, boss.Target.AimPoint + Vector3.up * 0.8f, k);
            var shake = 0.25f * (1f - t / 3.4f) * new Vector3(Mathf.PerlinNoise(t * 20f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 20f) - 0.5f, 0f);
            var p = Vector3.Lerp(close, wide, k) + shake;
            shot.transform.SetPositionAndRotation(p, Quaternion.LookRotation(look - p));
            yield return null;
        }
        Destroy(shot.gameObject); // the brain blends back to Wulang's view
        overlay.ShowSkipHint(false);
        if (hud != null) hud.enabled = true;
        PlayerLock.Pop();
        StoryStateManager.Instance.SetFlag(RevealSeen);
        Begin();
    }

    private void OnBossDied()
    {
        if (finished) return;
        finished = true;
        BossHealthBar.Hide();
        GameAudio.Play("Sfx/Victory", null, 0.7f);
        GameAudio.Mood(gameObject.scene.name, 3f);
        if (!string.IsNullOrEmpty(dyingLine)) Subtitle.Show(dyingLine, 5f);
        StartCoroutine(Reward());
    }

    private IEnumerator Reward()
    {
        yield return new WaitForSeconds(2.5f);
        if (rewardPrefab != null)
        {
            var drop = Instantiate(rewardPrefab, boss.Target.AimPoint + Vector3.up * 0.5f, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(drop, gameObject.scene);
        }
        // The body would block the memory echo's shot at the arena centre; a reload hides it too.
        boss.gameObject.SetActive(false);
        var story = StoryStateManager.Instance;
        story.SetFlag(Flag);
        story.SetFlag(defeatFlag);
        Defeated?.Invoke();
    }

    private void OnPlayerDied()
    {
        if (!engaged || finished) return;
        engaged = false;
        BossHealthBar.Hide();
        boss.Disengage();
        GameAudio.Mood(gameObject.scene.name, 2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, engageRadius);
    }
}
