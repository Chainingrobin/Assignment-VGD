using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Plays DialogueData conversations one line at a time with a typewriter reveal. Space, Enter,
// F or left click finishes the line or advances; choices appear as buttons (number keys work
// too) and set their story flag. The player is paused while anyone is talking. Data
// (DialogueData), logic (this) and display (DialogueUI) are kept separate.
public class DialogueManager : MonoBehaviour
{
    private const float CharactersPerSecond = 45f;

    private static DialogueManager instance;
    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject(nameof(DialogueManager)).AddComponent<DialogueManager>();
                DontDestroyOnLoad(instance.gameObject);
                instance.ui = DialogueUI.Build();
            }
            return instance;
        }
    }

    private DialogueUI ui;
    private readonly Queue<(DialogueData data, Action done)> queue = new();
    private bool advance;
    private DialogueData.Choice picked;

    public bool IsTalking { get; private set; }
    public event Action<DialogueData> Finished;

    public void Play(DialogueData dialogue, Action onComplete = null)
    {
        if (dialogue == null) { onComplete?.Invoke(); return; }
        queue.Enqueue((dialogue, onComplete));
        if (!IsTalking) StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        IsTalking = true;
        PlayerLock.Push();
        ui.Show(true);
        while (queue.Count > 0)
        {
            var (dialogue, done) = queue.Dequeue();
            var current = dialogue;
            while (current != null)
            {
                foreach (var line in current.lines) yield return Say(line);
                var next = (DialogueData)null;
                if (current.choices != null && current.choices.Length > 0)
                {
                    yield return Choose(current.choices);
                    StoryStateManager.Instance.SetFlag(picked.setFlag);
                    next = picked.next;
                }
                StoryStateManager.Instance.SetFlag(current.flagToSetWhenFinished);
                Finished?.Invoke(current);
                current = next;
            }
            done?.Invoke();
        }
        ui.Show(false);
        PlayerLock.Pop();
        IsTalking = false;
    }

    private IEnumerator Say(DialogueData.Line line)
    {
        ui.SetLine(line.speaker, line.portrait);
        yield return null;
        advance = false; // ignore the key press that opened the conversation or ended the last line
        // Typewriter: reveal characters over time; a press shows the whole line at once.
        ui.Body.text = line.text;
        ui.Body.maxVisibleCharacters = 0;
        ui.Body.ForceMeshUpdate();
        int total = ui.Body.textInfo.characterCount;
        for (float shown = 0f; shown < total && !advance; shown += CharactersPerSecond * Time.unscaledDeltaTime)
        {
            ui.Body.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        ui.Body.maxVisibleCharacters = total;
        advance = false;
        while (!advance) yield return null;
    }

    private IEnumerator Choose(DialogueData.Choice[] choices)
    {
        picked = null;
        EnsureEventSystem();
        PlayerLock.Push(showCursor: true);
        ui.ShowChoices(choices, choice => picked = choice);
        while (picked == null)
        {
            var keyboard = Keyboard.current;
            for (int i = 0; keyboard != null && i < choices.Length && i < 9; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) picked = choices[i];
            yield return null;
        }
        ui.ClearChoices();
        PlayerLock.Pop(showCursor: true);
    }

    private void Update()
    {
        if (!IsTalking) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked))
            advance = true;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        DontDestroyOnLoad(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));
    }
}
