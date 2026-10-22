using UnityEngine;

[RequireComponent(typeof(CombatTarget), typeof(EnemyDetection), typeof(Rigidbody))]
public class VampireMelee : MonoBehaviour
{
    [SerializeField, Min(2f)] private float territoryRadius = 8f;
    [SerializeField, Min(8f)] private float pursuitRadius = 40f;
    private CombatTarget target;
    private EnemyDetection sight;
    private Rigidbody body;
    private Animator animator;
    private Camera player;
    private float attackRemaining;
    private bool interrupted;
    private float attackDuration = 3.1f;
    private float displayDuration = 3f;
    private Vector3 home;
    private Vector3 roamGoal;
    private float pauseRemaining;
    private float roamRemaining;
    private float displayRemaining;
    private bool sawPlayer;
    private bool returning;
    private float movementSpeed;
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
            movementSpeed = 0f;
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
        float speed = 0f;
        float homeDistance = Vector3.ProjectOnPlane(transform.position - home, Vector3.up).magnitude;
        float playerHomeDistance = Vector3.ProjectOnPlane(player.transform.position - home, Vector3.up).magnitude;
        bool sees = sight.CanSeePlayer && playerHomeDistance <= PursuitRadius && homeDistance <= PursuitRadius + 2f;
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
            if (elapsed >= 0.95f && elapsed < 1.4f && distance > 1.05f)
                speed = Mathf.Min(1.5f, (distance - 1.05f) / Time.fixedDeltaTime);
        }
        else if (sees)
        {
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction.normalized), 240f * Time.fixedDeltaTime));
            if (distance <= 1.8f && Vector3.Angle(transform.forward, direction) < 20f)
            {
                animator.SetFloat("Speed", 0f);
                animator.CrossFadeInFixedTime("Melee swipe", 0.18f, 0);
                attackRemaining = attackDuration;
            }
            else if (distance > 1.8f) speed = distance > 5f ? 3.6f : 1.6f;
        }
        else if (pauseRemaining > 0f) pauseRemaining -= Time.fixedDeltaTime;
        else if (returning)
        {
            Turn(Vector3.ProjectOnPlane(home - transform.position, Vector3.up));
            speed = 1.6f;
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
                speed = 1.2f;
                foreach (var obstacle in Physics.SphereCastAll(transform.position + Vector3.up * 0.7f, 0.35f, transform.forward, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(transform) && Vector3.Dot(obstacle.normal, Vector3.up) < 0.7f)
                    { speed = 0f; PickRoamGoal(); break; }
            }
        }
        // Chase only along visible routes; physics keeps the vampire against solid obstacles.
        Vector3 velocity = body.linearVelocity;
        // Accumulate commanded speed independently of velocity lost to ground friction.
        movementSpeed = IsAttacking ? speed : displayRemaining > 0f || pauseRemaining > 0f ? 0f : Mathf.MoveTowards(movementSpeed, speed, 4f * Time.fixedDeltaTime);
        Vector3 horizontal = transform.forward * movementSpeed;
        body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        animator.SetFloat("Speed", IsAttacking ? 0f : movementSpeed, 0.35f, Time.fixedDeltaTime);
    }
}
