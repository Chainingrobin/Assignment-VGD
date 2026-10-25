using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Unity.Cinemachine;

public static class BiomeCinematicSetup
{
    const string Folder = "Assets/Cutscenes/";
    const string SkyPack = "Assets/ZerinLabs_shaderPack_CartoonSky/";

    public static string Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Build cinematics in Edit mode.");
        var source = UnityEngine.Object.FindFirstObjectByType<FreezeRevealCutscene>();
        var volcano = GameObject.Find("Redwood Lava Cavern").transform;
        var statue = GameObject.Find("Ice statue").transform;
        if (source == null || GameObject.Find("Volcano Preview") != null) throw new InvalidOperationException("An existing freeze reveal and no existing previews are required.");
        var sourceSettings = new SerializedObject(source);
        var camera = (Camera)sourceSettings.FindProperty("playerCamera").objectReferenceValue;
        var volcanoLook = Landmark(volcano, "Volcano preview landmark", volcano.TransformPoint(new Vector3(0, 30, 0)));
        var smoke = volcano.Find("Summit smoke");
        var iceLook = Landmark(Terrain.activeTerrain.transform, "Ice biome preview landmark", new Vector3(statue.position.x, 65, statue.position.z - 40));
        var v = volcano;
        var volcanoPreview = Preview("Volcano Preview", source, new[] { volcanoLook, smoke }, volcano, 380,
            new[] { v.TransformPoint(new Vector3(-15, 95, -120)), v.TransformPoint(new Vector3(-10, 72, -72)), v.TransformPoint(new Vector3(0, 10, -59)), v.TransformPoint(new Vector3(0, 9, -40)), v.TransformPoint(new Vector3(-3, 10, -29)) },
            new[] { v.TransformPoint(new Vector3(0, 35, 0)), v.TransformPoint(new Vector3(0, 27, -10)), v.TransformPoint(new Vector3(0, 13, -32)), v.TransformPoint(new Vector3(4, 17, 5)), v.TransformPoint(new Vector3(4, 18, 16)) });
        Vector3 s = statue.position;
        var icePositions = new[] { s + new Vector3(90, 85, -155), s + new Vector3(60, 57, -70), s + new Vector3(22, 24, -16), s + new Vector3(9, 7, 12), s + new Vector3(9, 7, 12) };
        for (int i = 0; i < icePositions.Length; i++) icePositions[i].y = Mathf.Max(icePositions[i].y, Terrain.activeTerrain.SampleHeight(icePositions[i]) + Terrain.activeTerrain.transform.position.y + (i < 2 ? 28 : 6));
        var icePreview = Preview("Ice Biome Preview", source, new[] { iceLook, statue }, Terrain.activeTerrain.transform, 210,
            icePositions, new[] { s + new Vector3(-35, 5, -85), s + new Vector3(0, 3, -30), s + Vector3.up * 2, s + Vector3.up * 2, s + Vector3.up * 2 });
        var interaction = UnityEngine.Object.FindFirstObjectByType<PlayerInteract>();
        Undo.RecordObject(interaction, "Connect biome preview prompts");
        var interactionSettings = new SerializedObject(interaction);
        var previews = interactionSettings.FindProperty("biomePreviews"); previews.arraySize = 2;
        previews.GetArrayElementAtIndex(0).objectReferenceValue = volcanoPreview;
        previews.GetArrayElementAtIndex(1).objectReferenceValue = icePreview;
        interactionSettings.ApplyModifiedPropertiesWithoutUndo();
        SetupFreezeEffects(sourceSettings);
        SetupSky();
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(camera.gameObject.scene);
        return "Created two one-time look-triggered Timeline previews, night freeze effects and overlapping cartoon day/night lighting.";
    }

    static Transform Landmark(Transform parent, string name, Vector3 position)
    {
        var g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Add biome landmark");
        // Terrain ignores its own zero scale, but child transforms do not.
        if (parent.lossyScale.sqrMagnitude > .001f) g.transform.SetParent(parent);
        g.transform.position = position; return g.transform;
    }

    static BiomePreview Preview(string name, FreezeRevealCutscene source, Transform[] landmarks, Transform biome, float range, Vector3[] positions, Vector3[] targets)
    {
        var g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Add biome cinematic");
        var director = g.AddComponent<PlayableDirector>(); director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.None;
        var shotObject = new GameObject(name + " Drone"); shotObject.transform.SetParent(g.transform, false);
        var drone = shotObject.AddComponent<CinemachineCamera>(); drone.Priority = 0;
        var lens = drone.Lens; lens.FieldOfView = name.StartsWith("Volcano") ? 65 : 58; lens.FarClipPlane = 1500; drone.Lens = lens;
        shotObject.transform.position = positions[0]; shotObject.transform.LookAt(targets[0]);
        var animator = shotObject.AddComponent<Animator>(); animator.applyRootMotion = false;
        var clip = new AnimationClip { name = name + " Flight", frameRate = 60 };
        KeyFlight(clip, new[] { 0, 2.7f, 5.1f, 7.1f, 8.5f }, positions, targets);
        AssetDatabase.CreateAsset(clip, Folder + name + " Flight.anim");
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, Folder + name + ".playable");
        var animationTrack = timeline.CreateTrack<AnimationTrack>(null, "Drone flight"); animationTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
        var movement = animationTrack.CreateClip(clip); movement.start = 0; movement.duration = 9.5;
        var flight = (AnimationPlayableAsset)movement.asset; flight.removeStartOffset = false; flight.applyFootIK = false;
        var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Area reveal");
        var shot = cameraTrack.CreateClip<CinemachineShot>(); shot.start = 0; shot.duration = 9.5; shot.easeInDuration = .8; shot.easeOutDuration = 1;
        var shotAsset = (CinemachineShot)shot.asset; shotAsset.VirtualCamera.exposedName = new PropertyName(name + " Camera");
        director.playableAsset = timeline; director.SetGenericBinding(animationTrack, animator);
        var sourceSettings = new SerializedObject(source);
        var playerCamera = (Camera)sourceSettings.FindProperty("playerCamera").objectReferenceValue;
        director.SetGenericBinding(cameraTrack, playerCamera.GetComponent<CinemachineBrain>());
        director.SetReferenceValue(shotAsset.VirtualCamera.exposedName, drone);
        var cutscene = g.AddComponent<FreezeRevealCutscene>(); EditorUtility.CopySerialized(source, cutscene);
        var settings = new SerializedObject(cutscene); settings.FindProperty("freezer").objectReferenceValue = null;
        settings.FindProperty("droneCamera").objectReferenceValue = drone; settings.ApplyModifiedPropertiesWithoutUndo();
        var preview = g.AddComponent<BiomePreview>(); var previewSettings = new SerializedObject(preview);
        var anchors = previewSettings.FindProperty("landmarks"); anchors.arraySize = landmarks.Length;
        for (int i = 0; i < landmarks.Length; i++) anchors.GetArrayElementAtIndex(i).objectReferenceValue = landmarks[i];
        previewSettings.FindProperty("biomeRoot").objectReferenceValue = biome;
        previewSettings.FindProperty("range").floatValue = range; previewSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(timeline); return preview;
    }

    [MenuItem("Wulang/Cinematics/Author smooth cinematics")]
    public static string Polish()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author cinematics in Edit mode.");
        Vector3 s = GameObject.Find("Ice statue").transform.position;
        // High over the snowfield, down into the river channel south of the statue, a skim west along it, then an orbit onto the statue.
        AuthorFlight("Ice Biome Preview", new[] { 0, 2.4f, 4.6f, 6.8f, 8.6f, 10.6f },
            new[] { s + new Vector3(170, 80, -150), s + new Vector3(130, 22, -45), s + new Vector3(60, 12, -30), s + new Vector3(15, 7, -28), s + new Vector3(-12, 5, -10), s + new Vector3(4, 3.2f, 8) },
            new[] { s + new Vector3(0, 5, -30), s + new Vector3(40, 8, -30), s + new Vector3(0, 6, -28), s + new Vector3(0, 2.5f, 0), s + new Vector3(0, 2.2f, 0), s + new Vector3(0, 2, 0) },
            new[] { 62f, 56f, 52f, 48f, 44f, 42f });
        VolcanoBiomeBuilder.RetargetPreview();
        var reveal = new SerializedObject(GameObject.Find("FreezeRevealCutscene").GetComponent<FreezeRevealCutscene>());
        // Hold the wide frozen-river shot before blending back to the player.
        reveal.FindProperty("returnDuration").floatValue = 1.5f;
        reveal.ApplyModifiedProperties();
        Handheld((CinemachineCamera)reveal.FindProperty("droneCamera").objectReferenceValue, .5f);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return "Smooth banking flights, lens moves and handheld noise authored for both previews and the freeze reveal.";
    }

    // Rebuilds a preview's drone flight and timing; asserts the camera never clips terrain or colliders.
    internal static void AuthorFlight(string name, float[] times, Vector3[] positions, Vector3[] targets, float[] fieldOfView)
    {
        var director = GameObject.Find(name).GetComponent<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var drone = (CinemachineCamera)new SerializedObject(director.GetComponent<FreezeRevealCutscene>()).FindProperty("droneCamera").objectReferenceValue;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name + " Flight.anim");
        var path = Spline(times, positions);
        for (float t = 0; t <= times[^1]; t += .1f)
        {
            var p = Sample(path, t);
            float ground = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y;
            UnityEngine.Assertions.Assert.IsTrue(p.y - ground > 2f, name + " flies through terrain at " + t.ToString("0.0") + " s, " + p);
            UnityEngine.Assertions.Assert.IsFalse(Physics.CheckSphere(p, .8f, ~0, QueryTriggerInteraction.Ignore), name + " clips geometry at " + t.ToString("0.0") + " s, " + p);
        }
        Undo.RecordObjects(new UnityEngine.Object[] { clip, timeline, drone.transform }, "Author smooth flight");
        KeyFlight(clip, times, positions, targets, fieldOfView);
        double end = times[^1] + .4;
        foreach (var track in timeline.GetOutputTracks())
            foreach (var timelineClip in track.GetClips())
            {
                timelineClip.duration = end;
                // Short blends: a long one swings the camera across the map between the player and the drone.
                if (track is CinemachineTrack) { timelineClip.easeInDuration = .5; timelineClip.easeOutDuration = .5; }
            }
        drone.transform.position = positions[0];
        drone.transform.LookAt(targets[0]);
        Handheld(drone, .6f);
        EditorUtility.SetDirty(clip); EditorUtility.SetDirty(timeline);
    }

    // Dense drone flight: unclamped splines through position and aim keys, sampled at 30 Hz, banking into turns.
    internal static void KeyFlight(AnimationClip clip, float[] times, Vector3[] positions, Vector3[] targets, float[] fieldOfView = null)
    {
        clip.ClearCurves();
        var path = Spline(times, positions);
        var aim = Spline(times, targets);
        var fov = fieldOfView == null ? null : Smooth(times, fieldOfView);
        var position = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var rotation = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var lens = new AnimationCurve();
        Quaternion last = Quaternion.identity;
        float bank = 0;
        const float step = 1 / 30f;
        int count = Mathf.CeilToInt(times[^1] / step);
        for (int i = 0; i <= count; i++)
        {
            float t = Mathf.Min(i * step, times[^1]);
            var p = Sample(path, t);
            float yawRate = Mathf.DeltaAngle(Heading(path, aim, t - .15f), Heading(path, aim, t + .15f)) / .3f;
            // Roll into turns like a drone, eased so it never snaps.
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * .08f, -9f, 9f), .12f);
            var q = Quaternion.LookRotation(Sample(aim, t) - p) * Quaternion.Euler(0, 0, bank);
            if (i > 0 && Quaternion.Dot(last, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            last = q;
            for (int k = 0; k < 3; k++) position[k].AddKey(t, p[k]);
            for (int k = 0; k < 4; k++) rotation[k].AddKey(t, q[k]);
            if (fov != null) lens.AddKey(t, fov.Evaluate(t));
        }
        for (int k = 0; k < 3; k++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition." + "xyz"[k]), position[k]);
        for (int k = 0; k < 4; k++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation." + "xyzw"[k]), rotation[k]);
        if (fov != null) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CinemachineCamera), "Lens.FieldOfView"), lens);
        clip.EnsureQuaternionContinuity();
    }

    internal static void Handheld(CinemachineCamera drone, float amplitude)
    {
        var noise = drone.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null) noise = Undo.AddComponent<CinemachineBasicMultiChannelPerlin>(drone.gameObject);
        noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_wideangle_mild.asset");
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = .6f;
        EditorUtility.SetDirty(noise);
    }

    static AnimationCurve Smooth(float[] times, float[] values)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i < times.Length; i++) curve.AddKey(times[i], values[i]);
        // Auto, not ClampedAuto: velocity carries through keys instead of stopping at every turn.
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        }
        return curve;
    }

    static AnimationCurve[] Spline(float[] times, Vector3[] points) => new[] { 0, 1, 2 }.Select(k => Smooth(times, points.Select(p => p[k]).ToArray())).ToArray();
    static Vector3 Sample(AnimationCurve[] spline, float t) => new(spline[0].Evaluate(t), spline[1].Evaluate(t), spline[2].Evaluate(t));
    static float Heading(AnimationCurve[] path, AnimationCurve[] aim, float t)
    {
        var d = Sample(aim, t) - Sample(path, t);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static void SetupFreezeEffects(SerializedObject source)
    {
        var freezer = (WaterFreezer)source.FindProperty("freezer").objectReferenceValue;
        Undo.RegisterFullObjectHierarchyUndo(freezer.gameObject, "Add glowing freeze effects");
        var settings = new SerializedObject(freezer);
        var ice = (Transform)settings.FindProperty("ice").objectReferenceValue;
        var bounds = ice.GetComponent<Renderer>().bounds;
        var g = new GameObject("Freeze front crystals"); g.transform.SetParent(freezer.transform, false);
        var particles = g.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = true; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.4f); main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.8f); main.startSpeed = 0; main.maxParticles = 1000; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(.3f, .85f, 1f, .9f);
        var emission = particles.emission; emission.rateOverTime = 450;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(5, .4f, bounds.size.z);
        var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(1, 3); velocity.x = new ParticleSystem.MinMaxCurve(-1, 1); velocity.z = new ParticleSystem.MinMaxCurve(-1, 1);
        var color = particles.colorOverLifetime; color.enabled = true; var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(new Color(.3f,.85f,1),0), new GradientColorKey(Color.white,1) }, new[] { new GradientAlphaKey(.9f,0), new GradientAlphaKey(0,1) }); color.color = gradient;
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyPack + "Textures/t_vfx_sun.png"));
        material.SetColor("_BaseColor", new Color(1,2.5f,4,1)); material.SetFloat("_Surface",1); material.SetFloat("_Blend",2); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",1); material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
        AssetDatabase.CreateAsset(material, Folder + "Freeze crystal glow.mat"); particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        var lightObject = new GameObject("Freeze front light"); lightObject.transform.SetParent(freezer.transform, false);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.25f,.7f,1); light.range = 95; light.intensity = 35; light.shadows = LightShadows.None; light.enabled = false;
        settings.FindProperty("freezeParticles").objectReferenceValue = particles; settings.FindProperty("freezeLight").objectReferenceValue = light; settings.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupSky()
    {
        string include = "Assets/Shaders/CartoonSunDirection.hlsl";
        File.WriteAllText(include, "float4 _CartoonSunDirection;\nvoid MainLightDirection_float(out float3 Direction)\n{\n#if SHADERGRAPH_PREVIEW\nDirection = float3(0.5,0.5,0);\n#else\nDirection = _CartoonSunDirection.xyz;\n#endif\n}\n");
        AssetDatabase.ImportAsset(include);
        string graphPath = "Assets/Shaders/CartoonSkyURP.shadergraph";
        var graph = File.ReadAllText(SkyPack + "Shaders/sg_ZL_cartoonSky.shadergraph")
            .Replace("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget", "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget")
            .Replace("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget", "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget")
            .Replace("a3b2e14e5ab0dc944b1f4c89980da914", AssetDatabase.AssetPathToGUID(include));
        File.WriteAllText(graphPath, graph); AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport);
        var sky = new Material(AssetDatabase.LoadAssetAtPath<Material>(SkyPack + "Materials/mat_skybox_DayNightCycle.mat"));
        sky.shader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
        sky.SetColor("_Sun_zenith_color", new Color(1,.9f,.65f)); sky.SetColor("_Sun_dawn_color", new Color(1,.45f,.15f));
        sky.SetFloat("_Day_cycle_time", .5f);
        Shader.SetGlobalVector("_CartoonSunDirection", -RenderSettings.sun.transform.forward);
        AssetDatabase.CreateAsset(sky, Folder + "Cartoon day night sky.mat");
        RenderSettings.skybox = sky;
        var cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>(); Undo.RecordObject(cycle,"Enable continuous day and night");
        var settings = new SerializedObject(cycle); settings.FindProperty("moonIntensity").floatValue = .65f;
        settings.FindProperty("nightAmbient").colorValue = new Color(.10f,.12f,.18f);
        settings.FindProperty("nightReflectionIntensity").floatValue = .2f;
        settings.ApplyModifiedPropertiesWithoutUndo(); cycle.enabled = true;
    }
}
