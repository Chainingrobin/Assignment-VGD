using System;
using UnityEngine;

/// <summary>
/// Every story line that is not part of a conversation, kept as data in one asset
/// (<c>Assets/Story/Story Text.asset</c>) so the writing can be changed in the Inspector without
/// touching code (assignment guide, Step 2).
/// </summary>
/// <remarks>
/// Where the story text lives:
/// <list type="bullet">
/// <item><description>Conversations: one <see cref="DialogueData"/> asset each, in <c>Assets/Story/Dialogue</c>.
/// The game reads them directly, so an edit shows up the next time that conversation plays, even in Play mode.</description></item>
/// <item><description>Journal pages, memories and item lore: one <see cref="ItemData"/> asset each, in
/// <c>Assets/Story/Journal</c> and <c>Assets/Items</c>, also read directly.</description></item>
/// <item><description>Everything else (this asset): guardian lines, chapter titles and recaps, objectives, barks
/// and the ending cards. These are copied onto the scene objects that show them, so after editing this asset run
/// <b>Wulang &gt; Story &gt; Apply story text</b> once.</description></item>
/// </list>
/// </remarks>
[CreateAssetMenu(fileName = "Story Text", menuName = "Wulang/Story Text")]
public class StoryText : ScriptableObject
{
    /// <summary>One chapter (a region scene): its chapter card, arrival recap, objectives and Abe's shop greeting.</summary>
    [Serializable]
    public class Chapter
    {
        [Tooltip("Region scene this chapter belongs to (Forest, Ice, Volcano, FinalArena).")]
        public string scene;
        [Tooltip("Roman numeral on the chapter card, e.g. II.")]
        public string number;
        [Tooltip("Chapter name on the card, e.g. What the Cold Keeps.")]
        public string title;
        [Tooltip("Recap shown under the chapter title while the region loads.")]
        [TextArea(2, 5)] public string recap;
        [Tooltip("Wulang's line when she walks into this region's elemental seal before its story flag is set. Empty = none.")]
        public string lockedLine;
        [Tooltip("Abe's first line when his shop opens at this chapter's camp. Empty = no camp.")]
        public string abeGreeting;
        [Tooltip("The chapter's objectives in story order. Which flag shows and completes each one is set on the scene's RegionObjectives.")]
        [TextArea(1, 3)] public string[] objectives;
    }

    /// <summary>A guardian (boss): the name on its health bar, its line when the fight starts and its last words.</summary>
    [Serializable]
    public class Guardian
    {
        [Tooltip("Story flag set when this guardian falls; identifies the encounter (e.g. bossEarthDefeated).")]
        public string defeatFlag;
        public string displayName;
        [TextArea(1, 3)] public string introLine;
        [TextArea(1, 3)] public string dyingLine;
    }

    /// <summary>A one-line subtitle played by a NarrativeTrigger (an arrival line, a hint).</summary>
    [Serializable]
    public class Bark
    {
        [Tooltip("Region scene that holds the trigger.")]
        public string scene;
        [Tooltip("Name of the NarrativeTrigger's GameObject in that scene.")]
        public string trigger;
        [TextArea(1, 3)] public string text;
    }

    public Chapter[] chapters;
    public Guardian[] guardians;
    public Bark[] barks;

    [Header("Hazards (first time only)")]
    public string lakeLine;
    public string lavaLine;

    [Header("Barks")]
    [Tooltip("Subtitle when H is pressed with no potions left.")]
    public string noPotionsLine;
    [Tooltip("Wulang's line when H is pressed at full health.")]
    public string fullHealthLine;
    [Tooltip("Subtitle when Ganfaul changes element; {0} is the element's name.")]
    public string ganfaulPhaseLine;

    [Header("Memories")]
    [Tooltip("Caption over each of Abe's memory echoes.")]
    public string memoryTitle;
    [Tooltip("Caption over the true memory in the Truth ending.")]
    public string trueMemoryTitle;

    [Header("Ending cards")]
    public string submissionTitle;
    [TextArea(3, 8)] public string submissionText;
    public string truthTitle;
    [TextArea(3, 8)] public string truthText;
}
