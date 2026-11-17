using TMPro;
using UnityEngine;

// The top-left status plate: vitality (heart + bar + value) on the first row, then Wubucks
// and healing potions (with the H key to drink) on the second. One plate instead of three
// loose boxes keeps the HUD corner tidy and evenly spaced.
public class StatusHUD : MonoBehaviour
{
    private RectTransform healthFill;
    private TMP_Text healthValue, coins, potions;
    private PlayerHealth health;
    private PlayerCoins wallet;
    private PlayerInventory inventory;

    private void Start()
    {
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        health = GetComponent<PlayerHealth>();
        wallet = GetComponentInChildren<PlayerCoins>();
        inventory = GetComponent<PlayerInventory>();

        var left = new Vector2(0f, 1f);
        var plate = UIKit.Rect(hud, "StatusPlate", new Vector2(372f, 112f), new Vector2(24f, -24f), left);
        FantasyUI.Panel(plate);

        HUDIcon.Create(plate, HUDIcon.Shape.Heart, 26f, new Vector2(20f, -18f), left);
        UIKit.Text(plate, "VitalityLabel", "Vitality", 13f, new Vector2(120f, 16f), new Vector2(56f, -14f), left, FantasyUI.Muted).fontStyle = FontStyles.SmallCaps;
        healthValue = UIKit.Text(plate, "VitalityValue", "", 15f, new Vector2(120f, 18f), new Vector2(-20f, -13f), Vector2.one,
            FantasyUI.Parchment, TextAlignmentOptions.TopRight);
        healthFill = UIKit.Bar(plate, "VitalityBar", new Vector2(296f, 12f), new Vector2(56f, -32f), left, new Color(0.78f, 0.14f, 0.12f));

        UIKit.Fill(UIKit.Rect(plate, "Divider", new Vector2(332f, 1f), new Vector2(20f, -58f), left), FantasyUI.Gold * new Color(1f, 1f, 1f, 0.35f));

        var coinIcon = UIKit.Rect(plate, "CoinIcon", new Vector2(26f, 26f), new Vector2(20f, -70f), left).gameObject.AddComponent<CoinIcon>();
        coinIcon.raycastTarget = false;
        coins = UIKit.Text(plate, "Wubucks", "", 22f, new Vector2(120f, 28f), new Vector2(54f, -68f), left, FantasyUI.Parchment);
        UIKit.Text(plate, "WubucksLabel", "Wubucks", 12f, new Vector2(80f, 14f), new Vector2(54f, -94f), left, FantasyUI.Muted).fontStyle = FontStyles.SmallCaps;

        HUDIcon.Create(plate, HUDIcon.Shape.Potion, 28f, new Vector2(196f, -69f), left);
        potions = UIKit.Text(plate, "Potions", "", 22f, new Vector2(60f, 28f), new Vector2(230f, -68f), left, FantasyUI.Parchment);
        UIKit.Text(plate, "PotionLabel", "Potions", 12f, new Vector2(80f, 14f), new Vector2(230f, -94f), left, FantasyUI.Muted).fontStyle = FontStyles.SmallCaps;
        UIKit.Keycap(plate, "H", new Vector2(-20f, -70f), Vector2.one);

        if (health != null) health.Changed += _ => Refresh();
        if (wallet != null) wallet.OnChanged += _ => Refresh();
        if (inventory != null) inventory.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (healthFill == null) return;
        if (health != null)
        {
            UIKit.SetBar(healthFill, health.Health / health.MaxHealth);
            healthValue.text = $"{Mathf.CeilToInt(health.Health)} / {Mathf.RoundToInt(health.MaxHealth)}";
        }
        coins.text = wallet != null ? wallet.Count.ToString() : "0";
        potions.text = inventory != null && inventory.HealingPotion != null ? inventory.Count(inventory.HealingPotion).ToString() : "0";
    }
}
