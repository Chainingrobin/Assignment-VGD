using System.Collections.Generic;
using UnityEngine;

// A cast owns its hit list, including all colliders, area hits and lightning arcs.
public sealed class SpellHit
{
    private readonly HashSet<IDamageable> hit = new();
    private readonly float damage;
    private readonly ElementType element;
    private readonly Vector3 origin;
    private readonly Material arcMaterial;
    private int arcsRemaining = 2;

    public SpellHit(float damage, ElementType element, Vector3 origin, Material arcMaterial = null)
    {
        this.damage = damage;
        this.element = element;
        this.origin = origin;
        this.arcMaterial = arcMaterial;
    }

    public void Apply(Collider collider) => Apply(new[] { collider });

    public void Apply(IEnumerable<Collider> colliders)
    {
        var primary = new List<IDamageable>();
        var burning = new Dictionary<CombatTarget, float>();
        foreach (var collider in colliders)
        {
            var target = collider.GetComponentInParent<IDamageable>();
            if (target != null && hit.Add(target))
            {
                primary.Add(target);
                if (target is CombatTarget enemy) burning[enemy] = enemy.BurnRemaining;
            }
        }
        // Reserve every direct area hit before chains, so an arc cannot replace a full hit.
        foreach (var target in primary)
        {
            if (target is CombatTarget enemy) enemy.ReceiveSpell(damage, element, origin);
            else target.TakeDamage(damage, element);
        }
        if (element != ElementType.Lightning) return;
        foreach (var target in primary)
            if (target is CombatTarget enemy) ChainFrom(enemy, burning[enemy]);
    }

    private void ChainFrom(CombatTarget source, float burn)
    {
        Vector3 start = source.AimPoint;
        while (arcsRemaining > 0)
        {
            CombatTarget next = null;
            float nearest = float.PositiveInfinity;
            foreach (var collider in Physics.OverlapSphere(start, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = collider.GetComponentInParent<CombatTarget>();
                if (candidate == null || candidate.IsDead || hit.Contains(candidate)) continue;
                float distance = (candidate.AimPoint - start).sqrMagnitude;
                if (distance > 16f || distance >= nearest || Blocked(start, candidate.AimPoint)) continue;
                nearest = distance;
                next = candidate;
            }
            if (next == null) return;
            hit.Add(next);
            arcsRemaining--;
            next.ReceiveSpell(damage * 0.5f, element, start);
            next.TransferBurn(burn);
            if (arcMaterial != null) LightningArc.Create(source, next, arcMaterial);
            source = next;
            start = next.AimPoint;
        }
    }

    private static bool Blocked(Vector3 start, Vector3 end)
    {
        foreach (var obstacle in Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
            if (obstacle.collider.GetComponentInParent<CombatTarget>() == null) return true;
        return false;
    }

}
