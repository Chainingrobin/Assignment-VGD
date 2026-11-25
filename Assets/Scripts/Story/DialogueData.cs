using System;
using UnityEngine;

/// <summary>
/// One conversation, stored as data (assignment guide, Step 2): one asset per conversation in
/// <c>Assets/Story/Dialogue</c>, created with <i>Assets &gt; Create &gt; Wulang &gt; Dialogue</i>.
/// </summary>
/// <remarks>
/// <para><b>Editing.</b> All wording is edited here, in the Inspector, without touching code. The game reads the asset
/// each time the conversation plays, so a change shows up immediately (in Play mode too).</para>
/// <para><b>Structure.</b> <see cref="lines"/> play in order. If <see cref="choices"/> is not empty, the player picks one
/// at the end: it sets its own story flag and can lead into another conversation (<see cref="Choice.next"/>), which
/// makes this a small dialogue tree whose links are asset references. Finally <see cref="flagToSetWhenFinished"/> is
/// set, which is how a conversation moves the story on (e.g. <c>metAbe</c> advances the Forest objective).</para>
/// <para><b>Separation.</b> Data (this asset), logic (<c>DialogueManager</c>) and display (<c>DialogueUI</c>) are kept
/// apart, as the guide's Step 3 asks.</para>
/// </remarks>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Wulang/Dialogue")]
public class DialogueData : ScriptableObject
{
    /// <summary>One line of the conversation: who speaks, their face, and what they say.</summary>
    [Serializable]
    public class Line
    {
        /// <summary>Name shown above the text ("Abe", "Wulang", "Young Wulang"). Empty for the narrator.</summary>
        public string speaker;
        /// <summary>Portrait in the dialogue box's socket. None hides the socket and the text uses the full width.</summary>
        public Sprite portrait;
        /// <summary>The spoken line, revealed by the typewriter effect.</summary>
        [TextArea(2, 5)] public string text;
    }

    /// <summary>A player answer at the end of the conversation (the Sanctum's "kneel or refuse").</summary>
    [Serializable]
    public class Choice
    {
        /// <summary>Button text; number keys 1-9 pick it too.</summary>
        public string text;
        /// <summary>Story flag recorded when this answer is picked (e.g. <c>refusedOffer</c>), which the endings read.</summary>
        public string setFlag;
        /// <summary>Optional conversation that follows this answer. None ends the conversation.</summary>
        public DialogueData next;
    }

    /// <summary>The lines, played in order.</summary>
    public Line[] lines;
    /// <summary>Optional answers offered after the last line; empty = no choice.</summary>
    public Choice[] choices;
    [Tooltip("Story flag set once the conversation (and any choice) finishes, e.g. metAbe.")]
    public string flagToSetWhenFinished;
}
