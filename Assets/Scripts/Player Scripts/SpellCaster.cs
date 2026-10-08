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
    [SerializeField] private LayerMask aoeGroundMask = ~0;
    [Header("AoE Hitbox")]
    [SerializeField, Min(0.1f)] private float aoeRadius = 5f;
    [SerializeField, Min(0f)] private float aoeImpulse = 12f;
    [SerializeField, Min(0f)] private float aoeDamage = 25f;

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

        SpellElementLight.AddTo(go, spell.element, false);
    }

    public void CastAoE()
    {
        var spell = GetSpell(PlayerMagicAffinity.Instance.ActiveElement);
        if (spell.aoePrefab == null) return;

        var controller = GetComponentInParent<CharacterController>();
        var player = controller != null ? controller.transform : transform;
        var origin = player.position;
        origin.y = controller != null ? controller.bounds.max.y + 0.5f : origin.y + 2f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 1000f, aoeGroundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player)) continue;
            var rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            var go = Instantiate(spell.aoePrefab, hit.point + Vector3.up * 0.02f, rotation);
            SpellElementLight.AddTo(go, spell.element, true);
            AoESpellHitbox.Create(go, player, spell.element, aoeRadius, aoeImpulse, aoeDamage);
            return;
        }
    }

    private ElementSpell GetSpell(ElementType element)
    {
        foreach (var s in spells)
            if (s.element == element) return s;
        return default;
    }
}
