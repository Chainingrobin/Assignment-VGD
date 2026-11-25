using UnityEngine;

/// <summary>
/// A scripted level exit: the Ice region's mountain-pass waystone and the volcano's rift into the Sanctum.
/// </summary>
/// <remarks>
/// Pressing F on it asks <see cref="RegionManager"/> to load the next chapter (flight, chapter card with recap,
/// asynchronous load). A <see cref="FlagGate"/> on its <c>Interactable</c> keeps it unusable until the region's guardian
/// is beaten, so the exit is conditioned on story state, not on where the player stands (assignment brief, "Level
/// Transition Implementation"; guide Step 8).
/// </remarks>
[RequireComponent(typeof(Interactable))]
public class RegionTravel : MonoBehaviour
{
    [Tooltip("Region scene this exit leads to, e.g. Volcano or FinalArena.")]
    [SerializeField] private string region;

    private void Awake() => GetComponent<Interactable>().OnInteract.AddListener(Travel);

    /// <summary>Starts the transition to <c>region</c>. Ignored while another transition is running.</summary>
    public void Travel()
    {
        if (RegionManager.Instance != null) RegionManager.Instance.EnterRegion(region);
    }
}
