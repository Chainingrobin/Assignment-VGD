using TMPro;
using UnityEngine;

// Bottom-left vitals, frameless and icon-first: a heart and its bar, the healing flask with its
// count and [H], and the Wubucks purse. No word labels: the heart, flask and coin say what they
// are. The HUD speaks up only when it matters: the purse shows when it changes (it is always in
// the satchel and the shop), and the flask pulses when health runs low and a potion is at hand.
public class StatusHUD : MonoBehaviour
{
    private const float LowHealth = 0.35f;
    private const float PurseSeconds = 3f;

    private RectTransform healthFill, flask;
    private TMP_Text coins, potions;
    private CanvasGroup purse;
    private float purseShownAt = -100f;
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

        var vitals = UIKit.Rect(hud, "Vitals", new Vector2(330f, 74f), new Vector2(36f, 32f), Vector2.zero);
        UIKit.Backing(vitals);
        var left = new Vector2(0f, 1f);
        HUDIcon.Create(vitals, HUDIcon.Shape.Heart, 26f, Vector2.zero, left);
        healthFill = UIKit.Bar(vitals, "Health", new Vector2(286f, 10f), new Vector2(40f, -8f), left, new Color(0.78f, 0.14f, 0.12f));

        flask = HUDIcon.Create(vitals, HUDIcon.Shape.Potion, 30f, new Vector2(-2f, -40f), left).rectTransform;
        potions = UIKit.Text(vitals, "Potions", "", 22f, new Vector2(40f, 30f), new Vector2(34f, -42f), left, FantasyUI.Parchment);
        UIKit.Keycap(vitals, "H", new Vector2(70f, -44f), left);

        var purseRect = UIKit.Rect(vitals, "Purse", new Vector2(120f, 30f), new Vector2(130f, -42f), left);
        purse = purseRect.gameObject.AddComponent<CanvasGroup>();
        purse.alpha = 0f;
        UIKit.Rect(purseRect, "Coin", new Vector2(24f, 24f), new Vector2(0f, -3f), left).gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        coins = UIKit.Text(purseRect, "Wubucks", "", 20f, new Vector2(90f, 28f), new Vector2(32f, -2f), left, FantasyUI.Parchment);

        if (health != null) health.Changed += _ => Refresh();
        if (wallet != null) wallet.OnChanged += _ => { Refresh(); purseShownAt = Time.unscaledTime; };
        if (inventory != null) inventory.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (healthFill == null) return;
        if (health != null) UIKit.SetBar(healthFill, health.Health / health.MaxHealth);
        coins.text = wallet != null ? wallet.Count.ToString() : "0";
        potions.text = PotionCount().ToString();
    }

    private int PotionCount() => inventory != null && inventory.HealingPotion != null ? inventory.Count(inventory.HealingPotion) : 0;

    private void Update()
    {
        if (purse == null) return;
        purse.alpha = Mathf.Clamp01(1f - (Time.unscaledTime - purseShownAt - PurseSeconds) / 0.6f);
        bool low = health != null && health.Health / health.MaxHealth < LowHealth && PotionCount() > 0;
        flask.localScale = Vector3.one * (low ? 1f + 0.2f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f);
    }
}
