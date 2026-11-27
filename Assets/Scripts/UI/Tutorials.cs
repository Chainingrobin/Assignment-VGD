using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Short how-to tips that appear once each, the first time they matter: casting at the start, reloading
// when bolts run low, switching once a second element is claimed, chaining elements, staggering a guardian,
// drinking a potion when hurt and reading the journal. A tip is a small card on the left of the HUD: its
// keys as keycaps and one line of explanation. Tips queue, wait out cutscenes and conversations, and are
// remembered between sessions (PlayerPrefs), so they never nag. Every tip can be read again on the Guide
// page of the satchel (InventoryUI).
public class Tutorials : MonoBehaviour
{
    public struct Tip
    {
        public string id, title, body;
        public (string key, string verb)[] keys;
    }

    public static readonly Tip[] All =
    {
        new() { id = "cast", title = "Spells", keys = new[] { ("LMB", "Bolt"), ("RMB", "Burst") },
            body = "Bolts are quick shots. A burst strikes everything around you." },
        new() { id = "reload", title = "Charges", keys = new[] { ("R", "Reload") },
            body = "The outer ring is your 15 bolts: R refills them in 3 seconds. The inner ring is 5 bursts; one comes back every 10 seconds." },
        new() { id = "switch", title = "Elements", keys = new[] { ("Q", "Previous"), ("E", "Next") },
            body = "Switch elements. Every guardian is weak to one of them." },
        new() { id = "combo", title = "Chain elements",
            body = "Hits leave a mark, and bursts leave it twice as long. Fire melts a frozen foe, Ice shocks a burning one, and Earth with Lightning makes a Storm Quake." },
        new() { id = "stagger", title = "Stagger",
            body = "Hits fill the gold bar under a guardian's health; bursts and weaknesses fill it fastest. When it breaks, the guardian is stunned and takes extra damage." },
        new() { id = "potion", title = "Potions", keys = new[] { ("H", "Drink") },
            body = "A potion heals you. Abe sells more for Wubucks." },
        new() { id = "journal", title = "Journal", keys = new[] { ("Tab", "Satchel and journal") },
            body = "Memories and the pages you find are kept in your journal." },
    };

    private const float Seconds = 8f;
    private static Tutorials instance;
    private readonly Queue<Tip> pending = new();
    private RectTransform card;
    private CanvasGroup group;
    private SpellCaster caster;
    private bool castShown;

    private static bool Seen(string id) => PlayerPrefs.GetInt("tip: " + id, 0) == 1;

    /// <summary>Queues a tip unless it has been shown before.</summary>
    public static void Show(string id)
    {
        if (instance == null || Seen(id)) return;
        foreach (var queued in instance.pending) if (queued.id == id) return;
        foreach (var tip in All)
            if (tip.id == id) instance.pending.Enqueue(tip);
    }

    private void Awake() => instance = this;
    private void OnDestroy() { if (instance == this) instance = null; }

    private IEnumerator Start()
    {
        caster = FindFirstObjectByType<SpellCaster>();
        while (true)
        {
            Watch();
            if (pending.Count > 0 && !PlayerLock.IsLocked && UIKit.HUDRoot() != null)
            {
                var tip = pending.Dequeue();
                if (!Seen(tip.id))
                {
                    PlayerPrefs.SetInt("tip: " + tip.id, 1);
                    PlayerPrefs.Save();
                    yield return Present(tip);
                }
            }
            yield return null;
        }
    }

    // The tips that follow from what the player is doing rather than from a story event.
    private void Watch()
    {
        if (PlayerLock.IsLocked) return;
        if (!castShown) { castShown = true; Show("cast"); }
        if (caster != null && caster.Charges.Projectiles <= 5) Show("reload");
        var health = PlayerHealth.Instance;
        var inventory = PlayerInventory.Instance;
        if (health != null && inventory != null && health.Health < health.MaxHealth * 0.5f && inventory.Count(inventory.HealingPotion) > 0) Show("potion");
        if (inventory != null)
            foreach (var item in inventory.Items)
                if (item.category is ItemCategory.Lore or ItemCategory.Memory) { Show("journal"); break; }
    }

    private IEnumerator Present(Tip tip)
    {
        var hud = UIKit.HUDRoot();
        if (card != null) Destroy(card.gameObject);
        card = UIKit.Rect(hud, "Tip", new Vector2(420f, 120f), new Vector2(40f, 60f), new Vector2(0f, 0.5f));
        UIKit.Backing(card, 18f, 0.6f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var left = new Vector2(0f, 1f);
        UIKit.Text(card, "Title", tip.title, 22f, new Vector2(420f, 28f), Vector2.zero, left, FantasyUI.Gold).fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        float y = -34f, x = 0f;
        if (tip.keys != null)
        {
            foreach (var (key, verb) in tip.keys)
            {
                var hint = UIKit.Hint(card, key, verb, new Vector2(x, y), left, 18f);
                hint.pivot = new Vector2(0f, 1f);
                x += hint.sizeDelta.x + 24f;
            }
            y -= 36f;
        }
        var body = UIKit.Text(card, "Body", tip.body, 18f, new Vector2(420f, 90f), new Vector2(0f, y), left, FantasyUI.Parchment);
        body.textWrappingMode = TextWrappingModes.Normal;
        card.sizeDelta = new Vector2(420f, -y + body.GetPreferredValues(tip.body, 420f, 0f).y);
        GameAudio.Play("Sfx/Coin", null, 0.25f, 1.4f);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.25f; yield return null; }
        group.alpha = 1f;
        // Stays for its time (paused while a cutscene or menu covers the HUD), or until the next tip is waiting.
        for (float t = 0f; t < Seconds && !(pending.Count > 0 && t > 4f); t += PlayerLock.IsLocked ? 0f : Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.4f; yield return null; }
        Destroy(card.gameObject);
    }

    /// <summary>A tip as plain text for the Guide page: its keys on the first line, then the explanation.</summary>
    public static string Describe(Tip tip)
    {
        var keys = new List<string>();
        if (tip.keys != null) foreach (var (key, verb) in tip.keys) keys.Add($"<color=#E8C77F><b>[{key}]</b></color> {verb}");
        return (keys.Count > 0 ? string.Join("     ", keys) + "\n\n" : "") + tip.body;
    }
}
