using TMPro;
using UnityEngine;

public class CombatHUD : MonoBehaviour
{
    [SerializeField] private SpellCaster caster;
    [SerializeField] private TMP_Text projectileCount;
    [SerializeField] private TMP_Text areaCount;
    [SerializeField] private TMP_Text reloadLabel;
    [SerializeField] private TMP_Text recoveryLabel;
    [SerializeField] private UnityEngine.UI.Image reloadFill;
    [SerializeField] private UnityEngine.UI.Image recoveryFill;
    private int previousProjectiles = -1;
    private int previousAreas = -1;
    private float projectilePulse;
    private float areaPulse;

    private void Start()
    {
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = TMP_Settings.defaultFontAsset;
            label.color = label == projectileCount || label == areaCount ? FantasyUI.Parchment : FantasyUI.Gold;
            label.characterSpacing = 2f;
            label.outlineWidth = 0.15f;
            label.outlineColor = new Color32(12, 7, 4, 255);
        }
        // The cards are scene-authored flat panels; dress them as gilded plaques.
        for (int i = 0; i < transform.childCount; i++) FantasyUI.Panel((RectTransform)transform.GetChild(i));
        foreach (var fill in new[] { reloadFill, recoveryFill })
        {
            fill.color = FantasyUI.Gold;
            fill.transform.parent.GetComponent<UnityEngine.UI.Image>().color = FantasyUI.Inset;
        }
        // Name the spells and show their keys as keycaps, like the rest of the HUD.
        Label(projectileCount.transform.parent, "Bolt", "LMB");
        Label(areaCount.transform.parent, "Burst", "RMB");
    }

    private static void Label(Transform card, string spell, string key)
    {
        var caption = card.Find("Caption")?.GetComponent<TMP_Text>();
        if (caption != null)
        {
            caption.text = spell;
            caption.fontSize = 15f;
            caption.fontStyle = FontStyles.SmallCaps;
        }
        UIKit.Keycap(card, key, new Vector2(-10f, -8f), Vector2.one);
        // Count on its own row under the name; status text bottom-right above the bar.
        Place(card.Find("Count"), new Vector2(12f, -30f), new Vector2(72f, 26f), TextAlignmentOptions.Left, 22f);
        Place(card.Find("Recovery"), new Vector2(66f, -36f), new Vector2(68f, 18f), TextAlignmentOptions.Right, 12f);
    }

    private static void Place(Transform item, Vector2 position, Vector2 size, TextAlignmentOptions alignment, float fontSize)
    {
        if (item == null) return;
        var rect = (RectTransform)item;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = item.GetComponent<TMP_Text>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Update()
    {
        if (caster == null) return;
        var charges = caster.Charges;
        if (previousProjectiles != charges.Projectiles) projectilePulse = 1f;
        if (previousAreas != charges.Areas) areaPulse = 1f;
        previousProjectiles = charges.Projectiles;
        previousAreas = charges.Areas;
        projectilePulse = Mathf.MoveTowards(projectilePulse, 0f, Time.deltaTime * 4f);
        areaPulse = Mathf.MoveTowards(areaPulse, 0f, Time.deltaTime * 4f);
        projectileCount.transform.localScale = Vector3.one * (1f + projectilePulse * 0.18f);
        areaCount.transform.localScale = Vector3.one * (1f + areaPulse * 0.18f);
        projectileCount.text = $"{charges.Projectiles}/15";
        areaCount.text = $"{charges.Areas}/5";
        reloadLabel.text = charges.IsReloading ? $"{charges.ReloadRemaining:0.0}s" : "<color=#E8C77F>R</color>  Reload";
        recoveryLabel.text = "";
        reloadFill.transform.parent.gameObject.SetActive(charges.IsReloading);
        recoveryFill.transform.parent.gameObject.SetActive(charges.Areas < SpellCharges.AreaCapacity);
        reloadFill.fillAmount = charges.IsReloading ? 1f - charges.ReloadRemaining / SpellCharges.ReloadDuration : charges.Projectiles / 15f;
        recoveryFill.fillAmount = charges.Areas == SpellCharges.AreaCapacity ? 1f : charges.RecoveryElapsed / SpellCharges.AreaRecoveryDuration;
    }
}
