using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dialogue box, display only (the "display" part of the guide's Step 3): speaker name, portrait, body text,
/// a "[Space] Continue" hint and the choice buttons, in the game's gilded parchment style.
/// </summary>
/// <remarks>
/// <para><b>Story role (GDD section 5).</b> The box carries the characters' voices: the name and portrait say who is
/// speaking, the typewriter pace gives each line weight, and the numbered choice buttons are where the player makes
/// the game's one decisive choice.</para>
/// <para><b>No logic here.</b> <see cref="DialogueManager"/> decides what to show and when; this class only draws what it
/// is told, so the look can change without touching the dialogue flow.</para>
/// <para>Built in code by <see cref="Build"/> (no prefab), on its own canvas above the HUD.</para>
/// </remarks>
public class DialogueUI : MonoBehaviour
{
    private Canvas canvas;
    private Image portrait;
    private RectTransform portraitFrame;
    private TMP_Text speaker, body;
    private RectTransform choiceRoot, hint;

    /// <summary>The body text. <see cref="DialogueManager"/> drives its typewriter reveal through <c>maxVisibleCharacters</c>.</summary>
    public TMP_Text Body => body;

    /// <summary>Creates the dialogue canvas, hidden until a conversation starts.</summary>
    /// <returns>The view, ready for <see cref="Show"/> and <see cref="SetLine"/>.</returns>
    public static DialogueUI Build()
    {
        var canvas = UIKit.Canvas("Dialogue", 350, true);
        var ui = canvas.gameObject.AddComponent<DialogueUI>();
        ui.canvas = canvas;
        var bottom = new Vector2(0.5f, 0f);
        // A wide box along the bottom edge, like a film subtitle band: it leaves the scene visible above.
        var panel = UIKit.Rect(canvas.transform, "Box", new Vector2(1240f, 240f), new Vector2(0f, 40f), bottom);
        FantasyUI.Panel(panel);
        var left = new Vector2(0f, 1f);
        // The portrait sits in a round gem socket, clipped to the socket's inner circle.
        ui.portraitFrame = UIKit.Rect(panel, "Portrait", new Vector2(190f, 190f), new Vector2(26f, -25f), left);
        var socket = ui.portraitFrame.gameObject.AddComponent<Image>();
        socket.sprite = FantasyUI.Socket;
        var center = new Vector2(0.5f, 0.5f);
        var window = UIKit.Rect(ui.portraitFrame, "Window", new Vector2(150f, 150f), Vector2.zero, center);
        window.gameObject.AddComponent<Image>().sprite = FantasyUI.Socket;
        window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        ui.portrait = UIKit.Rect(window, "Image", new Vector2(150f, 150f), Vector2.zero, center).gameObject.AddComponent<Image>();
        ui.portrait.preserveAspect = true;
        // Speaker in gold small caps, body in parchment: the name reads first (visual hierarchy), then the line.
        ui.speaker = UIKit.Text(panel, "Speaker", "", 30f, new Vector2(900f, 40f), new Vector2(232f, -26f), left, FantasyUI.Gold);
        ui.speaker.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        ui.body = UIKit.Text(panel, "Body", "", 26f, new Vector2(960f, 150f), new Vector2(232f, -72f), left, FantasyUI.Parchment);
        ui.body.textWrappingMode = TextWrappingModes.Normal;
        // A keycap hint instead of a "Next" button: the key is the instruction.
        ui.hint = UIKit.Hint(panel, "Space", "Continue", new Vector2(-24f, 18f), new Vector2(1f, 0f), 16f);
        ui.choiceRoot = UIKit.Rect(canvas.transform, "Choices", new Vector2(800f, 300f), new Vector2(0f, 300f), bottom);
        canvas.enabled = false;
        return ui;
    }

    /// <summary>Shows or hides the whole dialogue box.</summary>
    public void Show(bool visible) => canvas.enabled = visible;

    /// <summary>Prepares a new line: sets the speaker's name and portrait and clears the body for the typewriter.</summary>
    /// <param name="name">Speaker name; empty for the narrator.</param>
    /// <param name="face">Portrait sprite; null hides the socket.</param>
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

    /// <summary>Shows one numbered button per choice, stacked above the box, first choice on top.</summary>
    /// <param name="choices">The conversation's answers.</param>
    /// <param name="picked">Called with the clicked answer (number keys are handled by <see cref="DialogueManager"/>).</param>
    public void ShowChoices(DialogueData.Choice[] choices, Action<DialogueData.Choice> picked)
    {
        ClearChoices();
        // The Continue hint would be misleading while an answer is required.
        hint.gameObject.SetActive(false);
        var bottom = new Vector2(0.5f, 0f);
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var button = UIKit.Button(choiceRoot, choice.text, new Vector2(780f, 60f), new Vector2(0f, (choices.Length - 1 - i) * 72f), bottom,
                () => picked(choice));
            UIKit.Keycap(button.transform, (i + 1).ToString(), new Vector2(22f, 0f), new Vector2(0f, 0.5f));
        }
    }

    /// <summary>Removes the choice buttons and brings the Continue hint back.</summary>
    public void ClearChoices()
    {
        foreach (Transform child in choiceRoot) Destroy(child.gameObject);
        hint.gameObject.SetActive(true);
    }
}
