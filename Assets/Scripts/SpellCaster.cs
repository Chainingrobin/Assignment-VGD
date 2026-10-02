using UnityEngine;

// Attach to the Player. Assign one entry per element you have a spell for.
public class SpellCaster : MonoBehaviour
{
    [System.Serializable]
    public struct ElementSpell
    {
        public ElementType element;
        public GameObject spellPrefab; // must have SpellProjectile + a Rigidbody + a trigger collider
    }

    [SerializeField] private ElementSpell[] spells;
    [SerializeField] private Transform castPoint; // empty GameObject in front of the player/camera
    [SerializeField] private float spellSpeed = 20f;

    public void CastSpell()
    {
        ElementType active = PlayerMagicAffinity.Instance.ActiveElement;
        GameObject prefab = GetPrefabForElement(active);

        if (prefab == null)
        {
            Debug.LogWarning($"No spell prefab assigned for {active}. Did you fill in the Spells list?");
            return;
        }

        GameObject spell = Instantiate(prefab, castPoint.position, castPoint.rotation);

        Rigidbody rb = spell.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = castPoint.forward * spellSpeed;

        Debug.Log($"Cast {active} spell from {castPoint.position}");
    }

    private GameObject GetPrefabForElement(ElementType element)
    {
        foreach (var spell in spells)
        {
            if (spell.element == element)
                return spell.spellPrefab;
        }
        return null;
    }
}
