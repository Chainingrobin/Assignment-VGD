using System.Collections.Generic;
using UnityEngine;

public class AoESpellHitbox : MonoBehaviour
{
    private Transform owner;
    private float radius;
    private float impulse;
    private SpellHit hit;
    private readonly HashSet<Rigidbody> pushed = new();

    public static void Create(GameObject effect, Transform owner, ElementType element, float radius, float impulse, float damage, Material chainMaterial = null)
    {
        var go = new GameObject("AoEHitbox");
        go.transform.position = effect.transform.position + Vector3.up * 0.5f;
        // Keep world scale one so imported VFX scaling cannot shrink the hitbox.
        go.transform.SetParent(effect.transform, true);
        var hitbox = go.AddComponent<AoESpellHitbox>();
        hitbox.owner = owner;
        hitbox.radius = radius;
        hitbox.impulse = impulse;
        hitbox.hit = new SpellHit(damage, element, effect.transform.position, chainMaterial);
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
        var colliders = new List<Collider>();
        foreach (var collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
            if (Eligible(collider)) { colliders.Add(collider); PushProp(collider); }
        hit.Apply(colliders);
    }
    private void OnTriggerEnter(Collider other) => Hit(other);

    private void Hit(Collider other)
    {
        if (!Eligible(other)) return;
        PushProp(other);
        hit.Apply(other);
    }

    private bool Eligible(Collider other) => (owner == null || !other.transform.IsChildOf(owner)) && !other.transform.IsChildOf(transform.parent);

    private void PushProp(Collider other)
    {
        // Enemy knockback belongs to Earth and its frozen-target reaction.
        if (other.GetComponentInParent<CombatTarget>() != null) return;
        var body = other.attachedRigidbody;
        if (body != null && !body.isKinematic && pushed.Add(body))
            body.AddExplosionForce(impulse, transform.position, radius, 0.6f, ForceMode.Impulse);
    }
}
