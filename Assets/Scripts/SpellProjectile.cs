using UnityEngine;

// Attach to each spell prefab (fireball, ice shard, etc.) alongside a
// Rigidbody and a trigger Collider.
public class SpellProjectile : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private ElementType element;
    [SerializeField] private float lifeTime = 5f; // cleanup if it never hits anything

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, element);
        }
        Destroy(gameObject);
    }
}
