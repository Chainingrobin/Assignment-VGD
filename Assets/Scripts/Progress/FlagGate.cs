using UnityEngine;

/// <summary>
/// Turns objects or components on only while a story flag is set (or, inverted, only while it is not): the
/// "door that unlocks when the story says so" of the assignment guide's Steps 5 and 8.
/// </summary>
/// <remarks>
/// Examples in the levels: the ice statue's <c>Interactable</c> stays off until <c>bossEarthDefeated</c>; the Ice
/// region's waystone and the volcano's rift (both <see cref="RegionTravel"/> exits) stay off until their guardian
/// falls. It re-checks the instant its flag is set, so a gate opens during play without a reload.
/// </remarks>
public class FlagGate : MonoBehaviour
{
    [Tooltip("Story flag that opens the gate, e.g. bossIceDefeated.")]
    [SerializeField] private string flag;
    [Tooltip("Open while the flag is NOT set instead (e.g. hide something once a beat has happened).")]
    [SerializeField] private bool invert;
    [Tooltip("GameObjects shown only while the gate is open.")]
    [SerializeField] private GameObject[] objects;
    [Tooltip("Components enabled only while the gate is open (e.g. an Interactable).")]
    [SerializeField] private Behaviour[] components;

    private void OnEnable()
    {
        StoryStateManager.Instance.OnFlagSet += OnFlag;
        Apply();
    }

    private void OnDisable()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlag;
    }

    private void OnFlag(string changed) { if (changed == flag) Apply(); }

    // Shows or hides every target to match the story state.
    private void Apply()
    {
        bool open = StoryStateManager.Instance.HasFlag(flag) != invert;
        foreach (var target in objects) if (target != null) target.SetActive(open);
        foreach (var target in components) if (target != null) target.enabled = open;
    }
}
