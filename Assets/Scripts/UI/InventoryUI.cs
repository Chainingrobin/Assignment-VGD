using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Tab opens Wulang's satchel: a Satchel page (potions, elemental tomes) and a Journal page
// (memories and lore found). Selecting an entry shows its lore description, so items carry
// the story as well as the mechanics.
public class InventoryUI : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform list;
    private TMP_Text title, detailName, detailText, tabLabel;
    private bool journal;

    private void Start()
    {
        canvas = UIKit.Canvas("Inventory", 320);
        canvas.transform.SetParent(transform, false);
        var center = new Vector2(0.5f, 0.5f);
        UIKit.Fill(UIKit.Stretch(canvas.transform, "Dim"), new Color(0f, 0f, 0f, 0.6f));
        var panel = UIKit.Rect(canvas.transform, "Panel", new Vector2(1100f, 680f), Vector2.zero, center);
        FantasyUI.Panel(panel);
        var top = new Vector2(0.5f, 1f);
        title = UIKit.Text(panel, "Title", "", 38f, new Vector2(1000f, 50f), new Vector2(0f, -22f), top, FantasyUI.Gold, TextAlignmentOptions.Center);
        UIKit.Button(panel, "Satchel", new Vector2(200f, 50f), new Vector2(-110f, -80f), top, () => Show(false));
        UIKit.Button(panel, "Journal", new Vector2(200f, 50f), new Vector2(110f, -80f), top, () => Show(true));
        list = UIKit.Rect(panel, "List", new Vector2(420f, 500f), new Vector2(40f, -150f), new Vector2(0f, 1f));
        var detail = UIKit.Rect(panel, "Detail", new Vector2(560f, 500f), new Vector2(-40f, -150f), Vector2.one);
        UIKit.Fill(detail, new Color(0.08f, 0.055f, 0.04f, 0.85f));
        var left = new Vector2(0f, 1f);
        detailName = UIKit.Text(detail, "Name", "", 30f, new Vector2(520f, 40f), new Vector2(20f, -18f), left, FantasyUI.Gold);
        detailText = UIKit.Text(detail, "Lore", "", 22f, new Vector2(520f, 420f), new Vector2(20f, -70f), left, FantasyUI.Parchment);
        detailText.textWrappingMode = TextWrappingModes.Normal;
        tabLabel = UIKit.Text(panel, "Hint", "<color=#E8C77F>Tab</color>  close", 16f, new Vector2(200f, 24f), new Vector2(-24f, 16f),
            new Vector2(1f, 0f), FantasyUI.Muted, TextAlignmentOptions.BottomRight);
        canvas.enabled = false;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame) return;
        if (canvas.enabled) Close();
        else if (!PlayerLock.IsLocked) Open();
    }

    private void Open()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            DontDestroyOnLoad(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));
        canvas.enabled = true;
        PlayerLock.Push(showCursor: true);
        Show(journal);
    }

    private void Close()
    {
        canvas.enabled = false;
        PlayerLock.Pop(showCursor: true);
    }

    private void Show(bool journalPage)
    {
        journal = journalPage;
        title.text = journal ? "Journal" : "Satchel";
        foreach (Transform child in list) Destroy(child.gameObject);
        var inventory = PlayerInventory.Instance;
        var items = inventory == null ? new ItemData[0] : inventory.Items.Where(i =>
            journal ? i.category is ItemCategory.Memory or ItemCategory.Lore : i.category is ItemCategory.Consumable or ItemCategory.Tome).ToArray();
        var coins = FindFirstObjectByType<PlayerCoins>();
        detailName.text = journal ? "Journal" : $"Wubucks: {(coins != null ? coins.Count : 0)}";
        detailText.text = items.Length == 0
            ? (journal ? "[Memories and lore you find are written here.]" : "[Your satchel is empty.]")
            : "Select an entry to read it.";
        float y = 0f;
        foreach (var item in items)
        {
            int count = inventory.Count(item);
            string label = count > 1 ? $"{item.displayName}  x{count}" : item.displayName;
            var captured = item;
            var button = UIKit.Button(list, label, new Vector2(420f, 56f), new Vector2(0f, y), new Vector2(0.5f, 1f), () => Select(captured));
            button.GetComponentInChildren<TMP_Text>().fontSize = 20f;
            y -= 64f;
        }
    }

    private void Select(ItemData item)
    {
        detailName.text = item.displayName;
        detailText.text = item.description;
    }
}
