using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Plays <see cref="DialogueData"/> conversations: the dialogue logic of the guide's Step 3.
/// </summary>
/// <remarks>
/// <para><b>Flow.</b> <see cref="Play"/> queues a conversation. Each line is shown with its speaker name and portrait
/// and revealed by a typewriter effect; Space, Enter, F or a left click first completes the line, then advances. If
/// the conversation has choices, they appear as buttons (number keys work too) and the picked one's flag is set.
/// When a conversation ends, its <c>flagToSetWhenFinished</c> is set in the <see cref="StoryStateManager"/>, which is
/// how talking moves the story on (objectives update, gates open).</para>
/// <para><b>While talking</b> the player is paused (<c>PlayerLock</c>) and the HUD is hidden so the dialogue box owns the
/// bottom of the screen.</para>
/// <para><b>Who calls it.</b> <c>NarrativeTrigger</c> (zones, F on notes, flag watches), <c>Merchant</c> (Abe at each
/// camp), <c>MemoryEcho</c> (the memories' lines) and <c>EndingController</c> (the offer, the second offer, kneeling).</para>
/// <para>Data (<see cref="DialogueData"/>), logic (this) and display (<see cref="DialogueUI"/>) are kept separate.</para>
/// </remarks>
public class DialogueManager : MonoBehaviour
{
    // Typewriter speed. A press shows the rest of the line at once.
    private const float CharactersPerSecond = 45f;

    private static DialogueManager instance;

    /// <summary>The single dialogue player, created with its UI on first use and kept across scene loads.</summary>
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
    // Conversations asked for while one is playing wait their turn instead of interrupting it.
    private readonly Queue<(DialogueData data, Action done)> queue = new();
    private bool advance;
    private DialogueData.Choice picked;

    /// <summary>True while any conversation is on screen. Triggers and transitions wait for it to finish.</summary>
    public bool IsTalking { get; private set; }

    /// <summary>Raised after each conversation (and any choice) finishes, once its flags are set.</summary>
    public event Action<DialogueData> Finished;

    /// <summary>Raised as each line starts, so speakers can gesture while they talk (<c>NpcGestures</c>).</summary>
    public event Action<DialogueData.Line> LineStarted;

    /// <summary>
    /// Plays a conversation, or queues it behind the one already playing.
    /// </summary>
    /// <param name="dialogue">The conversation asset. Null just calls <paramref name="onComplete"/>.</param>
    /// <param name="onComplete">Called after the conversation, its choice and any follow-up conversations finish.</param>
    public void Play(DialogueData dialogue, Action onComplete = null)
    {
        if (dialogue == null) { onComplete?.Invoke(); return; }
        queue.Enqueue((dialogue, onComplete));
        if (!IsTalking) StartCoroutine(Run());
    }

    // Drains the queue: every line, then the choice, then the flags; a choice's "next" conversation continues in place.
    private IEnumerator Run()
    {
        IsTalking = true;
        PlayerLock.Push();
        // The HUD steps aside while someone speaks: the box takes the bottom of the screen.
        var hud = UIKit.HUDRoot() != null ? UIKit.HUDRoot().GetComponentInParent<Canvas>() : null;
        bool hudWasOn = hud != null && hud.enabled;
        if (hud != null) hud.enabled = false;
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
                    // The player's decision becomes story state: later beats (the endings) read this flag.
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
        if (hud != null) hud.enabled = hudWasOn;
        PlayerLock.Pop();
        IsTalking = false;
    }

    // One line: name and portrait, then the typewriter reveal, then wait for the player to advance.
    private IEnumerator Say(DialogueData.Line line)
    {
        ui.SetLine(line.speaker, line.portrait);
        LineStarted?.Invoke(line);
        yield return null;
        advance = false; // ignore the key press that opened the conversation or ended the last line
        // Typewriter: reveal characters over time; a press shows the whole line at once.
        ui.Body.text = line.text;
        ui.Body.maxVisibleCharacters = 0;
        ui.Body.ForceMeshUpdate();
        int total = ui.Body.textInfo.characterCount;
        // Unscaled time, so the reveal still runs if the game is paused underneath.
        for (float shown = 0f; shown < total && !advance; shown += CharactersPerSecond * Time.unscaledDeltaTime)
        {
            ui.Body.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        ui.Body.maxVisibleCharacters = total;
        advance = false;
        while (!advance) yield return null;
    }

    // Shows the answers with a free cursor and waits for a click or a number key; the pick is left in `picked`.
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

    // Input for "next line". A click only counts while the cursor is locked, so clicking a choice button doesn't
    // also skip the line after it.
    private void Update()
    {
        if (!IsTalking) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked))
            advance = true;
    }

    // Choice buttons need an EventSystem; a region scene played on its own may not have one yet.
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        DontDestroyOnLoad(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));
    }
}
