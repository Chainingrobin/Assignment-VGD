using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    [SerializeField] private ElementType element;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private GameObject hitPrefab;

    private float damage = 25f;
    private float size = 1f;
    private bool hasHit;

    // Called by SpellCaster right after the projectile is spawned
    public void Init(float damage, float size)
    {
        this.damage = damage;
        this.size = size;
        transform.localScale *= size;
    }

    private void Start()
    {
        if (muzzlePrefab)
        {
            var fx = Instantiate(muzzlePrefab, transform.position, transform.rotation);
            fx.transform.localScale *= size;
            Destroy(fx, 2f);
        }
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision c)
    {
        if (hasHit) return;
        hasHit = true;

        var contact = c.GetContact(0);
        if (hitPrefab)
        {
            var fx = Instantiate(hitPrefab, contact.point,
                Quaternion.FromToRotation(Vector3.up, contact.normal));
            fx.transform.localScale *= size;
            Destroy(fx, 3f);
        }

        if (c.collider.TryGetComponent<IDamageable>(out var target))
            target.TakeDamage(damage, element);

        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Destroy(gameObject);
    }
}