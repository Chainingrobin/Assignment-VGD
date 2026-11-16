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
