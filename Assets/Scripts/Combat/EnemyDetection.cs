using UnityEngine;

[RequireComponent(typeof(CombatTarget))]
public class EnemyDetection : MonoBehaviour
{
    [SerializeField, Min(1f)] private float viewDistance = 18f;
    [SerializeField, Range(1f, 180f)] private float viewAngle = 120f;
    private CombatTarget target;
    private Camera playerCamera;
    private Transform playerRoot;
    public bool CanSeePlayer { get; private set; }

    private void Awake() => target = GetComponent<CombatTarget>();

    private void Update()
    {
        if (playerCamera == null)
        {
            var caster = FindFirstObjectByType<SpellCaster>();
            playerCamera = caster != null ? caster.PlayerCamera : Camera.main;
            playerRoot = caster != null ? caster.transform.root : playerCamera != null ? playerCamera.transform.root : null;
        }
        CanSeePlayer = playerCamera != null && CanDetect(playerCamera.transform.position, playerRoot);
    }

    public bool CanDetect(Vector3 point, Transform playerRoot = null)
    {
        if (target == null) target = GetComponent<CombatTarget>();
        if (target.IsDead) return false;
        if (CanSee(point, playerRoot)) return true;
        if (Vector3.Distance(target.HeadPosition, point) > 1.5f) return false;
        return ClearPath(target.HeadPosition, point, playerRoot);
    }

    public bool CanSee(Vector3 point, Transform playerRoot = null)
    {
        if (target == null) target = GetComponent<CombatTarget>();
        if (target.IsDead) return false;
        Vector3 eye = target.HeadPosition;
        Vector3 direction = point - eye;
        var behavior = GetComponent<VampireMelee>();
        float range = behavior != null && behavior.IsPursuing ? Mathf.Max(viewDistance, behavior.PursuitRadius) : viewDistance;
        if (direction.sqrMagnitude > range * range ||
            Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f || SteamCloud.BlocksSight(eye, point)) return false;
        return ClearPath(eye, point, playerRoot);
    }

    private bool ClearPath(Vector3 eye, Vector3 point, Transform playerRoot)
    {
        Vector3 direction = point - eye;
        foreach (var obstacle in Physics.RaycastAll(eye, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (obstacle.transform.IsChildOf(transform) ||
                (playerRoot != null && obstacle.transform.IsChildOf(playerRoot)) ||
                obstacle.collider.GetComponentInParent<CombatTarget>() != null) continue;
            return false;
        }
        return true;
    }

    private void OnDisable() => CanSeePlayer = false;
}
