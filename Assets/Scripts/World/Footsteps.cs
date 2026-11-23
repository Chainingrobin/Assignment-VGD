using StarterAssets;
using UnityEngine;

// Wulang's footsteps: one step sound every stride of ground covered while grounded, and a thump on
// landing. Silent during cutscenes and dialogue, and after a teleport (respawn, region spawn).
[RequireComponent(typeof(FirstPersonController))]
public class Footsteps : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float stride = 2.2f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

    private FirstPersonController controller;
    private Vector3 last;
    private float travelled;
    private bool wasGrounded = true;

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        last = transform.position;
    }

    private void Update()
    {
        float moved = Vector3.ProjectOnPlane(transform.position - last, Vector3.up).magnitude;
        last = transform.position;
        bool grounded = controller.Grounded;
        if (grounded && !wasGrounded && !PlayerLock.IsLocked) GameAudio.Play("Sfx/Land", null, volume * 1.3f);
        wasGrounded = grounded;
        if (!grounded || PlayerLock.IsLocked || moved > 3f) return; // a jump of more than 3 m in a frame is a teleport
        travelled += moved;
        if (travelled < stride) return;
        travelled = 0f;
        GameAudio.Play("Sfx/Footstep", null, volume);
    }
}
