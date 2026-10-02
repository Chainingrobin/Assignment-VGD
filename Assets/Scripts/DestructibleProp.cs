using UnityEngine;

// Attach to an intact prop (crate, barrel, etc.). Assign a "broken" version
// of the same prop as brokenPiecesPrefab - many asset packs already include
// a pre-shattered variant. Each piece in that prefab needs its own Rigidbody.
public class DestructibleProp : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private GameObject brokenPiecesPrefab;
    [SerializeField] private float explosionForce = 5f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float debrisCleanupTime = 8f;

    private float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, ElementType sourceElement = ElementType.None)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
            Break();
    }

    private void Break()
    {
        if (brokenPiecesPrefab != null)
        {
            GameObject debris = Instantiate(brokenPiecesPrefab, transform.position, transform.rotation);

            // Push each piece outward so it looks like an explosion, not a swap.
            foreach (Rigidbody piece in debris.GetComponentsInChildren<Rigidbody>())
            {
                piece.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }

            Destroy(debris, debrisCleanupTime);
        }

        Destroy(gameObject);
    }
}
