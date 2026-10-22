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
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask aimMask = ~0;   // exclude the Player layer
    [SerializeField] private float maxAimDistance = 100f;
    [SerializeField] private LayerMask aoeGroundMask = ~0;
    [Header("AoE Hitbox")]
    [SerializeField, Min(0.1f)] private float aoeRadius = 5f;
    [SerializeField, Min(0f)] private float aoeImpulse = 12f;
    [SerializeField, Min(0f)] private float aoeDamage = 40f;
    [SerializeField, Min(0.05f)] private float projectileInterval = 0.35f;
    [SerializeField, Min(0.05f)] private float areaInterval = 1f;
    [SerializeField] private Material chainMaterial;
    private float nextProjectileTime;
    private float nextAreaTime;
    public SpellCharges Charges { get; } = new();
    public Camera PlayerCamera => playerCamera;

    private void Update() => Charges.Advance(Time.deltaTime);

    public void Reload() => Charges.Reload();

    public bool CastProjectile()
    {
        if (Time.time < nextProjectileTime || Charges.IsReloading || Charges.Projectiles == 0) return false;
        var spell = GetSpell(PlayerMagicAffinity.Instance.ActiveElement);
        if (spell.projectilePrefab == null || playerCamera == null || projectileCastPoint == null) return false;
        if (spell.projectilePrefab.GetComponent<SpellProjectile>() == null)
        {
            Debug.LogError($"{spell.element} projectile is missing SpellProjectile.", this);
            return false;
        }
        if (!Charges.SpendProjectile()) return false;
        nextProjectileTime = Time.time + projectileInterval;

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
        float damage = spell.projectileDamage > 0 ? spell.projectileDamage : 20f;
        float size = spell.projectileSize > 0 ? spell.projectileSize : 1f;

        if (go.TryGetComponent<SpellProjectile>(out var proj))
            proj.Init(damage, size, spell.element, transform.root, chainMaterial);

        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            // Imported projectile mass otherwise pushes targets even with non-Earth spells.
            rb.mass = 0.01f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = dir * speed;
        }

        SpellElementLight.AddTo(go, spell.element, false);
        return true;
    }

    public bool CastAoE()
    {
        if (Time.time < nextAreaTime || Charges.Areas == 0) return false;
        var spell = GetSpell(PlayerMagicAffinity.Instance.ActiveElement);
        if (spell.aoePrefab == null) return false;

        var controller = GetComponentInParent<CharacterController>();
        var player = controller != null ? controller.transform : transform;
        var origin = player.position;
        origin.y = controller != null ? controller.bounds.max.y + 0.5f : origin.y + 2f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 1000f, aoeGroundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player) || hit.normal.y < 0.5f || hit.collider.GetComponentInParent<CombatTarget>() != null) continue;
            if (!Charges.SpendArea()) return false;
            nextAreaTime = Time.time + areaInterval;
            var rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            var go = Instantiate(spell.aoePrefab, hit.point + Vector3.up * 0.02f, rotation);
            TerrainAoE.AddTo(go, spell.element, player, aoeGroundMask);
            SpellElementLight.AddTo(go, spell.element, true);
            AoESpellHitbox.Create(go, player, spell.element, aoeRadius, aoeImpulse, aoeDamage, chainMaterial);
            return true;
        }
        return false;
    }

    private ElementSpell GetSpell(ElementType element)
    {
        foreach (var s in spells)
            if (s.element == element) return s;
        return default;
    }
}
