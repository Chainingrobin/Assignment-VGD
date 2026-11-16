using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Wulang's health (100 HP). Enemies, bosses and hazards call TakeDamage; potions call Heal.
// Shows a gilded vitality bar on the HUD and a red flash when hurt. On death the screen
// fades, she wakes at the current region's spawn at full health, and Respawned fires so
// boss encounters can reset.
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField, Min(1f)] private float maxHealth = 100f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    // Set while dashing through danger or during scripted moments.
    public bool Invulnerable { get; set; }

    public event Action Died;
    public event Action Respawned;
    public event Action<float> Changed;

    private RectTransform barFill;
    private TMP_Text barLabel;
    private CanvasGroup flash;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        Instance = this;
        Health = maxHealth;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        var top = new Vector2(0f, 1f);
        var panel = UIKit.Rect(hud, "VitalityHUD", new Vector2(320f, 58f), new Vector2(24f, -92f), top);
        FantasyUI.Panel(panel);
        UIKit.Text(panel, "Caption", "Vitality", 12f, new Vector2(200f, 18f), new Vector2(18f, -8f), top, FantasyUI.Muted);
        barLabel = UIKit.Text(panel, "Value", "", 14f, new Vector2(100f, 18f), new Vector2(-18f, -8f), Vector2.one,
            FantasyUI.Parchment, TextAlignmentOptions.TopRight);
        barFill = UIKit.Bar(panel, "Bar", new Vector2(284f, 12f), new Vector2(18f, -32f), top, new Color(0.75f, 0.12f, 0.1f));

        var flashRect = UIKit.Stretch(hud, "HurtFlash");
        UIKit.Fill(flashRect, new Color(0.6f, 0f, 0f, 0.35f));
        flash = flashRect.gameObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        flash.blocksRaycasts = false;
        Refresh();
    }

    private void Update()
    {
        if (flash != null && flash.alpha > 0f) flash.alpha = Mathf.MoveTowards(flash.alpha, 0f, Time.deltaTime * 2.5f);
    }

    public void TakeDamage(float amount, ElementType element)
    {
        // No damage during cutscenes, dialogue or the shop.
        if (IsDead || Invulnerable || PlayerLock.IsLocked || !float.IsFinite(amount) || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - amount);
        if (flash != null) flash.alpha = 1f;
        Refresh();
        if (Health <= 0f) StartCoroutine(Die());
    }

    public bool Heal(float amount)
    {
        if (IsDead || Health >= maxHealth) return false;
        Health = Mathf.Min(maxHealth, Health + amount);
        Refresh();
        return true;
    }

    public void HealFull()
    {
        Health = maxHealth;
        Refresh();
    }

    private IEnumerator Die()
    {
        IsDead = true;
        Died?.Invoke();
        PlayerLock.Push();
        var overlay = TransitionOverlay.Instance;
        yield return overlay.FadeTo(1f, 1.2f, Color.black);
        var regions = RegionManager.Instance;
        if (regions != null && regions.CurrentSpawn != null) regions.MoveToSpawn(regions.Current);
        else
        {
            var body = GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (body != null) body.enabled = true;
        }
        HealFull();
        IsDead = false;
        Respawned?.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return overlay.FadeTo(0f, 1f);
        PlayerLock.Pop();
    }

    private void Refresh()
    {
        Changed?.Invoke(Health);
        if (barFill == null) return;
        UIKit.SetBar(barFill, Health / maxHealth);
        barLabel.text = $"{Mathf.CeilToInt(Health)} / {Mathf.RoundToInt(maxHealth)}";
    }
}
