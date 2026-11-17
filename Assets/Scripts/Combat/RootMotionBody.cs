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
        foreach (var goal in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
        {
            // Keep the animated lift (height above the body's base) and add the ground's rise or dip under that foot.
            var foot = animator.GetIKPosition(goal);
            float lift = foot.y - transform.position.y;
            if (!Physics.Raycast(foot + Vector3.up * reach, Vector3.down, out var hit, reach * 2f, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(body != null ? body.transform : transform))
                continue;
            float offset = hit.point.y - transform.position.y;
            lowest = Mathf.Min(lowest, offset);
            animator.SetIKPositionWeight(goal, footWeight);
            animator.SetIKPosition(goal, new Vector3(foot.x, hit.point.y + lift, foot.z));
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * animator.GetIKRotation(goal);
            animator.SetIKRotationWeight(goal, footWeight * 0.6f);
            animator.SetIKRotation(goal, rotation);
        }
        // Drop the hips onto the lower foot so the downhill leg can reach the ground.
        hipOffset = Mathf.Lerp(hipOffset, lowest, 0.2f);
        animator.bodyPosition += Vector3.up * hipOffset;
    }
}
