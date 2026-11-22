using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small builders for the code-made gilded UI, so every new panel matches the existing HUD
// (FantasyUI frames, parchment text) without hand-authored prefabs.
public static class UIKit
{
    // A screen-space canvas scaled like the HUD (1920x1080 reference).
    public static Canvas Canvas(string name, int sortingOrder, bool persistent = false)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (persistent) UnityEngine.Object.DontDestroyOnLoad(go);
        return canvas;
    }

    // The HUD's safe-area root, so HUD widgets follow the same layout as the existing ones.
    public static RectTransform HUDRoot()
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<ResponsiveHUD>();
        return hud != null ? hud.Content : null;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position, Vector2 anchor)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static RectTransform Stretch(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Fill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(Transform parent, string name, string content, float size, Vector2 boxSize, Vector2 position,
        Vector2 anchor, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        var text = Rect(parent, name, boxSize, position, anchor).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; // callers opt in to wrapping for paragraphs
        text.text = content;
        return text;
    }

    // A horizontal bar; returns the fill rect, sized by SetBar.
    public static RectTransform Bar(Transform parent, string name, Vector2 size, Vector2 position, Vector2 anchor, Color color)
    {
        var back = Rect(parent, name, size, position, anchor);
        Fill(back, FantasyUI.Inset);
        var fill = Stretch(back, "Fill");
        Fill(fill, color);
        return fill;
    }

    // A small gold-rimmed key label, e.g. [H] or [LMB], used for every input hint.
    public static RectTransform Keycap(Transform parent, string key, Vector2 position, Vector2 anchor)
    {
        float width = Mathf.Max(28f, 14f + key.Length * 11f);
        var cap = Rect(parent, "Key " + key, new Vector2(width, 26f), position, anchor);
        Fill(cap, FantasyUI.Gold * new Color(0.75f, 0.75f, 0.75f, 1f));
        var inner = Stretch(cap, "Face");
        inner.offsetMin = new Vector2(1.5f, 1.5f);
        inner.offsetMax = new Vector2(-1.5f, -1.5f);
        Fill(inner, new Color(0.08f, 0.055f, 0.04f, 1f));
        var label = Text(cap, "Label", key, 15f, new Vector2(width, 26f), Vector2.zero, new Vector2(0.5f, 0.5f), FantasyUI.Gold, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        return cap;
    }

    // An input hint, [Key] Verb: the keycap carries the key, the word only the action.
    public static RectTransform Hint(Transform parent, string key, string verb, Vector2 position, Vector2 anchor, float fontSize = 18f)
    {
        var hint = Rect(parent, "Hint " + verb, Vector2.zero, position, anchor);
        var middle = new Vector2(0f, 0.5f);
        var cap = Keycap(hint, key, Vector2.zero, middle);
        float gap = cap.sizeDelta.x + 10f;
        var label = Text(hint, "Verb", verb, fontSize, new Vector2(400f, 28f), new Vector2(gap, 0f), middle, FantasyUI.Parchment,
            TextAlignmentOptions.MidlineLeft);
        float width = label.GetPreferredValues(verb).x + 4f;
        label.rectTransform.sizeDelta = new Vector2(width, 28f);
        hint.sizeDelta = new Vector2(gap + width, 28f);
        return hint;
    }

    // An item's picture, centred in `parent`: its own icon if it has one, otherwise the matching vector
    // glyph (a flask for potions, the element's sigil for tomes, a scroll for lore, a scroll in a
    // ghost-blue glow for memories).
    public static void ItemGlyph(Transform parent, ItemData item, float size)
    {
        var center = new Vector2(0.5f, 0.5f);
        if (item.icon != null)
        {
            var image = Rect(parent, "Icon", Vector2.one * size, Vector2.zero, center).gameObject.AddComponent<Image>();
            image.sprite = item.icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return;
        }
        if (item.category == ItemCategory.Tome)
        {
            var sigil = Rect(parent, "Icon", Vector2.one * size, Vector2.zero, center).gameObject.AddComponent<ElementIcon>();
            sigil.Element = item.element;
            sigil.color = ElementVisuals.ColorFor(item.element);
            sigil.raycastTarget = false;
            return;
        }
        if (item.category == ItemCategory.Memory)
            Fill(Rect(parent, "Memory glow", Vector2.one * size * 1.6f, Vector2.zero, center), new Color(0.45f, 0.7f, 1f, 0.8f)).sprite = FantasyUI.Glow;
        HUDIcon.Create(parent, item.category == ItemCategory.Consumable ? HUDIcon.Shape.Potion : HUDIcon.Shape.Quest, size, Vector2.zero, center);
    }

    // A soft dark halo behind a frameless HUD widget, so it reads over bright sky or snow without a box.
    public static Image Backing(RectTransform widget, float spread = 22f, float alpha = 0.5f)
    {
        var halo = Stretch(widget, "Backing");
        halo.offsetMin = new Vector2(-spread, -spread);
        halo.offsetMax = new Vector2(spread, spread);
        halo.SetAsFirstSibling();
        var image = Fill(halo, new Color(0f, 0f, 0f, alpha));
        image.sprite = FantasyUI.Soft;
        image.type = Image.Type.Sliced;
        return image;
    }

    public static void SetBar(RectTransform fill, float fraction) =>
        fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    public static Button Button(Transform parent, string label, Vector2 size, Vector2 position, Vector2 anchor, Action onClick)
    {
        var rect = Rect(parent, label + " Button", size, position, anchor);
        var image = FantasyUI.Panel(rect);
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        var text = Text(rect, "Label", label, 26f, size, Vector2.zero, new Vector2(0.5f, 0.5f), FantasyUI.Parchment, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }
}
