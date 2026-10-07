using UnityEngine;
using StarterAssets;

// Put this on the Protagonist object (the child that has the Animator).
// It reads state from FirstPersonController / CharacterController / StarterAssetsInputs
// on the parent player object and drives these Animator parameters
// (names must match the Animator window exactly, including capitalization):
//   horizontal, vertical (float) - local-space velocity for the 2D blend tree
//   IsMoving, Sprinting  (bool)
//   isGrounded, isJumping, isFalling (bool)
// Make sure Apply Root Motion is OFF on the Animator.
[RequireComponent(typeof(Animator))]
public class CharacterAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private StarterAssetsInputs inputs;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [Tooltip("Horizontal speed above which the character counts as moving.")]
    [SerializeField] private float movementThreshold = 0.05f;
    [Tooltip("Smoothing for the blend tree inputs so the pose doesn't pop between clips.")]
    [SerializeField] private float blendDamping = 0.1f;

    [Header("Air")]
    [Tooltip("Vertical velocity below which the character counts as falling.")]
    [SerializeField] private float fallThreshold = -2f;
    [Tooltip("How long a grounded change must hold before the Animator sees it (filters brief collision blips).")]
    [SerializeField] private float groundedDebounce = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool warnAboutMissingParameters = true;

    private static readonly string[] ParameterNames =
    {
        "horizontal", "vertical", "IsMoving", "Sprinting",
        "isGrounded", "isJumping", "isFalling"
    };

    private static readonly int HorizontalHash = Animator.StringToHash("horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("vertical");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");

    private bool _wasGrounded = true;
    private bool _reportedGrounded = true;
    private bool _jumping;
    private float _groundedChangeTimer;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<CharacterController>();
        firstPersonController = GetComponentInParent<FirstPersonController>();
        inputs = GetComponentInParent<StarterAssetsInputs>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (!warnAboutMissingParameters || animator == null) return;

        // Tells you in the Console if a parameter name here doesn't match the Animator window.
        foreach (string paramName in ParameterNames)
        {
            bool found = false;
            foreach (var p in animator.parameters)
            {
                if (p.name == paramName) { found = true; break; }
            }
            if (!found)
            {
                Debug.LogWarning($"[CharacterAnimationController] Animator has no parameter named '{paramName}'. " +
                                 "Fix the name in the Animator window or in this script.", this);
            }
        }
    }

    private void Update()
    {
        if (controller == null || animator == null || firstPersonController == null || inputs == null) return;

        Vector3 velocity = controller.velocity;
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);

        // --- Movement: blend tree inputs ---
        // Local velocity relative to the player's facing, divided by sprint speed,
        // so sprinting forward = 1 and walking forward = MoveSpeed / SprintSpeed.
        float sprintSpeed = Mathf.Max(0.01f, firstPersonController.SprintSpeed);
        Vector3 localVelocity = controller.transform.InverseTransformDirection(flatVelocity) / sprintSpeed;

        animator.SetFloat(HorizontalHash, localVelocity.x, blendDamping, Time.deltaTime);
        animator.SetFloat(VerticalHash, localVelocity.z, blendDamping, Time.deltaTime);
        animator.SetBool(IsMovingHash, flatVelocity.magnitude > movementThreshold);
        animator.SetBool(SprintingHash, inputs.sprint);

        // --- Grounded, debounced so a brief mid-air collision can't flicker the Animator ---
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

        bool grounded = _reportedGrounded;
        bool falling = !grounded && velocity.y < fallThreshold;

        // Jump starts when we leave the ground moving upward,
        // and ends when we land or start descending.
        if (_wasGrounded && !grounded && velocity.y > 0f) _jumping = true;
        if (grounded || falling) _jumping = false;

        animator.SetBool(IsGroundedHash, grounded);
        animator.SetBool(IsJumpingHash, _jumping);
        animator.SetBool(IsFallingHash, falling);

        _wasGrounded = grounded;
    }
}