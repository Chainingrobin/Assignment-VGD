using System.Collections;
using TMPro;
using UnityEngine;

// One-line spoken or system text at the bottom of the screen (Wulang's barks, locked-path
// lines, boss last words, shop feedback). A new line replaces the current one.
public class Subtitle : MonoBehaviour
{
    private static Subtitle instance;
    private TMP_Text line;
    private CanvasGroup group;
    private Coroutine routine;

    public static void Show(string text, float seconds = 3.5f)
    {
        if (instance == null)
        {
            var canvas = UIKit.Canvas("Subtitles", 400, true);
            instance = canvas.gameObject.AddComponent<Subtitle>();
            instance.group = canvas.gameObject.AddComponent<CanvasGroup>();
            instance.group.blocksRaycasts = false;
            instance.line = UIKit.Text(canvas.transform, "Line", "", 28f, new Vector2(1300f, 90f), new Vector2(0f, 230f),
                new Vector2(0.5f, 0f), FantasyUI.Parchment, TextAlignmentOptions.Bottom);
            instance.line.textWrappingMode = TextWrappingModes.Normal;
            instance.line.outlineWidth = 0.2f;
            instance.line.outlineColor = new Color32(10, 6, 3, 255);
        }
        if (instance.routine != null) instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Run(text, seconds));
    }

    private IEnumerator Run(string text, float seconds)
    {
        line.text = text;
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.4f;
            yield return null;
        }
        group.alpha = 0f;
        routine = null;
    }
}
