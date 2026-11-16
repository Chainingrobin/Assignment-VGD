using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Wulang's satchel: counted items (potions), elemental tomes and memory/lore entries, each
// with its lore description. H drinks a healing potion. Element tomes are added automatically
// when an element is unlocked. Counted-item pattern ported from Paramnesia's PlayerInventory.
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private ItemData healingPotion;
    [Tooltip("One tome per element; added to the satchel when that element is unlocked.")]
    [SerializeField] private ItemData[] tomes;

    private readonly Dictionary<ItemData, int> counts = new();
    private readonly List<ItemData> order = new();
    private InputAction usePotion;
    private TMP_Text potionCount;

    public event Action Changed;
    public IReadOnlyList<ItemData> Items => order;
    public ItemData HealingPotion => healingPotion;

    private void Awake()
    {
        Instance = this;
        if (inputActions != null) usePotion = inputActions.FindActionMap("Gameplay", true).FindAction("UsePotion", true);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnEnable()
    {
        if (usePotion == null) return;
        usePotion.Enable();
        usePotion.performed += OnUsePotion;
    }

    private void OnDisable()
    {
        if (usePotion != null) usePotion.performed -= OnUsePotion;
    }

    private void Start()
    {
        var affinity = PlayerMagicAffinity.Instance;
        if (affinity != null)
        {
            foreach (var element in affinity.UnlockedElements) AddTome(element);
            affinity.OnElementUnlocked += AddTome;
        }
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        var top = new Vector2(0f, 1f);
        var panel = UIKit.Rect(hud, "PotionHUD", new Vector2(156f, 56f), new Vector2(188f, -24f), top);
        FantasyUI.Panel(panel);
        UIKit.Text(panel, "Key", "<color=#E8C77F>H</color>", 14f, new Vector2(30f, 20f), new Vector2(14f, -18f), top, FantasyUI.Muted);
        potionCount = UIKit.Text(panel, "Count", "", 22f, new Vector2(110f, 36f), new Vector2(38f, -12f), top, FantasyUI.Parchment);
        Refresh();
    }

    private void AddTome(ElementType element)
    {
        if (tomes == null) return;
        foreach (var tome in tomes)
            if (tome != null && tome.element == element && Count(tome) == 0) Add(tome);
    }

    public int Count(ItemData item) => item != null && counts.TryGetValue(item, out int n) ? n : 0;

    public void Add(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;
        if (!counts.ContainsKey(item)) { counts[item] = 0; order.Add(item); }
        counts[item] += amount;
        Refresh();
    }

    public bool Consume(ItemData item, int amount = 1)
    {
        if (Count(item) < amount) return false;
        counts[item] -= amount;
        if (counts[item] == 0) { counts.Remove(item); order.Remove(item); }
        Refresh();
        return true;
    }

    private void OnUsePotion(InputAction.CallbackContext context)
    {
        if (PlayerLock.IsLocked || healingPotion == null) return;
        var health = PlayerHealth.Instance;
        if (Count(healingPotion) == 0) Subtitle.Show("[No potions left. Abe sells them for Wubucks.]", 2.5f);
        else if (health != null && health.Heal(healingPotion.healAmount)) Consume(healingPotion);
        else Subtitle.Show("[I'm already at full strength.]", 2f);
    }

    private void Refresh()
    {
        if (potionCount != null) potionCount.text = $"Potion x{Count(healingPotion)}";
        Changed?.Invoke();
    }
}
