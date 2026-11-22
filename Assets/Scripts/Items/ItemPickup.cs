using UnityEngine;

// An item lying in the world (a healing potion tucked away off the path): walking into it puts it
// in the satchel. Sits on a trigger collider with FloatingPickup for the hover and glow.
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData item;
    private bool taken;

    private void OnTriggerEnter(Collider other)
    {
        if (taken || item == null || other.GetComponentInParent<PlayerCoins>() == null || PlayerInventory.Instance == null) return;
        taken = true;
        PlayerInventory.Instance.Add(item);
        Subtitle.Show($"[{item.displayName} +1]", 1.5f);
        Destroy(gameObject);
    }
}
