using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// A memory replayed in the world instead of in a separate scene: the colour drains (a
// desaturating post-process Volume fades in), translucent ghosts of Wulang and Abe appear on
// the spot and act the moment out, and the memory's lines play as dialogue. Each one is added
// to the journal afterwards. The memories are Abe's fabrications, so each echo can carry a
// small glitch the player may notice (a twist clue for the narrative pass).
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
        if (journalEntry != null && PlayerInventory.Instance != null) PlayerInventory.Instance.Add(journalEntry);
        PlayerLock.Pop();
        Playing = false;
    }
}
