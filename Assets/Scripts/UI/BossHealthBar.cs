using TMPro;
using UnityEngine;

// The large top-centre bar shown during a boss fight: the boss's name and a health fill in
// its current element's colour (the final boss's colour changes with its phase, which tells
// the player what to switch to). Under it, a thin gold stagger meter fills with every hit;
// when it breaks the bar flashes white and drains over the stagger window (the time to punish),
// then sits dim while it recovers. No words: the fill and the flash say it.
public class BossHealthBar : MonoBehaviour
{
    private static readonly Color Gold = new(1f, 0.8f, 0.35f);
    private static BossHealthBar instance;
    private CombatTarget target;
    private BossBrain brain;
    private RectTransform fill, staggerFill;
    private UnityEngine.UI.Image fillImage, staggerImage;
    private TMP_Text title;
    private ElementIcon icon;

    public static void Show(CombatTarget target, string name)
    {
        if (instance == null)
        {
            var hud = UIKit.HUDRoot();
            if (hud == null) return;
            // Frameless, top centre: the name, then the bar in the boss's element colour, with that
            // element's glyph beside the name (no element word: the colour and glyph say it).
            var top = new Vector2(0.5f, 1f);
            var panel = UIKit.Rect(hud, "BossBar", new Vector2(720f, 62f), new Vector2(0f, -30f), top);
            UIKit.Backing(panel);
            instance = panel.gameObject.AddComponent<BossHealthBar>();
            instance.title = UIKit.Text(panel, "Name", "", 26f, new Vector2(620f, 32f), Vector2.zero, top,
                FantasyUI.Gold, TextAlignmentOptions.Center);
            instance.title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            instance.fill = UIKit.Bar(panel, "Health", new Vector2(720f, 12f), new Vector2(0f, -40f), top, Color.white);
            instance.staggerFill = UIKit.Bar(panel, "Stagger", new Vector2(420f, 6f), new Vector2(0f, -56f), top, Gold);
            instance.icon = UIKit.Rect(panel, "ElementIcon", new Vector2(30f, 30f), new Vector2(0f, -1f), new Vector2(0f, 1f))
                .gameObject.AddComponent<ElementIcon>();
            instance.icon.raycastTarget = false;
            instance.fillImage = instance.fill.GetComponent<UnityEngine.UI.Image>();
            instance.staggerImage = instance.staggerFill.GetComponent<UnityEngine.UI.Image>();
        }
        instance.target = target;
        instance.brain = target != null ? target.GetComponent<BossBrain>() : null;
        instance.title.text = name;
        instance.gameObject.SetActive(true);
        instance.Update(); // fill and colour now, so the first frame never shows the previous boss's bar
    }

    public static void Hide()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (target == null) { gameObject.SetActive(false); return; }
        UIKit.SetBar(fill, target.Health / target.MaxHealth);
        var color = ElementVisuals.ColorFor(target.Affinity);
        fillImage.color = color;
        icon.Element = target.Affinity;
        icon.color = color;
        staggerFill.parent.gameObject.SetActive(brain != null);
        if (brain == null) return;
        float window = brain.StaggerWindow;
        if (window > 0f)
        {
            // Broken: a bright pulse draining over the punish window.
            UIKit.SetBar(staggerFill, window / BossBrain.StaggerSeconds);
            staggerImage.color = Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
        }
        else
        {
            UIKit.SetBar(staggerFill, brain.StaggerFill);
            staggerImage.color = brain.StaggerResting ? Gold * new Color(1f, 1f, 1f, 0.3f) : Gold;
        }
    }
}
