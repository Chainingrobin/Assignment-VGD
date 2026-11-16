using UnityEngine;

// Turns objects or components on only while a story flag is set (or, inverted, only while
// it is not). Example: the ice statue's Interactable stays off until the Earth Guardian falls.
public class FlagGate : MonoBehaviour
{
    [SerializeField] private string flag;
    [SerializeField] private bool invert;
    [SerializeField] private GameObject[] objects;
    [SerializeField] private Behaviour[] components;

    private void OnEnable()
    {
        StoryStateManager.Instance.OnFlagSet += OnFlag;
        Apply();
    }

    private void OnDisable()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
    }

    private void OnFlag(string changed) { if (changed == flag) Apply(); }

    private void Apply()
    {
        bool open = StoryStateManager.Instance.HasFlag(flag) != invert;
        foreach (var target in objects) if (target != null) target.SetActive(open);
        foreach (var target in components) if (target != null) target.enabled = open;
    }
}
