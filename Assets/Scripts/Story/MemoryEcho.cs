using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

// A memory replayed in the world instead of in a separate scene: the colour drains (a
// desaturating post-process Volume fades in), translucent ghosts of Wulang and Abe appear on
// the spot and act the moment out, and the memory's lines play as dialogue. Each one is added
// to the journal afterwards. The memories are Abe's fabrications, so each echo can carry a
// small glitch the player may notice (a twist clue for the narrative pass).
// The camera cuts to a framed two-shot of the ghosts for the whole memory, so wherever the
// player happened to be looking, the scene is in view; it blends back to the player after.
public class MemoryEcho : MonoBehaviour
{
    [SerializeField] private GameObject actors;
    [SerializeField] private DialogueData lines;
    [SerializeField] private Volume volume;
    [SerializeField] private ItemData journalEntry;
    [SerializeField] private string title = "[A Memory Returns]";
    [SerializeField, Min(0.1f)] private float fade = 1.2f;

    public bool Playing { get; private set; }

    private void Awake()
    {
        if (actors != null) actors.SetActive(false);
        if (volume != null) volume.weight = 0f;
    }

    public IEnumerator Play()
    {
        Playing = true;
        PlayerLock.Push();
        var shot = FrameActors();
        var overlay = TransitionOverlay.Instance;
        StartCoroutine(overlay.ShowCaption(title, ""));
        if (actors != null) actors.SetActive(true);
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            if (volume != null) volume.weight = t / fade;
            yield return null;
        }
        if (volume != null) volume.weight = 1f;
        yield return overlay.HideCaption(0.4f);
        bool done = false;
        DialogueManager.Instance.Play(lines, () => done = true);
        while (!done) yield return null;
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            if (volume != null) volume.weight = 1f - t / fade;
            yield return null;
        }
        if (volume != null) volume.weight = 0f;
        if (actors != null) actors.SetActive(false);
        if (shot != null) Destroy(shot.gameObject); // the brain blends back to the player's view
        if (journalEntry != null && PlayerInventory.Instance != null) PlayerInventory.Instance.Add(journalEntry);
        PlayerLock.Pop();
        Playing = false;
    }

    // A side-on two-shot at eye level from the player's side of the scene, high enough to clear the ground.
    private CinemachineCamera FrameActors()
    {
        var player = Camera.main != null ? Camera.main.transform.position : transform.position - transform.forward * 5f;
        var center = transform.position + Vector3.up * 1.3f;
        var side = Vector3.ProjectOnPlane(player - center, Vector3.up);
        side = Vector3.Project(side, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Project(side, Vector3.forward).normalized : Vector3.forward;
        var position = center + side * 4.5f + Vector3.up * 0.3f;
        if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            position.y = Mathf.Max(position.y, hit.point.y + 1.4f);
        var shot = new GameObject("Memory camera").AddComponent<CinemachineCamera>();
        shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position));
        var lens = shot.Lens;
        lens.FieldOfView = 45f;
        shot.Lens = lens;
        shot.Priority = 100;
        return shot;
    }
}
