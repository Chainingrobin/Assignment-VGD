using UnityEngine;

// Lets the animation move the body: the clip's own root motion (walk and run strides, the
// leap's jump, a swipe's step) drives the rigidbody, so feet stay planted instead of sliding
// while code pushes the body at a different speed. The AI only picks a gait and a facing.
// Foot IK then keeps each foot on uneven ground and lowers the hips onto the lower foot.
[RequireComponent(typeof(Animator))]
public class RootMotionBody : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float footWeight = 1f;
    [SerializeField, Min(0f)] private float footReach = 0.6f;
    [Tooltip("How far this rig's soles sit above the ground in its idle pose (negative: buried). Retargeting the " +
             "same clips onto differently built guardians leaves each a little off; the body is shifted by it. " +
             "Measured by Wulang > Bosses > Fix animation roots.")]
    [SerializeField] private float groundOffset;

    // How close to the ground (per metre of scale) a foot counts as planted.
    public const float PlantBand = 0.06f;

    private Animator animator;
    private Rigidbody body;
    private float hipOffset;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponentInParent<Rigidbody>();
        animator.applyRootMotion = true;
        animator.updateMode = AnimatorUpdateMode.Fixed; // root motion lands on the physics step
    }

    private void OnAnimatorMove()
    {
        if (body == null || body.isKinematic || Time.deltaTime <= 0f) return;
        var velocity = animator.deltaPosition / Time.deltaTime;
        // Horizontal from the animation; gravity keeps the vertical.
        body.linearVelocity = new Vector3(velocity.x, body.linearVelocity.y, velocity.z);
    }

    private void OnAnimatorIK(int layer)
    {
        if (!animator.isHuman || footWeight <= 0f) return;
        float scale = transform.lossyScale.y;
        float reach = footReach * scale;
        float lowest = 0f;
        // The character's own colliders (a boss's capsule on its rigidbody, Abe's on his root) aren't ground.
        var self = body != null ? body.transform : transform.parent != null ? transform.parent : transform;
        foreach (var goal in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
        {
            // Keep the animated lift (height above the body's base) and add the ground's rise or dip under that foot.
            // A foot within PlantBand of the ground is planted and stands its sole exactly on it: clips retargeted
            // from other rigs leave planted feet a few cm buried or hovering. Higher lifts carry on smoothly.
            var foot = animator.GetIKPosition(goal);
            float sole = (goal == AvatarIKGoal.LeftFoot ? animator.leftFeetBottomHeight : animator.rightFeetBottomHeight) * scale;
            float lift = sole + Mathf.Max(0f, foot.y - transform.position.y - groundOffset - sole - PlantBand * scale);
            if (!Physics.Raycast(foot + Vector3.up * reach, Vector3.down, out var hit, reach * 2f, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(self))
                continue;
            float offset = hit.point.y - transform.position.y;
            lowest = Mathf.Min(lowest, offset);
            animator.SetIKPositionWeight(goal, footWeight);
            animator.SetIKPosition(goal, new Vector3(foot.x, hit.point.y + lift, foot.z));
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * animator.GetIKRotation(goal);
            animator.SetIKRotationWeight(goal, footWeight * 0.6f);
            animator.SetIKRotation(goal, rotation);
        }
        // Drop the hips onto the lower foot so the downhill leg can reach the ground, and stand the
        // whole pose on the ground by the rig's own offset.
        hipOffset = Mathf.Lerp(hipOffset, lowest, 0.2f);
        animator.bodyPosition += Vector3.up * (hipOffset - groundOffset);
    }
}
