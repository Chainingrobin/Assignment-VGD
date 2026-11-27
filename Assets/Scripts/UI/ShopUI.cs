using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Abe's trade window, one tidy panel: the trader's portrait and name on the left, the player's
// purse (a coin and a number) on the right, then his wares. He sells one thing (healing potions),
// so a single ware is one large display: the item's picture with how many Wulang carries, its name
// and lore, and one Buy button showing the price. Several wares fall back to a row of cards. Esc
// leaves. Not enough Wubucks makes the purse flash red instead of a message. Opening it pauses the
// player and frees the cursor.
public class ShopUI : MonoBehaviour
{
    private static ShopUI instance;
    private Canvas canvas;
    private RectTransform cards;
    private Image portrait;
    private TMP_Text wallet, header, lore, owned;
    private ItemData single;
    private ItemData[] stock;
    private System.Action onClose;

    public static bool IsOpen => instance != null && instance.canvas.enabled;

    public static void Open(string merchantName, Sprite face, ItemData[] stock, System.Action onClose = null)
    {
        if (instance == null) instance = Build();
        instance.stock = stock;
        instance.onClose = onClose;
        instance.header.text = merchantName;
        instance.portrait.sprite = face;
        instance.portrait.transform.parent.parent.gameObject.SetActive(face != null);
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
        var topLeft = new Vector2(0f, 1f);
        UIKit.Fill(UIKit.Stretch(canvas.transform, "Dim"), new Color(0f, 0f, 0f, 0.55f));
        var panel = UIKit.Rect(canvas.transform, "Panel", new Vector2(700f, 430f), Vector2.zero, center);
        FantasyUI.Panel(panel);

        // Trader: round portrait and name.
        var frame = UIKit.Rect(panel, "Portrait", new Vector2(100f, 100f), new Vector2(32f, -28f), topLeft);
        frame.gameObject.AddComponent<Image>().sprite = FantasyUI.Socket;
        var window = UIKit.Rect(frame, "Window", new Vector2(80f, 80f), Vector2.zero, center);
        window.gameObject.AddComponent<Image>().sprite = FantasyUI.Socket;
        window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        shop.portrait = UIKit.Rect(window, "Image", new Vector2(80f, 80f), Vector2.zero, center).gameObject.AddComponent<Image>();
        shop.portrait.preserveAspect = true;
        shop.header = UIKit.Text(panel, "Name", "", 34f, new Vector2(360f, 44f), new Vector2(150f, -54f), topLeft, FantasyUI.Gold);
        shop.header.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;

        // Purse, top right.
        var purse = UIKit.Rect(panel, "Purse", new Vector2(120f, 32f), new Vector2(-32f, -60f), Vector2.one);
        UIKit.Rect(purse, "Coin", new Vector2(28f, 28f), new Vector2(0f, -2f), topLeft).gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        shop.wallet = UIKit.Text(purse, "Wallet", "", 26f, new Vector2(84f, 32f), new Vector2(36f, 0f), topLeft, FantasyUI.Parchment);

        UIKit.Fill(UIKit.Rect(panel, "Divider", new Vector2(636f, 1f), new Vector2(0f, -146f), new Vector2(0.5f, 1f)), FantasyUI.Gold * new Color(1f, 1f, 1f, 0.35f));
        shop.cards = UIKit.Rect(panel, "Wares", new Vector2(636f, 176f), new Vector2(0f, -166f), new Vector2(0.5f, 1f));
        shop.lore = UIKit.Text(panel, "Lore", "", 18f, new Vector2(636f, 50f), new Vector2(0f, 64f), new Vector2(0.5f, 0f), FantasyUI.Muted,
            TextAlignmentOptions.Top);
        shop.lore.textWrappingMode = TextWrappingModes.Normal;
        UIKit.Hint(panel, "Esc", "Leave", new Vector2(-24f, 18f), new Vector2(1f, 0f), 16f);
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            DontDestroyOnLoad(events);
        }
        canvas.enabled = false;
        return shop;
    }

    // One ware: a large picture, the count carried, its name and lore, and the Buy button with its price.
    private void Single(ItemData item)
    {
        single = item;
        var left = new Vector2(0f, 1f);
        var frame = UIKit.Rect(cards, "Picture", new Vector2(150f, 150f), new Vector2(20f, -6f), left);
        frame.gameObject.AddComponent<Image>().sprite = FantasyUI.Socket;
        frame.GetComponent<Image>().raycastTarget = false;
        UIKit.ItemGlyph(frame, item, 92f);
        owned = UIKit.Text(frame, "Owned", "", 26f, new Vector2(70f, 30f), new Vector2(-4f, 4f), Vector2.right,
            FantasyUI.Parchment, TextAlignmentOptions.BottomRight);
        owned.fontStyle = FontStyles.Bold;
        UIKit.Text(cards, "Name", item.displayName, 30f, new Vector2(420f, 38f), new Vector2(200f, -10f), left, FantasyUI.Gold)
            .fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
        var about = UIKit.Text(cards, "Lore", item.description, 19f, new Vector2(420f, 56f), new Vector2(200f, -52f), left, FantasyUI.Muted);
        about.textWrappingMode = TextWrappingModes.Normal;
        var buy = UIKit.Button(cards, "Buy", new Vector2(240f, 58f), new Vector2(200f, -112f), left, () => Buy(item));
        var label = buy.GetComponentInChildren<TMP_Text>();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(26f, 0f, 0f, 0f);
        var price = UIKit.Rect(buy.transform, "Price", new Vector2(100f, 32f), new Vector2(-18f, 0f), new Vector2(1f, 0.5f));
        UIKit.Rect(price, "Coin", new Vector2(26f, 26f), Vector2.zero, new Vector2(0f, 0.5f)).gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        UIKit.Text(price, "Amount", item.price.ToString(), 26f, new Vector2(66f, 32f), new Vector2(34f, 0f), new Vector2(0f, 0.5f), FantasyUI.Gold,
            TextAlignmentOptions.MidlineLeft).fontStyle = FontStyles.Bold;
        lore.text = "";
        RefreshWallet();
    }

    // One card per item, centred in a row.
    private void Populate()
    {
        foreach (Transform child in cards) Destroy(child.gameObject);
        single = null;
        owned = null;
        const float width = 150f, gap = 16f;
        int count = 0;
        foreach (var item in stock) if (item != null) count++;
        if (count == 1) { foreach (var item in stock) if (item != null) Single(item); return; }
        float x = -(count * width + (count - 1) * gap) * 0.5f + width * 0.5f;
        var top = new Vector2(0.5f, 1f);
        foreach (var item in stock)
        {
            if (item == null) continue;
            var captured = item;
            var card = UIKit.Button(cards, "", new Vector2(width, 176f), new Vector2(x, 0f), top, () => Buy(captured));
            UIKit.ItemGlyph(UIKit.Rect(card.transform, "Slot", new Vector2(80f, 80f), new Vector2(0f, -14f), top), item, 60f);
            UIKit.Text(card.transform, "Name", item.displayName, 18f, new Vector2(width - 16f, 26f), new Vector2(0f, -100f), top,
                FantasyUI.Parchment, TextAlignmentOptions.Center);
            var price = UIKit.Rect(card.transform, "Price", new Vector2(80f, 28f), new Vector2(0f, -132f), top);
            UIKit.Rect(price, "Coin", new Vector2(22f, 22f), new Vector2(4f, -3f), new Vector2(0f, 1f)).gameObject.AddComponent<CoinIcon>().raycastTarget = false;
            UIKit.Text(price, "Amount", item.price.ToString(), 22f, new Vector2(50f, 28f), new Vector2(32f, 0f), new Vector2(0f, 1f), FantasyUI.Gold);
            // Hovering a card reads its lore underneath.
            var hover = card.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => lore.text = captured.description);
            hover.triggers.Add(enter);
            x += width + gap;
        }
        lore.text = "";
        RefreshWallet();
    }

    private void Buy(ItemData item)
    {
        var coins = FindFirstObjectByType<PlayerCoins>();
        var inventory = PlayerInventory.Instance;
        if (coins == null || inventory == null) return;
        if (!coins.Spend(item.price)) { StopAllCoroutines(); StartCoroutine(FlashPurse()); return; }
        inventory.Add(item);
        GameAudio.Play("Sfx/Coin", null, 0.6f);
        if (single == null) Subtitle.Show($"{item.displayName} +1", 1.2f);
        RefreshWallet();
    }

    private IEnumerator FlashPurse()
    {
        var red = new Color(1f, 0.35f, 0.3f);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            wallet.color = Color.Lerp(red, FantasyUI.Parchment, t / 0.6f);
            yield return null;
        }
        wallet.color = FantasyUI.Parchment;
    }

    private void RefreshWallet()
    {
        var coins = FindFirstObjectByType<PlayerCoins>();
        wallet.text = (coins != null ? coins.Count : 0).ToString();
        if (owned != null && single != null && PlayerInventory.Instance != null) owned.text = "x" + PlayerInventory.Instance.Count(single);
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
