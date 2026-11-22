using UnityEngine;

[RequireComponent(typeof(CombatTarget), typeof(EnemyDetection), typeof(Rigidbody))]
public class VampireMelee : MonoBehaviour
{
    [SerializeField, Min(2f)] private float territoryRadius = 8f;
    [SerializeField, Min(8f)] private float pursuitRadius = 40f;
    [SerializeField, Min(0f)] private float swipeDamage = 10f;
    private CombatTarget target;
    private EnemyDetection sight;
    private Rigidbody body;
    private Animator animator;
    private Camera player;
    private float attackRemaining;
    private bool interrupted;
    private bool swipeLanded;
    private float attackDuration = 3.1f;
    private float displayDuration = 3f;
    private Vector3 home;
    private Vector3 roamGoal;
    private float pauseRemaining;
    private float roamRemaining;
    private float displayRemaining;
    private bool sawPlayer;
    private bool returning;
    public bool IsAttacking => attackRemaining > 0f;
    public bool IsPursuing => sawPlayer;
    public float PursuitRadius => Mathf.Max(territoryRadius, pursuitRadius);

    private void Awake()
    {
        target = GetComponent<CombatTarget>();
        sight = GetComponent<EnemyDetection>();
        body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "Melee swipe" || clip.name.ToLowerInvariant().Contains("swip")) attackDuration = clip.length + 0.4f;
        player = Camera.main;
        home = transform.position;
        PickRoamGoal();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "flexing muscles") displayDuration = clip.length;
    }

    private void PickRoamGoal()
    {
        Vector2 offset = Random.insideUnitCircle * territoryRadius * 0.7f;
        roamGoal = home + new Vector3(offset.x, 0, offset.y);
        Debug.Assert(Vector3.Distance(home, roamGoal) <= territoryRadius, "Roaming goal escaped territory.");
        roamRemaining = Random.Range(3f, 6f);
    }

    private void Turn(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction.normalized), 180f * Time.fixedDeltaTime));
    }

    private void FixedUpdate()
    {
        if (animator == null || player == null) return;
        bool blocked = target.IsDead || target.IsFrozen || target.IsStunned;
        if (blocked)
        {
            interrupted = true;
            attackRemaining = 0f;
            displayRemaining = 0f;
            animator.SetFloat("Speed", 0f);
            return;
        }
        if (interrupted)
        {
            animator.CrossFadeInFixedTime("Locomotion", 0.15f);
            interrupted = false;
        }
        Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
        float distance = direction.magnitude;
        // Gait for the Locomotion blend: 0 idle, 1 walk, 2 run. Root motion (RootMotionBody) moves the body.
        float speed = 0f;
        float homeDistance = Vector3.ProjectOnPlane(transform.position - home, Vector3.up).magnitude;
        float playerHomeDistance = Vector3.ProjectOnPlane(player.transform.position - home, Vector3.up).magnitude;
        // Abe's camps are safe ground: a vampire gives up the chase there.
        bool sees = sight.CanSeePlayer && playerHomeDistance <= PursuitRadius && homeDistance <= PursuitRadius + 2f && !Merchant.Sanctuary(player.transform.position);
        if (sees) returning = false;
        else if (homeDistance > territoryRadius) returning = true;
        else if (homeDistance < territoryRadius * 0.5f) returning = false;
        if (sees) pauseRemaining = 0f;
        if (sawPlayer && !sees)
        {
            pauseRemaining = SteamCloud.BlocksSight(target.HeadPosition, player.transform.position) ? 4f : 1.5f;
            displayRemaining = 0f;
            if (!IsAttacking) animator.CrossFadeInFixedTime("Locomotion", 0.35f);
            PickRoamGoal();
        }
        sawPlayer = sees;
        if (sees && displayRemaining > 0f)
        {
            displayRemaining = 0f;
            animator.CrossFadeInFixedTime("Locomotion", 0.2f);
        }
        if (attackRemaining > 0f)
        {
            attackRemaining -= Time.fixedDeltaTime;
            float elapsed = attackDuration - attackRemaining;
            // One damage check per swipe, when the claws come through (~1.2 s into the clip).
            if (!swipeLanded && elapsed >= 1.2f)
            {
                swipeLanded = true;
                if (distance <= 2.2f && Vector3.Angle(transform.forward, direction) < 60f && PlayerHealth.Instance != null)
                    PlayerHealth.Instance.TakeDamage(swipeDamage, target.Affinity);
            }
        }
        else if (sees)
        {
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction.normalized), 240f * Time.fixedDeltaTime));
            if (distance <= 1.8f && Vector3.Angle(transform.forward, direction) < 20f)
            {
                animator.SetFloat("Speed", 0f);
                animator.CrossFadeInFixedTime("Melee swipe", 0.18f, 0);
                attackRemaining = attackDuration;
                swipeLanded = false;
            }
            else if (distance > 1.8f) speed = distance > 5f ? 2f : 1f;
        }
        else if (pauseRemaining > 0f) pauseRemaining -= Time.fixedDeltaTime;
        else if (returning)
        {
            Turn(Vector3.ProjectOnPlane(home - transform.position, Vector3.up));
            speed = 1f;
        }
        else if (displayRemaining > 0f) displayRemaining -= Time.fixedDeltaTime;
        else
        {
            Vector3 toGoal = Vector3.ProjectOnPlane(roamGoal - transform.position, Vector3.up);
            roamRemaining -= Time.fixedDeltaTime;
            if (toGoal.magnitude < 0.7f || roamRemaining <= 0f)
            {
                PickRoamGoal();
                pauseRemaining = Random.Range(1f, 2f);
                if (Random.value < 0.35f)
                {
                    animator.CrossFadeInFixedTime("Display", 0.25f);
                    displayRemaining = displayDuration;
                    pauseRemaining = 0f;
                }
            }
            else
            {
                Turn(toGoal);
                speed = 1f;
                foreach (var obstacle in Physics.SphereCastAll(transform.position + Vector3.up * 0.7f, 0.35f, transform.forward, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(transform) && Vector3.Dot(obstacle.normal, Vector3.up) < 0.7f)
                    { speed = 0f; PickRoamGoal(); break; }
            }
        }
        // Chase only along visible routes; the animation's root motion carries the body, and
        // physics keeps it against solid obstacles.
        bool still = IsAttacking || displayRemaining > 0f || pauseRemaining > 0f;
        animator.SetFloat("Speed", still ? 0f : speed, 0.35f, Time.fixedDeltaTime);
    }
}
