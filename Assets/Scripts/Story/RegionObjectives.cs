using System;
using UnityEngine;

// A region's mission list, in story order. ObjectiveManager shows the first one that is
// showing and not yet done, so objectives update by themselves as story flags are set.
public class RegionObjectives : MonoBehaviour
{
    [Serializable]
    public class Objective
    {
        [TextArea] public string text;
        [Tooltip("Flag(s) that reveal it; empty = from the start. Use a|b for either.")]
        public string showWhen;
        [Tooltip("Flag(s) that complete it; empty = stays until the region changes.")]
        public string doneWhen;
    }

    [SerializeField] private Objective[] objectives;
    public Objective[] Objectives => objectives;

    public void Set(Objective[] list) => objectives = list;
}
