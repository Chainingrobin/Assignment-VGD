using System;
using UnityEngine;

// One conversation, stored as data so the story can be edited without touching code
// (the assignment guide's Step 2). Lines play in order; optional choices at the end each set
// a story flag and can lead into another conversation.
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Wulang/Dialogue")]
public class DialogueData : ScriptableObject
{
    [Serializable]
    public class Line
    {
        public string speaker;
        public Sprite portrait;
        [TextArea(2, 5)] public string text;
    }

    [Serializable]
    public class Choice
    {
        public string text;
        public string setFlag;
        public DialogueData next;
    }

    public Line[] lines;
    public Choice[] choices;
    [Tooltip("Story flag set once the conversation (and any choice) finishes, e.g. metAbe.")]
    public string flagToSetWhenFinished;
}
