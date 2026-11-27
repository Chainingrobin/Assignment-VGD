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
    private Unity.Cinemachine.CinemachineImpulseSource kick;
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
        if (GetComponent<Tutorials>() == null) gameObject.AddComponent<Tutorials>();
        if (GetComponent<PlayerBodyView>() == null) gameObject.AddComponent<PlayerBodyView>();
        Kick();
        var hud = UIKit.HUDRoot();
        if (hud == null) return;
        // Hurt: the screen's edges flush red (a vignette, so the middle of the view stays clear).
        var flashRect = UIKit.Stretch(hud, "HurtFlash");
        var vignette = UIKit.Fill(flashRect, new Color(0.75f, 0.02f, 0f, 0.7f));
        vignette.sprite = Vignette();
        flash = flashRect.gameObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        flash.blocksRaycasts = false;
        Refresh();
    }

    private void Update()
    {
        if (flash != null && flash.alpha > 0f) flash.alpha = Mathf.MoveTowards(flash.alpha, 0f, Time.deltaTime * 1.6f);
    }

    // A short camera jolt on every hit: an impulse source on Wulang and a listener on her first-person camera.
    private void Kick()
    {
        foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
        {
            // Her view camera rides under her (PlayerViewCamera) rather than following a target.
            bool hers = camera.transform.IsChildOf(transform) || (camera.Follow != null && camera.Follow.IsChildOf(transform));
            if (!hers || camera.GetComponent<Unity.Cinemachine.CinemachineImpulseListener>() != null) continue;
            var listener = camera.gameObject.AddComponent<Unity.Cinemachine.CinemachineImpulseListener>();
            // Added at runtime, so Reset never filled in the defaults.
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.UseCameraSpace = true;
            listener.ApplyAfter = Unity.Cinemachine.CinemachineCore.Stage.Noise;
        }
        kick = gameObject.AddComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        kick.ImpulseDefinition.ImpulseChannel = 1;
        kick.ImpulseDefinition.ImpulseShape = Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Recoil;
        kick.ImpulseDefinition.ImpulseType = Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        kick.ImpulseDefinition.ImpulseDuration = 0.18f;
        kick.DefaultVelocity = new Vector3(0.6f, -1f, 0f);
    }

    // Transparent in the middle, solid at the corners.
    private static Sprite Vignette()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.4f, Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Story mode (main menu): half damage, for players who are here for the story (Lecture 4).
    public static bool StoryMode
    {
        get => PlayerPrefs.GetInt("storyMode", 0) == 1;
        set { PlayerPrefs.SetInt("storyMode", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public void TakeDamage(float amount, ElementType element)
    {
        // No damage during cutscenes, dialogue or the shop.
        if (IsDead || Invulnerable || PlayerLock.IsLocked || !float.IsFinite(amount) || amount <= 0f) return;
        if (StoryMode) amount *= 0.5f;
        Health = Mathf.Max(0f, Health - amount);
        GameAudio.Play("Sfx/Hurt", null, 0.9f);
        if (flash != null) flash.alpha = Mathf.Max(flash.alpha, Mathf.Clamp(amount / 20f, 0.6f, 1f));
        if (kick != null) kick.GenerateImpulseWithForce(Mathf.Clamp(amount / 15f, 0.4f, 1.5f));

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
        GameAudio.Play("Sfx/Death", null, 0.8f);
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
