using StarterAssets;
using UnityEngine;

// A box of dangerous ground: unfrozen lake water (drowning) or lava. Deals damage over time
// while the player is inside; drowning also hauls her back to the last safe ground she
// stood on, following the GDD rule that water and lava gate the map without UI borders.
// Uses the box bounds directly rather than physics triggers, so it works with the
// CharacterController regardless of layers.
[RequireComponent(typeof(BoxCollider))]
public class HazardZone : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damagePerSecond = 15f;
    [SerializeField] private ElementType element = ElementType.None;
    [Tooltip("Drowning: after this many seconds inside, return the player to safe ground. 0 = never.")]
    [SerializeField, Min(0f)] private float returnAfter;
    [SerializeField] private string firstTimeLine;
    [Tooltip("Lava: the molten surface mesh. When set, the player is 'inside' only while standing at or below it.")]
    [SerializeField] private MeshCollider surface;

    private static Vector3 lastSafe;
    private static float lastSafeTime;
    private static int occupied;

    private BoxCollider box;
    private Transform player;
    private FirstPersonController controller;
    private float inside;
    private bool warned;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void Start()
    {
        // The player wades into lava rather than walking on top of it.
        var body = FindFirstObjectByType<CharacterController>();
        if (surface != null && body != null) Physics.IgnoreCollision(body, surface);
    }

    private bool BelowSurface() =>
        surface == null || surface.Raycast(new Ray(player.position + Vector3.up * 4f, Vector3.down), out _, 4.6f);

    private void Update()
    {
        if (player == null)
        {
            controller = FindFirstObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }
        bool isInside = box.bounds.Contains(player.position) && BelowSurface();
        if (!isInside)
        {
            if (inside > 0f) occupied--;
            inside = 0f;
            // Remember solid ground, only from one zone per frame and only when no zone has the player.
            if (occupied == 0 && controller.Grounded && Time.time - lastSafeTime > 0.5f)
            {
                lastSafe = player.position;
                lastSafeTime = Time.time;
            }
            return;
        }
        if (inside == 0f) occupied++;
        inside += Time.deltaTime;
        var health = PlayerHealth.Instance;
        if (health != null) health.TakeDamage(damagePerSecond * Time.deltaTime, element);
        if (!warned && !string.IsNullOrEmpty(firstTimeLine)) { warned = true; Subtitle.Show(firstTimeLine); }
        if (returnAfter > 0f && inside >= returnAfter && lastSafeTime > 0f && RegionManager.Instance != null)
        {
            RegionManager.Instance.Teleport(lastSafe, player.rotation);
            occupied--;
            inside = 0f;
        }
    }
}
