using UnityEngine;
using UnityEngine.Serialization;

public class SpellCaster : MonoBehaviour
{
    [System.Serializable]
    public struct ElementSpell
    {
        public ElementType element;

        [Header("Projectile")]
        public GameObject projectilePrefab;
        public float projectileSpeed;
        public float projectileDamage;
        public float projectileSize;

        [Header("AoE")]
        [FormerlySerializedAs("spellPrefab")] public GameObject aoePrefab;
    }

    [SerializeField] private ElementSpell[] spells;
    [SerializeField] private Transform projectileCastPoint;   // child of camera
    [FormerlySerializedAs("castPoint")]
    [SerializeField] private Transform aoeCastPoint;          // your existing feet point
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask aimMask = ~0;   // exclude the Player layer
    [SerializeField] private float maxAimDistance = 100f;

    public void CastProjectile()
    {
        var spell = GetSpell(PlayerMagicAffinity.Instance.ActiveElement);
        if (spell.projectilePrefab == null) return;

        Transform camT = playerCamera.transform;

        // Find what the crosshair is pointing at
        Vector3 aimPoint = camT.position + camT.forward * maxAimDistance;
        if (Physics.Raycast(camT.position, camT.forward, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;

        // Fly from the hand toward that point
        Vector3 dir = (aimPoint - projectileCastPoint.position).normalized;
        if (Vector3.Dot(dir, camT.forward) < 0.1f) dir = camT.forward; // wall right in your face

        var go = Instantiate(spell.projectilePrefab, projectileCastPoint.position, Quaternion.LookRotation(dir));

        var projectileColliders = go.GetComponentsInChildren<Collider>();
        
        foreach (var mine in transform.root.GetComponentsInChildren<Collider>())
            foreach (var theirs in projectileColliders)
                Physics.IgnoreCollision(mine, theirs);

        float speed = spell.projectileSpeed > 0 ? spell.projectileSpeed : 20f;
        float damage = spell.projectileDamage > 0 ? spell.projectileDamage : 25f;
        float size = spell.projectileSize > 0 ? spell.projectileSize : 1f;

        if (go.TryGetComponent<SpellProjectile>(out var proj))
            proj.Init(damage, size);

        if (go.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * speed;
    }

    public void CastAoE()
    {
        var spell = GetSpell(PlayerMagicAffinity.Instance.ActiveElement);
        if (spell.aoePrefab == null) return;

        Instantiate(spell.aoePrefab, aoeCastPoint.position, aoeCastPoint.rotation);
    }

    private ElementSpell GetSpell(ElementType element)
    {
        foreach (var s in spells)
            if (s.element == element) return s;
        return default;
    }
}