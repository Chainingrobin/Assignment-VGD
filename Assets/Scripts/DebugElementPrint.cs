using UnityEngine;

// TEMPORARY testing script. Delete this once you have real UI showing
// the active element. Attach anywhere alongside PlayerMagicAffinity.
public class DebugElementPrint : MonoBehaviour
{
    private void OnEnable()
    {
        PlayerMagicAffinity.Instance.OnActiveElementChanged += HandleChanged;
    }

    private void OnDisable()
    {
        PlayerMagicAffinity.Instance.OnActiveElementChanged -= HandleChanged;
    }

    private void HandleChanged(ElementType newElement)
    {
        Debug.Log($"Active element switched to: {newElement}");
    }
}
