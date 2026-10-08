using System.Collections.Generic;
using UnityEngine;

public class AoESpellHitbox : MonoBehaviour
{
    private Transform owner;
    private ElementType element;
    private float radius;
    private float impulse;
    private float damage;
    private readonly HashSet<Rigidbody> pushed = new();
    private readonly HashSet<IDamageable> damaged = new();

    public static void Create(GameObject effect, Transform owner, ElementType element, float radius, float impulse, float damage)
    {
        var go = new GameObject("AoEHitbox");
        go.transform.position = effect.transform.position + Vector3.up * 0.5f;
        // Keep world scale one so imported VFX scaling cannot shrink the hitbox.
        go.transform.SetParent(effect.transform, true);
        var hitbox = go.AddComponent<AoESpellHitbox>();
        hitbox.owner = owner;
        hitbox.element = element;
        hitbox.radius = radius;
        hitbox.impulse = impulse;
        hitbox.damage = damage;
        var sphere = go.AddComponent<SphereCollider>();
        sphere.radius = radius;
        sphere.isTrigger = true;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Destroy(go, 0.5f);
    }

    private void Start()
    {
        foreach (var collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore)) Hit(collider);
    }
    private void OnTriggerEnter(Collider other) => Hit(other);

    private void Hit(Collider other)
    {
        if (other.transform.IsChildOf(owner) || other.transform.IsChildOf(transform.parent)) return;
        var body = other.attachedRigidbody;
        if (body != null && !body.isKinematic && pushed.Add(body))
            body.AddExplosionForce(impulse, transform.position, radius, 0.6f, ForceMode.Impulse);
        var target = other.GetComponentInParent<IDamageable>();
        if (target != null && damaged.Add(target)) target.TakeDamage(damage, element);
    }
}
