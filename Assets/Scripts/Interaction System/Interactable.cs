using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [SerializeField] string prompt = "Press F to interact";
    [SerializeField] bool singleUse = true;
    [SerializeField] UnityEvent onInteract;

    bool used;

    public string Prompt => prompt;
    public bool CanInteract => !(singleUse && used);

    public void Interact()
    {
        if (!CanInteract) return;
        used = true;

        int n = onInteract.GetPersistentEventCount();
        Debug.Log($"Interactable.Interact called on {name}. Event entries: {n}");
        for (int i = 0; i < n; i++)
            Debug.Log($"  entry {i}: target={onInteract.GetPersistentTarget(i)}  method={onInteract.GetPersistentMethodName(i)}");

        onInteract.Invoke();
    }
}