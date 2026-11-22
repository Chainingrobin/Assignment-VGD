using System;
using System.Collections;
using UnityEngine;

// Wulang's health (100 HP). Enemies, bosses and hazards call TakeDamage; potions call Heal.
// Shows a gilded vitality bar on the HUD and a red flash when hurt. On death the screen
// fades, she wakes at the current region's spawn at full health, and Respawned fires so
// boss encounters can reset.
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField, Min(1f)] private float maxHealth = 100f;

    // Each vitality relic found (a story flag, so it is saved with the checkpoint) adds maximum health.
    public const string VitalityPrefix = "vitality: ";
    public const float VitalityPerRelic = 10f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    // Set while dashing through danger or during scripted moments.
    public bool Invulnerable { get; set; }

    public event Action Died;
    public event Action Respawned;
    public event Action<float> Changed;

    private CanvasGroup flash;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        Instance = this;
        var story = StoryStateManager.Instance;
        foreach (var flag in story.Flags)
            if (flag.StartsWith(VitalityPrefix)) maxHealth += VitalityPerRelic;
        story.OnFlagSet += OnFlagSet;
        Health = maxHealth;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlagSet;
    }

    private void OnFlagSet(string flag)
    {
        if (!flag.StartsWith(VitalityPrefix)) return;
        maxHealth += VitalityPerRelic;
        Health = Mathf.Min(maxHealth, Health + VitalityPerRelic);
        Refresh();
    }

    private void Start()
    {
        // The vitality bar lives on the shared status plate.
        if (GetComponent<StatusHUD>() == null) gameObject.AddComponent<StatusHUD>();
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
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
    }
}
