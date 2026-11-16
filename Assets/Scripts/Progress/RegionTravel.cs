using UnityEngine;

// A scripted level exit (the Ice region's mountain-pass waystone, the volcano's rift).
// Pressing F on it starts the region transition; a FlagGate on its Interactable keeps it
// unusable until the guardian is beaten, so the exit is conditioned on story state.
[RequireComponent(typeof(Interactable))]
public class RegionTravel : MonoBehaviour
{
    [SerializeField] private string region;

    private void Awake() => GetComponent<Interactable>().OnInteract.AddListener(Travel);

    public void Travel()
    {
        if (RegionManager.Instance != null) RegionManager.Instance.EnterRegion(region);
    }
}
