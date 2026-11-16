using UnityEngine;

public enum ItemCategory { Consumable, Tome, Memory, Lore }

// One item's data, kept out of code so names, prices and lore can be edited in the Inspector.
// Pattern ported from Paramnesia's ItemData / MerchantItemData.
[CreateAssetMenu(fileName = "NewItem", menuName = "Wulang/Item")]
public class ItemData : ScriptableObject
{
    public string displayName;
    [TextArea(3, 6)] public string description;
    public Sprite icon;
    public ItemCategory category;
    [Tooltip("Wubucks cost at Abe's shop; 0 = not sold.")]
    public int price;
    [Tooltip("Health restored when used (potions).")]
    public float healAmount;
    [Tooltip("For tomes: the element whose lore this is.")]
    public ElementType element;
}
