using System;
using UnityEngine;

/// <summary>
/// A region's mission list, in story order: the data half of the objective system (assignment guide, Step 6).
/// One per region scene; <see cref="ObjectiveManager"/> reads it.
/// </summary>
/// <remarks>
/// Each objective says which story flag reveals it (<see cref="Objective.showWhen"/>) and which completes it
/// (<see cref="Objective.doneWhen"/>). The tracker shows the first objective that is showing and not yet done, so
/// objectives update by themselves as flags are set, and always match the story state even if the player explores
/// out of order. Example (Forest): "Speak with the old man" is done by <c>metAbe</c>, which also reveals "Put the Earth
/// guardian to rest". The wording comes from the Story Text asset (Wulang &gt; Story &gt; Apply story text).
/// </remarks>
public class RegionObjectives : MonoBehaviour
{
    /// <summary>One objective: its text, the flags that show and complete it, and where it points.</summary>
    [Serializable]
    public class Objective
    {
        /// <summary>What the tracker says, e.g. "Take up the Earth tome".</summary>
        [TextArea] public string text;
        [Tooltip("Flag(s) that reveal it; empty = from the start. Use a|b for either.")]
        public string showWhen;
        [Tooltip("Flag(s) that complete it; empty = stays until the region changes.")]
        public string doneWhen;
        [Tooltip("World position of the goal, for the HUD compass marker. Zero = no marker.")]
        public Vector3 marker;
    }

    [SerializeField] private Objective[] objectives;

    /// <summary>The objectives in story order.</summary>
    public Objective[] Objectives => objectives;

    /// <summary>Replaces the list (used by the Editor story setup).</summary>
    public void Set(Objective[] list) => objectives = list;
}
