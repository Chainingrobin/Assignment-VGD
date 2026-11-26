using TMPro;
using UnityEngine;

// The large top-centre bar shown during a boss fight: the boss's name and a health fill in
// its current element's colour (the final boss's colour changes with its phase, which tells
// the player what to switch to).
public class BossHealthBar : MonoBehaviour
{
    private static BossHealthBar instance;
    private CombatTarget target;
    private RectTransform fill;
    private UnityEngine.UI.Image fillImage;
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
            var panel = UIKit.Rect(hud, "BossBar", new Vector2(720f, 52f), new Vector2(0f, -30f), top);
            UIKit.Backing(panel);
            instance = panel.gameObject.AddComponent<BossHealthBar>();
            instance.title = UIKit.Text(panel, "Name", "", 26f, new Vector2(620f, 32f), Vector2.zero, top,
                FantasyUI.Gold, TextAlignmentOptions.Center);
            instance.title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            instance.fill = UIKit.Bar(panel, "Health", new Vector2(720f, 12f), new Vector2(0f, -40f), top, Color.white);
            instance.icon = UIKit.Rect(panel, "ElementIcon", new Vector2(30f, 30f), new Vector2(0f, -1f), new Vector2(0f, 1f))
                .gameObject.AddComponent<ElementIcon>();
            instance.icon.raycastTarget = false;
            instance.fillImage = instance.fill.GetComponent<UnityEngine.UI.Image>();
        }
        instance.target = target;
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
    }
}
