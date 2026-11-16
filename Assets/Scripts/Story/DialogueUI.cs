using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dialogue box view only: speaker name, portrait, body text and choice buttons, in the
// gilded HUD style. DialogueManager owns the logic; this class just draws what it is told.
public class DialogueUI : MonoBehaviour
{
    private Canvas canvas;
    private Image portrait;
    private RectTransform portraitFrame;
    private TMP_Text speaker, body, hint;
    private RectTransform choiceRoot;

    public TMP_Text Body => body;

    public static DialogueUI Build()
    {
        var canvas = UIKit.Canvas("Dialogue", 350, true);
        var ui = canvas.gameObject.AddComponent<DialogueUI>();
        ui.canvas = canvas;
        var bottom = new Vector2(0.5f, 0f);
        var panel = UIKit.Rect(canvas.transform, "Box", new Vector2(1240f, 240f), new Vector2(0f, 40f), bottom);
        FantasyUI.Panel(panel);
        var left = new Vector2(0f, 1f);
        ui.portraitFrame = UIKit.Rect(panel, "Portrait", new Vector2(176f, 176f), new Vector2(32f, -32f), left);
        var socket = ui.portraitFrame.gameObject.AddComponent<Image>();
        socket.sprite = FantasyUI.Socket;
        ui.portrait = UIKit.Rect(ui.portraitFrame, "Image", new Vector2(150f, 150f), Vector2.zero, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        ui.portrait.preserveAspect = true;
        ui.speaker = UIKit.Text(panel, "Speaker", "", 30f, new Vector2(900f, 40f), new Vector2(232f, -26f), left, FantasyUI.Gold);
        ui.speaker.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        ui.body = UIKit.Text(panel, "Body", "", 26f, new Vector2(960f, 150f), new Vector2(232f, -72f), left, FantasyUI.Parchment);
        ui.body.textWrappingMode = TextWrappingModes.Normal;
        ui.hint = UIKit.Text(panel, "Hint", "<color=#E8C77F>Space</color>  continue", 16f, new Vector2(300f, 24f),
            new Vector2(-24f, 16f), new Vector2(1f, 0f), FantasyUI.Muted, TextAlignmentOptions.BottomRight);
        ui.choiceRoot = UIKit.Rect(canvas.transform, "Choices", new Vector2(800f, 300f), new Vector2(0f, 300f), bottom);
        canvas.enabled = false;
        return ui;
    }

    public void Show(bool visible) => canvas.enabled = visible;

    public void SetLine(string name, Sprite face)
    {
        speaker.text = name;
        portrait.sprite = face;
        portraitFrame.gameObject.SetActive(face != null);
        // Without a portrait the text uses the full width.
        float x = face != null ? 232f : 40f;
        speaker.rectTransform.anchoredPosition = new Vector2(x, -26f);
        body.rectTransform.anchoredPosition = new Vector2(x, -72f);
        body.text = "";
    }

    public void ShowChoices(DialogueData.Choice[] choices, Action<DialogueData.Choice> picked)
    {
        ClearChoices();
        hint.gameObject.SetActive(false);
        var bottom = new Vector2(0.5f, 0f);
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            UIKit.Button(choiceRoot, $"{i + 1}.  {choice.text}", new Vector2(780f, 60f), new Vector2(0f, (choices.Length - 1 - i) * 72f), bottom,
                () => picked(choice));
        }
    }

    public void ClearChoices()
    {
        foreach (Transform child in choiceRoot) Destroy(child.gameObject);
        hint.gameObject.SetActive(true);
    }
}
