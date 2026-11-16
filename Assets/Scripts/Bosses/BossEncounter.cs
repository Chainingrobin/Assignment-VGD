using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// One guardian fight in its arena. Walking into the arena starts it (optionally removing a
// disguise first: Abe becoming Ganfaul) and shows the boss bar. When the boss falls it says
// its last words, drops its element tome and sets the story flag that opens the next region.
// If the player dies mid-fight, the boss resets. A boss already beaten in this playthrough
// stays gone when its region loads again.
public class BossEncounter : MonoBehaviour
{
    [SerializeField] private BossBrain boss;
    [SerializeField] private string displayName = "[Guardian]";
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

    public void Engage()
    {
        if (engaged || finished) return;
        engaged = true;
        if (disguise != null && disguise.activeSelf)
        {
            if (revealEffect != null)
            {
                var fx = Instantiate(revealEffect, disguise.transform.position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(fx, gameObject.scene);
                Destroy(fx, 6f);
            }
            boss.transform.SetPositionAndRotation(disguise.transform.position, disguise.transform.rotation);
            disguise.SetActive(false);
        }
        boss.gameObject.SetActive(true);
        boss.Engage();
        BossHealthBar.Show(boss.Target, displayName);
        if (!string.IsNullOrEmpty(introLine)) Subtitle.Show(introLine, 4f);
    }

    private void OnBossDied()
    {
        if (finished) return;
        finished = true;
        BossHealthBar.Hide();
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
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, engageRadius);
    }
}
