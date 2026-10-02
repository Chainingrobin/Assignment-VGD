using UnityEngine;
using StarterAssets;

// Put this on the Model object (the child that has the Animator).
// It reads state from FirstPersonController/CharacterController/StarterAssetsInputs
// on the parent Player object and drives Animator parameters:
//   "IsMoving" (bool)   - true whenever horizontal speed is above movementThreshold
//   "Sprinting" (bool)  - direct copy of the real Shift/sprint input state, not inferred
//   "Grounded" (bool), "FreeFall" (bool), "Jump" (trigger)
// Make sure Apply Root Motion is OFF on the Animator.
[RequireComponent(typeof(Animator))]
public class CharacterAnimationController : MonoBehaviour
{
    [Tooltip("The CharacterController on your Player root object")]
    [SerializeField] private CharacterController controller;
    [Tooltip("The Starter Assets FirstPersonController on your Player root object")]
    [SerializeField] private FirstPersonController firstPersonController;
    [Tooltip("The Starter Assets StarterAssetsInputs on your Player root object - reads the real sprint key state")]
    [SerializeField] private StarterAssetsInputs inputs;
    [SerializeField] private Animator animator;

    [Header("Movement Detection")]
    [Tooltip("Horizontal speed above which we consider the character 'moving'. One tunable number instead of scattered thresholds on transitions.")]
    [SerializeField] private float movementThreshold = 0.05f;

    [Header("Fall Threshold")]
    [Tooltip("How negative vertical velocity must get before we call it FreeFall rather than a normal landing")]
    [SerializeField] private float fallThreshold = -2f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    [Header("Grounded Debounce")]
    [Tooltip("How long a raw grounded-state change must hold steady before the Animator sees it. Filters out brief mid-air collision blips (e.g. clipping a wall) that would otherwise flicker Grounded true/false and confuse transitions.")]
    [SerializeField] private float groundedDebounce = 0.08f;

    private bool _wasGrounded = true;
    private bool _reportedGrounded = true;
    private float _groundedChangeTimer;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<CharacterController>();
        firstPersonController = GetComponentInParent<FirstPersonController>();
        inputs = GetComponentInParent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (controller == null || animator == null || firstPersonController == null || inputs == null) return;

        // --- Movement / sprint state, driven by real input rather than a speed guess ---
        Vector3 flatVelocity = controller.velocity;
        flatVelocity.y = 0f;
        bool isMoving = flatVelocity.magnitude > movementThreshold;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(SprintingHash, inputs.sprint);

        // --- Grounded / falling, debounced so a brief mid-air collision can't flicker the animator ---
        bool rawGrounded = firstPersonController.Grounded;
        if (rawGrounded != _reportedGrounded)
        {
            _groundedChangeTimer += Time.deltaTime;
            if (_groundedChangeTimer >= groundedDebounce)
            {
                _reportedGrounded = rawGrounded;
                _groundedChangeTimer = 0f;
            }
        }
        else
        {
            _groundedChangeTimer = 0f;
        }

        animator.SetBool(GroundedHash, _reportedGrounded);
        animator.SetBool(FreeFallHash, !_reportedGrounded && controller.velocity.y < fallThreshold);

        // --- Jump trigger: fires the instant we leave the ground while still moving upward ---
        if (_wasGrounded && !_reportedGrounded && controller.velocity.y > 0f)
        {
            animator.SetTrigger(JumpHash);
        }

        _wasGrounded = _reportedGrounded;
    }
}