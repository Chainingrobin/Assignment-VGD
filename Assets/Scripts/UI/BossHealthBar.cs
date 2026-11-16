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
    private TMP_Text title, element;

    public static void Show(CombatTarget target, string name)
    {
        if (instance == null)
        {
            var hud = UIKit.HUDRoot();
            if (hud == null) return;
            var top = new Vector2(0.5f, 1f);
            var panel = UIKit.Rect(hud, "BossBar", new Vector2(760f, 84f), new Vector2(0f, -20f), top);
            FantasyUI.Panel(panel);
            instance = panel.gameObject.AddComponent<BossHealthBar>();
            instance.title = UIKit.Text(panel, "Name", "", 26f, new Vector2(520f, 32f), new Vector2(-90f, -10f), top,
                FantasyUI.Gold, TextAlignmentOptions.Center);
            instance.title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            instance.element = UIKit.Text(panel, "Element", "", 16f, new Vector2(200f, 24f), new Vector2(-24f, -16f),
                Vector2.one, FantasyUI.Parchment, TextAlignmentOptions.TopRight);
            instance.fill = UIKit.Bar(panel, "Health", new Vector2(712f, 16f), new Vector2(0f, -50f), top, Color.white);
            instance.fillImage = instance.fill.GetComponent<UnityEngine.UI.Image>();
        }
        instance.target = target;
        instance.title.text = name;
        instance.gameObject.SetActive(true);
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
        element.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{target.Affinity}</color>";
    }
}
