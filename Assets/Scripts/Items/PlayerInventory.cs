using System;
using System.Collections.Generic;
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
    // Written from the Story Text asset (Wulang > Story > Apply story text); empty = say nothing.
    [Tooltip("Subtitle when H is pressed with no potions left.")]
    [SerializeField] private string noPotionsLine;
    [Tooltip("Wulang's line when H is pressed at full health.")]
    [SerializeField] private string fullHealthLine;

    private readonly Dictionary<ItemData, int> counts = new();
    private readonly List<ItemData> order = new();
    private InputAction usePotion;

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
        Refresh(); // the potion count shows on the shared status plate (StatusHUD)
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
        if (Count(healingPotion) == 0) { if (!string.IsNullOrEmpty(noPotionsLine)) Subtitle.Show(noPotionsLine, 2.5f); }
        else if (health != null && health.Heal(healingPotion.healAmount)) { Consume(healingPotion); GameAudio.Play("Sfx/Potion", null, 0.7f); }
        else if (!string.IsNullOrEmpty(fullHealthLine)) Subtitle.Show(fullHealthLine, 2f);
    }

    private void Refresh() => Changed?.Invoke();
}
