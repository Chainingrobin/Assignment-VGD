using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What a chest held, as a row of icons under the crosshair: a coin with the Wubucks it gave, a heart for a
// vitality relic, and each item's own glyph (flask, scroll) with its count. Pictures instead of a line of text.
public class LootToast : MonoBehaviour
{
    private static LootToast instance;
    private RectTransform row;
    private CanvasGroup group;
    private Coroutine routine;

    public static void Show(int wubucks, float vitality, IEnumerable<ItemData> items)
    {
        if (instance == null)
        {
            var canvas = UIKit.Canvas("Loot", 390, true);
            instance = canvas.gameObject.AddComponent<LootToast>();
            instance.group = canvas.gameObject.AddComponent<CanvasGroup>();
            instance.group.blocksRaycasts = false;
            instance.group.alpha = 0f;
            instance.row = UIKit.Rect(canvas.transform, "Row", new Vector2(0f, 120f), new Vector2(0f, -150f), new Vector2(0.5f, 0.5f));
        }
        foreach (Transform child in instance.row) Destroy(child.gameObject);
        var cards = new List<RectTransform>();
        if (vitality > 0f) cards.Add(instance.Card(slot => HUDIcon.Create(slot, HUDIcon.Shape.Heart, 54f, Vector2.zero, new Vector2(0.5f, 0.5f)), $"+{vitality:0}"));
        if (wubucks > 0) cards.Add(instance.Card(slot =>
        {
            var coin = UIKit.Rect(slot, "Coin", new Vector2(52f, 52f), Vector2.zero, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<CoinIcon>();
            coin.raycastTarget = false;
        }, $"+{wubucks}"));
        var counts = new Dictionary<ItemData, int>();
        foreach (var item in items)
            if (item != null) counts[item] = counts.TryGetValue(item, out int n) ? n + 1 : 1;
        foreach (var pair in counts)
            cards.Add(instance.Card(slot => UIKit.ItemGlyph(slot, pair.Key, 54f), $"+{pair.Value}"));
        if (cards.Count == 0) return;
        const float width = 100f, gap = 14f;
        float x = -(cards.Count * width + (cards.Count - 1) * gap) * 0.5f + width * 0.5f;
        foreach (var card in cards) { card.anchoredPosition = new Vector2(x, 0f); x += width + gap; }
        if (instance.routine != null) instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Run());
    }

    private RectTransform Card(System.Action<RectTransform> icon, string amount)
    {
        var card = UIKit.Rect(row, "Card", new Vector2(100f, 110f), Vector2.zero, new Vector2(0.5f, 0.5f));
        UIKit.Backing(card, 10f, 0.55f);
        var slot = UIKit.Rect(card, "Icon", new Vector2(64f, 64f), new Vector2(0f, -10f), new Vector2(0.5f, 1f));
        icon(slot);
        var label = UIKit.Text(card, "Amount", amount, 26f, new Vector2(100f, 32f), new Vector2(0f, 8f), new Vector2(0.5f, 0f),
            FantasyUI.Gold, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        return card;
    }

    private IEnumerator Run()
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            group.alpha = k;
            row.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        group.alpha = 1f;
        row.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(3.5f);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.4f; yield return null; }
        group.alpha = 0f;
        routine = null;
    }
}
