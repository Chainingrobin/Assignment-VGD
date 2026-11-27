using UnityEngine;

/// <summary>
/// A scripted level exit: the Ice region's mountain path and the volcano's rift into the Sanctum.
/// </summary>
/// <remarks>
/// Pressing F on it asks <see cref="RegionManager"/> to load the next chapter (flight, chapter card with recap,
/// asynchronous load). A <see cref="FlagGate"/> on its <c>Interactable</c> keeps it unusable until the region's guardian
/// is beaten, so the exit is conditioned on story state, not on where the player stands (assignment brief, "Level
/// Transition Implementation"; guide Step 8). An exit with a <c>warning</c> (the rift: there is no coming back)
/// shows it once, the first time Wulang comes near while it is open.
/// </remarks>
[RequireComponent(typeof(Interactable))]
public class RegionTravel : MonoBehaviour
{
    [Tooltip("Region scene this exit leads to, e.g. Volcano or FinalArena.")]
    [SerializeField] private string region;
    [Tooltip("Shown once when the player first comes near while the exit is open. Written from the Story Text asset; empty = none.")]
    [SerializeField, TextArea] private string warning;
    [SerializeField, Min(1f)] private float warningRadius = 16f;

    private Interactable interactable;
    private Transform player;
    private bool warned;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        interactable.OnInteract.AddListener(Travel);
    }

    private void Update()
    {
        if (warned || string.IsNullOrEmpty(warning) || !interactable.CanInteract || PlayerLock.IsLocked) return;
        if (player == null)
        {
            var controller = FindFirstObjectByType<StarterAssets.FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }
        if ((player.position - transform.position).sqrMagnitude > warningRadius * warningRadius) return;
        warned = true;
        Subtitle.Show(warning, 6f);
    }

    /// <summary>Starts the transition to <c>region</c>. Ignored while another transition is running.</summary>
    public void Travel()
    {
        if (RegionManager.Instance != null) RegionManager.Instance.EnterRegion(region);
    }
}
