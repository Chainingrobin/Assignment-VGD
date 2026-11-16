using UnityEngine;

// Makes an NPC (Abe) a shopkeeper: pressing F on him opens the shop with his stock.
// Placed on the same object as an Interactable; hooks its event at runtime.
[RequireComponent(typeof(Interactable))]
public class Merchant : MonoBehaviour
{
    [SerializeField] private string merchantName = "[Abe]'s Wares";
    [SerializeField] private ItemData[] stock;
    [Tooltip("Line Abe says the first time the shop opens in this region.")]
    [SerializeField] private string greeting = "[Abe: Potions, friend. The road ahead is cruel.]";

    private bool greeted;

    private void Awake() => GetComponent<Interactable>().OnInteract.AddListener(Open);

    public void Open()
    {
        if (ShopUI.IsOpen) return;
        if (!greeted && !string.IsNullOrEmpty(greeting)) { greeted = true; Subtitle.Show(greeting); }
        ShopUI.Open(merchantName, stock);
    }
}
