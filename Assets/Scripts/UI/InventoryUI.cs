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
    private TMP_Text title, detailName, detailText, purseCount;
    private RectTransform purse;
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
        // The purse sits in the satchel's corner as a coin and a number.
        purse = UIKit.Rect(panel, "Purse", new Vector2(160f, 32f), new Vector2(40f, -34f), new Vector2(0f, 1f));
        UIKit.Rect(purse, "Coin", new Vector2(28f, 28f), new Vector2(0f, -2f), new Vector2(0f, 1f)).gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        purseCount = UIKit.Text(purse, "Count", "", 24f, new Vector2(120f, 32f), new Vector2(38f, 0f), new Vector2(0f, 1f), FantasyUI.Parchment);
        UIKit.Hint(panel, "Tab", "Close", new Vector2(-24f, 18f), new Vector2(1f, 0f), 16f);
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
        purse.gameObject.SetActive(!journal);
        purseCount.text = (coins != null ? coins.Count : 0).ToString();
        detailName.text = "";
        detailText.text = items.Length == 0
            ? (journal ? "[Memories and lore you find are written here.]" : "[Your satchel is empty.]")
            : "";
        // A grid of icon tiles (4 across); the count badge shows stacks. Selecting a tile reads its lore.
        const float tile = 96f, gap = 12f;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var button = UIKit.Button(list, "", new Vector2(tile, tile),
                new Vector2((i % 4) * (tile + gap), -(i / 4) * (tile + gap)), new Vector2(0f, 1f), () => Select(item));
            UIKit.ItemGlyph(button.transform, item, 52f);
            int count = inventory.Count(item);
            if (count > 1)
                UIKit.Text(button.transform, "Count", count.ToString(), 20f, new Vector2(40f, 24f), new Vector2(-10f, 8f), new Vector2(1f, 0f),
                    FantasyUI.Parchment, TextAlignmentOptions.BottomRight).fontStyle = FontStyles.Bold;
        }
        if (items.Length > 0) Select(items[0]);
    }

    private void Select(ItemData item)
    {
        detailName.text = item.displayName;
        detailText.text = item.description;
    }
}
