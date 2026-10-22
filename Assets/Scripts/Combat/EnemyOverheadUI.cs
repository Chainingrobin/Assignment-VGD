using TMPro;
using UnityEngine;

[RequireComponent(typeof(CombatTarget), typeof(EnemyDetection))]
[DefaultExecutionOrder(100)]
public class EnemyOverheadUI : MonoBehaviour
{
    private CombatTarget target;
    private EnemyDetection detection;
    private Camera playerCamera;
    private RectTransform content;
    private RectTransform badge;
    private CanvasGroup visibility;
    private UnityEngine.UI.Image health;
    private TMP_Text alert;
    private TMP_Text feedback;
    public bool AlertVisible => alert != null && alert.gameObject.activeSelf && visibility.alpha > 0f;

    private void Start()
    {
        target = GetComponent<CombatTarget>();
        detection = GetComponent<EnemyDetection>();
        playerCamera = Camera.main;
        var hud = FindFirstObjectByType<ResponsiveHUD>();
        if (hud == null || playerCamera == null) return;
        content = hud.Content;
        badge = Rect(content, name + " overhead", new Vector2(144, 72), Vector2.zero);
        badge.anchorMin = badge.anchorMax = content.pivot;
        visibility = badge.gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = visibility.interactable = false;
        var track = Rect(badge, "Health", new Vector2(124, 7), new Vector2(0, 0));
        Image(track, new Color(0.16f, 0.1f, 0.045f, 0.95f));
        var gold = new Color(0.85f, 0.65f, 0.3f);
        foreach (float side in new[] { -1f, 1f })
        {
            var gem = Rect(track, "Gilded tip", new Vector2(9, 9), new Vector2(side * 66, 0));
            gem.localRotation = Quaternion.Euler(0, 0, 45);
            Image(gem, gold);
        }
        var fill = Rect(track, "Fill", Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(1, 1);
        fill.offsetMax = new Vector2(-1, -1);
        health = Image(fill, ElementVisuals.ColorFor(target.Affinity));
        alert = Text(badge, "Detected", "!", 38, new Vector2(0, 60));
        alert.fontStyle = FontStyles.Bold;
        alert.color = new Color(1f, 0.8f, 0.15f);
        feedback = Text(badge, "Hit", "", 20, new Vector2(0, 15));
        alert.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (badge == null || playerCamera == null) return;
        Vector3 point = playerCamera.WorldToScreenPoint(target.HeadPosition + Vector3.up * 0.3f);
        bool visible = !target.IsDead && point.z > 0f && point.z < 28f && point.x > 0f && point.x < Screen.width && point.y > 0f && point.y < Screen.height;
        visibility.alpha = visible ? 1f : 0f;
        alert.gameObject.SetActive(visible && detection.CanSeePlayer);
        if (!visible) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, point, null, out Vector2 local);
        badge.anchoredPosition = local;
        health.rectTransform.anchorMax = new Vector2(Mathf.Lerp(health.rectTransform.anchorMax.x, Mathf.Clamp01(target.Health / target.MaxHealth), 1f - Mathf.Exp(-14f * Time.deltaTime)), 1f);
        float age = Time.time - target.LastHitTime;
        feedback.text = age < 1.8f && float.TryParse(target.LastFeedback, out _) ? target.LastFeedback : "";
        feedback.fontSize = 22f + 6f * Mathf.Clamp01(1f - age / 0.2f);
        feedback.rectTransform.anchoredPosition = new Vector2(0, 16 + Mathf.Clamp01(age / 1.8f) * 18);
        feedback.alpha = Mathf.Clamp01((1.8f - age) / 0.5f);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static UnityEngine.UI.Image Image(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size, Vector2 position)
    {
        var text = Rect(parent, name, new Vector2(200, 38), position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 230);
        text.raycastTarget = false;
        return text;
    }

    private void OnDisable()
    {
        if (badge != null) badge.gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        if (badge != null) badge.gameObject.SetActive(true);
    }
    private void OnDestroy()
    {
        if (badge != null) Destroy(badge.gameObject);
    }
}
