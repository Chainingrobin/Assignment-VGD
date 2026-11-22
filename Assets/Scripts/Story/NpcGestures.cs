using UnityEngine;

// Brings a seated NPC to life in conversation. His own lines get a talking gesture (an upper-body
// clip on the Animator's "Gestures" layer, so his legs keep the sitting pose); while someone else
// speaks he sometimes nods along; and whenever the player is close he turns his head to them.
[RequireComponent(typeof(Animator))]
public class NpcGestures : MonoBehaviour
{
    [Tooltip("Lines whose speaker contains this are his.")]
    [SerializeField] private string speaker = "Abe";
    [Tooltip("Gestures layer states played on his own lines.")]
    [SerializeField] private string[] speaking =
        { "happy hand gesture", "dismissing gesture", "being cocky", "thoughtful head shake", "look away gesture", "relieved sigh" };
    [Tooltip("Played now and then while someone else speaks.")]
    [SerializeField] private string[] listening = { "head nod yes", "acknowledging", "lengthy head nod" };
    [SerializeField, Min(1f)] private float lookRange = 7f;

    private const int GestureLayer = 1;
    private Animator animator;
    private Transform view;
    private float look;

    private DialogueManager dialogue;

    private void Awake() => animator = GetComponent<Animator>();

    private void OnEnable()
    {
        dialogue = DialogueManager.Instance;
        dialogue.LineStarted += OnLine;
    }

    // The cached reference: asking for Instance while the game shuts down would create a new one.
    private void OnDisable()
    {
        if (dialogue != null) dialogue.LineStarted -= OnLine;
    }

    private bool PlayerNear
    {
        get
        {
            if (view == null && Camera.main != null) view = Camera.main.transform;
            return view != null && (view.position - transform.position).sqrMagnitude < lookRange * lookRange;
        }
    }

    private void OnLine(DialogueData.Line line)
    {
        if (!PlayerNear || animator.layerCount <= GestureLayer) return;
        bool mine = line.speaker.Contains(speaker);
        if (!mine && Random.value < 0.5f) return;
        var pool = mine ? speaking : listening;
        animator.CrossFadeInFixedTime(pool[Random.Range(0, pool.Length)], 0.25f, GestureLayer);
    }

    // Head (and a little of the chest) turns toward the player's eyes.
    private void OnAnimatorIK(int layer)
    {
        if (layer != 0) return;
        look = Mathf.MoveTowards(look, PlayerNear ? 1f : 0f, Time.deltaTime * 2f);
        if (look <= 0f) return;
        animator.SetLookAtWeight(look * 0.85f, look * 0.15f, look * 0.85f, 0f, 0.6f);
        animator.SetLookAtPosition(view.position);
    }
}
