using System.Collections.Generic;
using UnityEngine;

// All of the game's sound goes through here. Clips live in Resources/Audio (Music/, Ambience/,
// Sfx/), so any script plays one by name with no inspector wiring, and a real recording saved
// over a placeholder with the same name replaces it. A name that is a folder (Sfx/Footstep)
// picks a random clip from it. Music and its ambience bed are two looping layers that crossfade
// when a region, boss fight, memory or ending changes the mood, and they survive scene loads.
public class GameAudio : MonoBehaviour
{
    private const float MusicVolume = 0.45f, AmbienceVolume = 0.7f;

    private static GameAudio instance;
    private static readonly Dictionary<string, AudioClip[]> clips = new();

    // Two sources per layer: one fading in, one fading out.
    private readonly AudioSource[] music = new AudioSource[2], ambience = new AudioSource[2];
    private int musicCurrent, ambienceCurrent;
    private float fadeSeconds = 2f;

    public static string CurrentMood { get; private set; }

    private static GameAudio Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = new GameObject(nameof(GameAudio)).AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            for (int i = 0; i < 2; i++)
            {
                instance.music[i] = instance.Layer();
                instance.ambience[i] = instance.Layer();
            }
            return instance;
        }
    }

    private AudioSource Layer()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    private static AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var found)) clips[name] = found = Resources.LoadAll<AudioClip>("Audio/" + name);
        return found.Length == 0 ? null : found[Random.Range(0, found.Length)];
    }

    // A one-shot in the world (3D, fading with distance) or, without a position, in the player's ears.
    public static void Play(string name, Vector3? at = null, float volume = 1f, float pitch = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return;
        var go = new GameObject("Sfx " + name);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch * Random.Range(0.94f, 1.06f);
        if (at.HasValue)
        {
            go.transform.position = at.Value;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 90f;
        }
        source.Play();
        Destroy(go, clip.length / source.pitch + 0.1f);
    }

    // The AoE pack ships an AudioSource on each effect prefab without playing it: start them.
    public static void PlayOwn(GameObject effect)
    {
        foreach (var source in effect.GetComponentsInChildren<AudioSource>()) source.Play();
    }

    // Crossfades to Music/<mood> and its Ambience/<mood> bed (silence where a clip doesn't exist).
    public static void Mood(string mood, float fade = 2f)
    {
        if (mood == CurrentMood) return;
        CurrentMood = mood;
        var self = Instance;
        self.fadeSeconds = Mathf.Max(0.05f, fade);
        bool silence = string.IsNullOrEmpty(mood); // "" fades everything out (a held breath before a reveal)
        self.musicCurrent = Swap(self.music, self.musicCurrent, silence ? null : Clip("Music/" + mood));
        self.ambienceCurrent = Swap(self.ambience, self.ambienceCurrent, silence ? null : Clip("Ambience/" + mood));
    }

    private static int Swap(AudioSource[] pair, int current, AudioClip clip)
    {
        if (clip != null && pair[current].clip == clip && pair[current].isPlaying) return current;
        int next = 1 - current;
        pair[next].clip = clip;
        pair[next].volume = 0f;
        if (clip != null) pair[next].Play();
        else pair[next].Stop();
        return next;
    }

    private void Update()
    {
        if (music[0] == null) { Destroy(gameObject); return; } // a script reload in the Editor dropped the layers
        float step = Time.unscaledDeltaTime / fadeSeconds;
        Fade(music, musicCurrent, MusicVolume, step);
        Fade(ambience, ambienceCurrent, AmbienceVolume, step);
    }

    private static void Fade(AudioSource[] pair, int current, float full, float step)
    {
        for (int i = 0; i < 2; i++)
        {
            float target = i == current && pair[i].clip != null ? full : 0f;
            pair[i].volume = Mathf.MoveTowards(pair[i].volume, target, step * full);
            if (target == 0f && pair[i].volume == 0f && pair[i].isPlaying) pair[i].Stop();
        }
    }
}
