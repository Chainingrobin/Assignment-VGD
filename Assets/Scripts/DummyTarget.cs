using UnityEngine;

// A plain cube with a collider is enough for this. No AI, no animations -
// just proves your damage pipeline works end to end.
public class DummyTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, ElementType sourceElement = ElementType.None)
    {
        currentHealth -= amount;
        Debug.Log($"{name} took {amount} {sourceElement} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Debug.Log($"{name} destroyed.");
            Destroy(gameObject);
        }
    }
}
