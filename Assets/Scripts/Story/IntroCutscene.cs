using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// The opening of a new game (assignment Step 7). An unseen narrator tells how the guardians of the
// Vale went mad, over slow drifting shots of the forest, the volcano and the Frozen Reach; a storm
// sky cracks with lightning; the Forest arrival flight carries the camera down to the armory; then
// black, Abe's voice, Wulang's eyes opening, and the chapter card. The narration is a DialogueData
// asset (StoryScript writes it): narrator lines have no speaker, the last line is Abe's.
// Space skips straight to the wake-up. RegionManager plays it once (the IntroSeen flag).
public static class IntroCutscene
{
    // Establishing shots: the camera drifts from -> to while looking at a point. Positions near the
    // main menu's drone path, which clears the terrain.
    private static readonly (Vector3 from, Vector3 to, Vector3 look, float seconds)[] Shots =
    {
        (new Vector3(610f, 70f, 700f), new Vector3(585f, 62f, 640f), new Vector3(560f, 10f, 560f), 6.5f),    // the forest
        (new Vector3(655f, 92f, 462f), new Vector3(672f, 96f, 486f), new Vector3(795f, 55f, 642f), 6.5f),    // the volcano
        (new Vector3(700f, 85f, 400f), new Vector3(682f, 80f, 372f), new Vector3(650f, 20f, 250f), 6.5f),    // the Frozen Reach
        (new Vector3(640f, 120f, 520f), new Vector3(652f, 128f, 540f), new Vector3(760f, 230f, 720f), 5.5f), // the storm sky
    };

    // Set by Watch on any Space press, so a press during a fade between shots still skips.
    private static bool skip, playing;

    public static IEnumerator Play(DialogueData narration, FreezeRevealCutscene flight, string chapter, string recap, string region)
    {
        var overlay = TransitionOverlay.Instance;
        skip = false;
        playing = true;
        overlay.StartCoroutine(Watch());
        PlayerLock.Push();
        var hudRoot = UIKit.HUDRoot();
        var hud = hudRoot != null ? hudRoot.GetComponentInParent<Canvas>() : null;
        if (hud != null) hud.enabled = false;
        // Hard cuts between shots (each one opens from black), restored afterwards.
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        var blend = brain != null ? brain.DefaultBlend : default;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        GameAudio.Mood("Menu", 0.5f);
        overlay.ShowSkipHint(true);

        var lines = narration.lines;
        int narrated = lines.Length - 1; // the last line is Abe waking her
        bool Skipping() => skip;

        yield return overlay.FadeTo(1f, 0f, Color.black);
        yield return overlay.ShowCaption("", lines[0].text, 1f);
        for (float t = 0f; t < 4.5f && !Skipping(); t += Time.deltaTime) yield return null;

        for (int i = 0; i < Shots.Length && !skip; i++)
        {
            var (from, to, look, seconds) = Shots[i];
            var shot = new GameObject("Intro shot").AddComponent<CinemachineCamera>();
            shot.Priority = 200;
            var lens = shot.Lens;
            lens.FieldOfView = 50f;
            lens.FarClipPlane = 2500f;
            shot.Lens = lens;
            shot.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
            yield return null; // let the brain cut to it before the fade opens
            if (i + 1 < narrated) overlay.StartCoroutine(overlay.SwapCaption("", lines[i + 1].text));
            overlay.StartCoroutine(overlay.FadeTo(0f, 0.9f, Color.black));
            bool storm = i == Shots.Length - 1;
            int flashes = 0;
            for (float t = 0f; t < seconds && !Skipping(); t += Time.deltaTime)
            {
                var p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                shot.transform.SetPositionAndRotation(p, Quaternion.LookRotation(look - p));
                // The Storm breaking loose: two lightning flashes and the thunder after them.
                if (storm && flashes < 2 && t > 1.2f + flashes * 1.9f)
                {
                    flashes++;
                    overlay.StartCoroutine(Flash(overlay));
                    GameAudio.Play("Sfx/Thunder", null, 0.9f);
                }
                yield return null;
            }
            if (!skip) yield return overlay.FadeTo(1f, 0.6f, Color.black);
            Object.Destroy(shot.gameObject);
        }

        // "...and it found a girl walking home in the rain": the flight down to the armory.
        if (!skip && flight != null)
        {
            if (narrated > Shots.Length + 1) overlay.StartCoroutine(overlay.SwapCaption("", lines[Shots.Length + 1].text));
            if (brain != null) brain.DefaultBlend = blend;
            flight.PlayTransition();
            overlay.StartCoroutine(overlay.FadeTo(0f, 1f, Color.black));
            while (flight.IsPlaying) yield return null; // the flight stops itself on Space
        }

        // Black, Abe's voice, and her eyes opening.
        if (brain != null) brain.DefaultBlend = blend;
        playing = false; // from here on Space only shortens the current beat
        GameAudio.Mood(region, 3f);
        yield return overlay.FadeTo(1f, skip ? 0.2f : 0.5f, Color.black);
        yield return overlay.SwapCaption("", lines[lines.Length - 1].speaker + ": " + lines[lines.Length - 1].text);
        for (float t = 0f; t < 2.8f && !TransitionOverlay.SkipPressed; t += Time.deltaTime) yield return null;
        yield return overlay.HideCaption(0.4f);
        overlay.ShowSkipHint(false);
        foreach (var (alpha, seconds) in new[] { (0.45f, 0.35f), (1f, 0.18f), (0.2f, 0.45f), (1f, 0.14f), (0f, 0.9f) })
            yield return overlay.FadeTo(alpha, seconds, Color.black);
        if (hud != null) hud.enabled = true;

        // The chapter card over the waking view.
        yield return overlay.ShowCaption(chapter, recap);
        overlay.ShowSkipHint(true);
        yield return null;
        for (float t = 0f; t < 5f && !TransitionOverlay.SkipPressed; t += Time.deltaTime) yield return null;
        overlay.ShowSkipHint(false);
        yield return overlay.HideCaption();
        PlayerLock.Pop();
    }

    private static IEnumerator Watch()
    {
        while (playing)
        {
            if (TransitionOverlay.SkipPressed) skip = true;
            yield return null;
        }
    }

    private static IEnumerator Flash(TransitionOverlay overlay)
    {
        yield return overlay.FadeTo(0.85f, 0.05f, Color.white);
        yield return overlay.FadeTo(0f, 0.35f, Color.white);
    }
}
