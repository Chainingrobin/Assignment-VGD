using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

public static class BiomeCinematicChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string RunScene()
    {
        var previews = Object.FindObjectsByType<BiomePreview>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.AreEqual(2, previews.Length);
        foreach (var preview in previews)
        {
            var landmarks = new SerializedObject(preview).FindProperty("landmarks");
            for (int i = 0; i < landmarks.arraySize; i++)
                Assert.IsTrue(((Transform)landmarks.GetArrayElementAtIndex(i).objectReferenceValue).position.sqrMagnitude > 1000, "Biome landmarks must retain their world positions.");
            var director = preview.GetComponent<PlayableDirector>();
            Assert.IsFalse(director.playOnAwake);
            Assert.AreEqual(DirectorWrapMode.None, director.extrapolationMode, "Preview timelines must stop and return controls automatically.");
            var timeline = director.playableAsset as TimelineAsset; Assert.IsNotNull(timeline);
            Assert.IsTrue(timeline.GetOutputTracks().Any(t => t is AnimationTrack));
            Assert.IsTrue(timeline.GetOutputTracks().Any(t => t is CinemachineTrack));
            Assert.IsTrue(director.duration > 8);
            var s = new SerializedObject(preview.GetComponent<FreezeRevealCutscene>());
            Assert.IsNull(s.FindProperty("freezer").objectReferenceValue);
            foreach (var name in new[] { "playerCamera", "playerInputs", "droneCamera" }) Assert.IsNotNull(s.FindProperty(name).objectReferenceValue);
        }
        var cycle = Object.FindFirstObjectByType<DayNightCycle>(); Assert.IsTrue(cycle.enabled);
        Assert.IsTrue(RenderSettings.skybox.HasProperty("_Day_cycle_time"));
        Assert.IsTrue(ShaderUtil.GetShaderMessages(RenderSettings.skybox.shader).All(m => m.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error));
        var freezer = Object.FindFirstObjectByType<WaterFreezer>(); var f = new SerializedObject(freezer);
        Assert.IsNotNull(f.FindProperty("freezeParticles").objectReferenceValue);
        Assert.IsNotNull(f.FindProperty("freezeLight").objectReferenceValue);
        return "Two Cinemachine/Timeline previews, assigned player controls, cartoon sky and freeze effects passed integration checks.";
    }

    public static string RunPrompts()
    {
        Assert.IsTrue(Application.isPlaying);
        var camera = Camera.main; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        var interaction = Object.FindFirstObjectByType<PlayerInteract>();
        var update = typeof(PlayerInteract).GetMethod("Update", Private);
        var current = typeof(PlayerInteract).GetField("currentPreview", Private);
        try
        {
            var volcano = GameObject.Find("Volcano Preview").GetComponent<BiomePreview>();
            camera.transform.LookAt(GameObject.Find("Redwood Lava Cavern").transform.Find("Summit smoke"));
            update.Invoke(interaction, null); Assert.AreEqual(volcano, current.GetValue(interaction));
            var statue = GameObject.Find("Ice statue").transform;
            var ice = GameObject.Find("Ice Biome Preview").GetComponent<BiomePreview>();
            camera.transform.position = statue.position + new Vector3(0, 15, 85);
            camera.transform.LookAt(GameObject.Find("Ice biome preview landmark").transform);
            update.Invoke(interaction, null); Assert.AreEqual(ice, current.GetValue(interaction));
            camera.transform.rotation *= Quaternion.Euler(0,180,0); Assert.IsFalse(ice.CanView(camera));
            camera.transform.position = statue.position + new Vector3(0, 15, 350);
            camera.transform.LookAt(statue); Assert.IsFalse(ice.CanView(camera));
            camera.transform.position = statue.position + new Vector3(0, 2, 2.5f);
            camera.transform.LookAt(statue.position + Vector3.up * 1.9f);
            update.Invoke(interaction, null); Assert.IsNull(current.GetValue(interaction));
            Assert.IsNotNull(typeof(PlayerInteract).GetField("current", Private).GetValue(interaction));
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); update.Invoke(interaction, null); }
        return "Volcano and nearby ice prompts, look-away/range rejection and statue priority passed.";
    }

    public static string RunPlayback()
    {
        Assert.IsTrue(Application.isPlaying);
        var cycle = Object.FindFirstObjectByType<DayNightCycle>();
        var time = typeof(DayNightCycle).GetField("timeOfDay", Private);
        var apply = typeof(DayNightCycle).GetMethod("ApplyLighting", Private);
        var settings = new SerializedObject(cycle);
        var sun = (Light)settings.FindProperty("sun").objectReferenceValue;
        var moon = (Light)settings.FindProperty("moon").objectReferenceValue;
        float originalTime = (float)time.GetValue(cycle), minimum = float.MaxValue;
        for (int i = 0; i < 240; i++)
        {
            time.SetValue(cycle, i / 10f); apply.Invoke(cycle, null);
            minimum = Mathf.Min(minimum, sun.intensity + moon.intensity);
            Assert.IsTrue(sun.intensity + moon.intensity >= .18f, "Sun/moon illumination must overlap through twilight.");
            Assert.IsTrue(RenderSettings.ambientLight.maxColorComponent >= .09f);
        }
        time.SetValue(cycle, originalTime); apply.Invoke(cycle, null);
        var camera = Camera.main;
        foreach (var preview in Object.FindObjectsByType<BiomePreview>(FindObjectsSortMode.None))
        {
            var cutscene = preview.GetComponent<FreezeRevealCutscene>();
            var s = new SerializedObject(cutscene); var controls = s.FindProperty("controls");
            var states = new bool[controls.arraySize];
            for (int i = 0; i < states.Length; i++) states[i] = ((Behaviour)controls.GetArrayElementAtIndex(i).objectReferenceValue).enabled;
            Vector3 originalPosition = camera.transform.localPosition; Quaternion originalRotation = camera.transform.localRotation;
            Assert.IsTrue(cutscene.CanPlay);
            preview.View(); Assert.IsTrue(cutscene.IsPlaying); Assert.IsFalse(cutscene.CanPlay);
            for (int i = 0; i < states.Length; i++) Assert.IsFalse(((Behaviour)controls.GetArrayElementAtIndex(i).objectReferenceValue).enabled);
            var director = preview.GetComponent<PlayableDirector>(); director.time = 4; director.Evaluate();
            typeof(FreezeRevealCutscene).GetMethod("LateUpdate", Private).Invoke(cutscene, null);
            if (Time.deltaTime > 0) Assert.IsTrue(director.time > 4, "Preview timelines must advance during playback.");
            var drone = (CinemachineCamera)s.FindProperty("droneCamera").objectReferenceValue;
            Assert.IsTrue(drone.transform.position.sqrMagnitude > 10000);
            director.Stop(); Assert.IsFalse(cutscene.IsPlaying);
            for (int i = 0; i < states.Length; i++) Assert.AreEqual(states[i], ((Behaviour)controls.GetArrayElementAtIndex(i).objectReferenceValue).enabled);
            Assert.IsTrue(Vector3.Distance(originalPosition, camera.transform.localPosition) < .01f);
            Assert.IsTrue(Quaternion.Angle(originalRotation, camera.transform.localRotation) < .1f);
            preview.View(); Assert.IsFalse(cutscene.IsPlaying, "A preview may only play once per session.");
        }
        return "240 day/night samples passed (minimum combined light " + minimum.ToString("F2") + "); both previews lock/restore controls and reject repeat playback.";
    }

    public static void BeginAutomaticReturnCheck()
    {
        Assert.IsTrue(Application.isPlaying);
        var preview = GameObject.Find("Volcano Preview").GetComponent<BiomePreview>();
        Assert.IsTrue(preview.GetComponent<FreezeRevealCutscene>().CanPlay);
        preview.View();
        var director = preview.GetComponent<PlayableDirector>();
        director.time = director.duration; director.Evaluate();
        typeof(FreezeRevealCutscene).GetMethod("LateUpdate", Private).Invoke(preview.GetComponent<FreezeRevealCutscene>(), null);
    }

    public static string CheckAutomaticReturn()
    {
        var cutscene = GameObject.Find("Volcano Preview").GetComponent<FreezeRevealCutscene>();
        Assert.IsFalse(cutscene.IsPlaying, "A preview must end automatically without a manual Stop call.");
        var states = (bool[])typeof(FreezeRevealCutscene).GetField("controlStates", Private).GetValue(cutscene);
        var controls = new SerializedObject(cutscene).FindProperty("controls");
        for (int i = 0; i < states.Length; i++) Assert.AreEqual(states[i], ((Behaviour)controls.GetArrayElementAtIndex(i).objectReferenceValue).enabled);
        return "Timeline automatically completed and restored all player controls.";
    }
}
