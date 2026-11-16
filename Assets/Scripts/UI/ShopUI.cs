using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Abe's shop window: his stock with prices and lore descriptions, the player's Wubucks, and a
// Buy button per item. Opening it pauses the player and frees the cursor; Esc or Leave closes.
public class ShopUI : MonoBehaviour
{
    private static ShopUI instance;
    private Canvas canvas;
    private RectTransform list;
    private TMP_Text wallet, header;
    private ItemData[] stock;
    private System.Action onClose;

    public static bool IsOpen => instance != null && instance.canvas.enabled;

    public static void Open(string merchantName, ItemData[] stock, System.Action onClose = null)
    {
        if (instance == null) instance = Build();
        instance.stock = stock;
        instance.onClose = onClose;
        instance.header.text = merchantName;
        instance.Populate();
        instance.canvas.enabled = true;
        PlayerLock.Push(showCursor: true);
    }

    private static ShopUI Build()
    {
        var canvas = UIKit.Canvas("Shop", 300, true);
        var shop = canvas.gameObject.AddComponent<ShopUI>();
        shop.canvas = canvas;
        var center = new Vector2(0.5f, 0.5f);
        UIKit.Fill(UIKit.Stretch(canvas.transform, "Dim"), new Color(0f, 0f, 0f, 0.55f));
        var panel = UIKit.Rect(canvas.transform, "Panel", new Vector2(760f, 620f), Vector2.zero, center);
        FantasyUI.Panel(panel);
        var top = new Vector2(0.5f, 1f);
        shop.header = UIKit.Text(panel, "Header", "", 36f, new Vector2(700f, 50f), new Vector2(0f, -24f), top,
            FantasyUI.Gold, TextAlignmentOptions.Center);
        shop.wallet = UIKit.Text(panel, "Wallet", "", 22f, new Vector2(700f, 30f), new Vector2(0f, -78f), top,
            FantasyUI.Parchment, TextAlignmentOptions.Center);
        shop.list = UIKit.Rect(panel, "Items", new Vector2(700f, 420f), new Vector2(0f, -120f), top);
        UIKit.Button(panel, "Leave", new Vector2(220f, 56f), new Vector2(0f, 24f), new Vector2(0.5f, 0f), shop.Close);
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            DontDestroyOnLoad(events);
        }
        canvas.enabled = false;
        return shop;
    }

    private void Populate()
    {
        foreach (Transform child in list) Destroy(child.gameObject);
        var top = new Vector2(0.5f, 1f);
        float y = 0f;
        foreach (var item in stock)
        {
            if (item == null) continue;
            var row = UIKit.Rect(list, item.displayName, new Vector2(700f, 120f), new Vector2(0f, y), top);
            UIKit.Fill(row, new Color(0.1f, 0.07f, 0.05f, 0.8f));
            var left = new Vector2(0f, 1f);
            UIKit.Text(row, "Name", $"{item.displayName}   <color=#E8C77F>{item.price} Wubucks</color>", 24f,
                new Vector2(480f, 32f), new Vector2(16f, -10f), left, FantasyUI.Parchment);
            var lore = UIKit.Text(row, "Lore", item.description, 16f, new Vector2(480f, 70f), new Vector2(16f, -44f), left, FantasyUI.Muted);
            lore.textWrappingMode = TextWrappingModes.Normal;
            var captured = item;
            UIKit.Button(row, "Buy", new Vector2(160f, 56f), new Vector2(-16f, 0f), new Vector2(1f, 0.5f), () => Buy(captured));
            y -= 132f;
        }
        RefreshWallet();
    }

    private void Buy(ItemData item)
    {
        var coins = FindFirstObjectByType<PlayerCoins>();
        var inventory = PlayerInventory.Instance;
        if (coins == null || inventory == null) return;
        if (!coins.Spend(item.price)) { Subtitle.Show($"[Not enough Wubucks for the {item.displayName}.]", 2f); return; }
        inventory.Add(item);
        Subtitle.Show($"[Bought {item.displayName}.]", 1.5f);
        RefreshWallet();
    }

    private void RefreshWallet()
    {
        var coins = FindFirstObjectByType<PlayerCoins>();
        wallet.text = $"Your Wubucks: {(coins != null ? coins.Count : 0)}";
    }

    private void Update()
    {
        if (canvas.enabled && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void Close()
    {
        if (!canvas.enabled) return;
        canvas.enabled = false;
        PlayerLock.Pop(showCursor: true);
        onClose?.Invoke();
    }
}
