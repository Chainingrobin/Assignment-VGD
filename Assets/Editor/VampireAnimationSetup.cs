using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VampireAnimationSetup
{
    [MenuItem("Wulang/Combat/Check chase recovery and lunge (Play mode)")]
    public static void CheckChaseAndLunge()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var cameraObject = new GameObject("Chase test camera");
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Combat/Lightning vampire.prefab"), new Vector3(20000,100,20000), Quaternion.identity);
        try
        {
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = instance.transform.position + Vector3.forward * 4 + Vector3.up;
            var behavior = instance.GetComponent<VampireMelee>(); behavior.enabled = false;
            var sight = instance.GetComponent<EnemyDetection>(); sight.enabled = false;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(VampireMelee).GetField("player",flags).SetValue(behavior,camera);
            typeof(VampireMelee).GetField("pauseRemaining",flags).SetValue(behavior,4f);
            typeof(EnemyDetection).GetField("<CanSeePlayer>k__BackingField",flags).SetValue(sight,true);
            behavior.SendMessage("FixedUpdate");
            var body = instance.GetComponent<Rigidbody>();
            if (body.linearVelocity.z <= 0 || (float)typeof(VampireMelee).GetField("pauseRemaining",flags).GetValue(behavior) != 0)
                throw new InvalidOperationException("Reacquisition leaves chase paused.");
            float duration = (float)typeof(VampireMelee).GetField("attackDuration",flags).GetValue(behavior);
            typeof(VampireMelee).GetField("attackRemaining",flags).SetValue(behavior,duration-1.1f);
            behavior.SendMessage("FixedUpdate");
            if (body.linearVelocity.z < 1f || Mathf.Abs(body.linearVelocity.x)>0.01f)
                throw new InvalidOperationException("Swipe lunge must move forward.");
            typeof(VampireMelee).GetField("attackRemaining",flags).SetValue(behavior,duration-1.8f);
            behavior.SendMessage("FixedUpdate");
            if (Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).sqrMagnitude>0.001f)
                throw new InvalidOperationException("Lunge continues after strike.");
            Debug.Log("Chase reacquisition, forward lunge and recovery stop PASS.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
    [MenuItem("Wulang/Combat/Check in-place attack and death (Play mode)")]
    public static void CheckGroundedAttack()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Combat/Lightning vampire.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab, new Vector3(20000,100,20000), Quaternion.identity);
        try
        {
            instance.GetComponent<VampireMelee>().enabled = false;
            var target = instance.GetComponent<CombatTarget>();
            var animator = instance.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            foreach (var side in new[]{0,1})
            {
                var arm = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var forearm = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                if (!forearm.IsChildOf(arm) || !hand.IsChildOf(forearm)) throw new InvalidOperationException("Invalid arm mapping.");
            }
            Vector3 rootPosition = instance.transform.position;
            Quaternion rootRotation = instance.transform.rotation;
            animator.Play("Melee swipe", 0, 0); animator.Update(0);
            Vector3 firstFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            float legMotion = 0f;
            for (int i=0;i<150;i++)
            {
                animator.Update(1f/60f);
                legMotion = Mathf.Max(legMotion, Vector3.Distance(firstFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot).position));
                if (Vector3.Distance(rootPosition,instance.transform.position)>0.01f || Quaternion.Angle(rootRotation,instance.transform.rotation)>0.1f)
                    throw new InvalidOperationException("Attack moves or turns its root.");
            }
            if (legMotion < 0.02f) throw new InvalidOperationException("Full-body attack legs are masked.");
            var detection = instance.GetComponent<EnemyDetection>();
            Vector3 eye = target.HeadPosition;
            if (!detection.CanDetect(eye-instance.transform.forward*1.4f) || detection.CanDetect(eye-instance.transform.forward*1.6f)) throw new InvalidOperationException("Proximity threshold failed.");
            target.TakeDamage(10000); animator.Update(0.5f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Death") || animator.speed==0 || !animator.gameObject.activeInHierarchy) throw new InvalidOperationException("Death playback failed.");
            target.ResetTarget(); animator.Update(0);
            if (target.IsDead || !animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")) throw new InvalidOperationException("Respawn failed.");
            Debug.Log("Full-body in-place attack, arm mappings, proximity, death and respawn PASS.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
    [MenuItem("Wulang/Combat/Update roaming animations")]
    public static void UpdateRoaming()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/Vampire melee.controller");
        var machine = controller.layers[0].stateMachine;
        var swipe = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Vampire/Creature NPC animations/mutant swiping.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        if (!swipe.humanMotion) throw new InvalidOperationException("Mutant swipe must be Humanoid.");
        machine.states.First(s => s.state.name == "Melee swipe").state.motion = swipe;
        if (controller.layers.Length > 1) controller.RemoveLayer(1);
        var attackState = machine.states.First(s => s.state.name == "Melee swipe").state;
        var settings = AnimationUtility.GetAnimationClipSettings(swipe);
        var inPlace = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Melee swipe.anim");
        EditorUtility.CopySerialized(swipe, inPlace);
        inPlace.name = "Melee swipe";
        settings.loopTime = false;
        settings.loopBlendPositionXZ = false;
        settings.loopBlendOrientation = true;
        settings.keepOriginalOrientation = true;
        settings.loopBlendPositionY = true;
        AnimationUtility.SetAnimationClipSettings(inPlace, settings);
        EditorUtility.SetDirty(inPlace);
        attackState.motion = inPlace;
        foreach (var transition in attackState.transitions) { transition.exitTime = 1f; transition.duration = 0.3f; }
        var death = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Death");
        if (death == null) death = machine.AddState("Death");
        death.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/dying.anim");
        if (death.motion == null) throw new InvalidOperationException("Missing death clip.");
        var display = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Display");
        if (display == null) display = machine.AddState("Display");
        display.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/flexing muscles.anim");
        if (display.motion == null) throw new InvalidOperationException("Missing display clip.");
        if (display.transitions.Length == 0)
        {
            var exit = display.AddTransition(machine.states.First(s => s.state.name == "Locomotion").state);
            exit.hasExitTime = true; exit.exitTime = 1f; exit.duration = 0.2f;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Check();
    }
    [MenuItem("Wulang/Combat/Check vampire animation wiring")]
    public static void Check()
    {
        var targets = UnityEngine.Object.FindObjectsByType<CombatTarget>(FindObjectsSortMode.None);
        if (targets.Length == 0) throw new InvalidOperationException("No vampires in scene.");
        foreach (var target in targets)
        {
            var animator = target.GetComponentInChildren<Animator>(true);
            if (target.GetComponent<VampireMelee>() == null || animator == null || !animator.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Invalid vampire wiring: " + target.name);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null || controller.animationClips.Any(c => !c.humanMotion))
                throw new InvalidOperationException("Non-humanoid controller: " + target.name);
        }
        Debug.Log("Vampire animation wiring PASS");
    }
    const string Folder = "Assets/Combat/Vampire animations";
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Combat", "Vampire animations");
        string softPath = Folder + "/Soft mote.mat";
        var soft = AssetDatabase.LoadAssetAtPath<Material>(softPath);
        if (soft == null)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = "Soft radial mote";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, Folder + "/Soft mote.asset");
            soft = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            soft.SetTexture("_BaseMap", texture);
            soft.SetColor("_BaseColor", Color.white);
            soft.SetFloat("_Surface", 1);
            soft.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            soft.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            soft.SetFloat("_ZWrite", 0);
            soft.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            soft.renderQueue = 3000;
            AssetDatabase.CreateAsset(soft, softPath);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[]{"Assets/Characters/Vampire/Creature NPC animations"}))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar = assets.OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid humanoid: " + path);
            string name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("mutant ", "");
            name = name == "walking" ? "Walk forward" : name == "run" ? "Run forward" : name == "swiping" ? "Melee swipe" : name == "idle" ? "Idle" : name;
            var source = assets.OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            if (!source.humanMotion) throw new InvalidOperationException("Not humanoid: " + path);
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk forward" || name == "Run forward" || name == "breathing idle";
            settings.loopBlend = settings.loopTime;
            settings.loopBlendPositionY = true;
            // Extract horizontal motion so applyRootMotion=false leaves locomotion in place.
            settings.loopBlendOrientation = settings.loopBlendPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string destination = Folder + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
            if (existing == null) AssetDatabase.CreateAsset(clip, destination);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); }
        }
        string controllerPath = Folder + "/Vampire melee.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            var blend = new BlendTree { name = "Idle walk run", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Idle.anim"), 0);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Walk forward.anim"), 1.6f);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Run forward.anim"), 3.6f);
            locomotion.motion = blend;
            machine.defaultState = locomotion;
            var attack = machine.AddState("Melee swipe");
            attack.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Melee swipe.anim");
            var exit = attack.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = 0.95f; exit.duration = 0.15f;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Combat"}))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.GetComponent<CombatTarget>();
                if (target == null) continue;
                var animator = root.GetComponentInChildren<Animator>();
                if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Invalid vampire Avatar");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particles.name.Contains("flames") || (target.Affinity == ElementType.Fire && particles.name.Contains("affinity aura"))) continue;
                    particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = soft;
                    var sheet = particles.textureSheetAnimation; sheet.enabled = false;
                    var main = particles.main; main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
                }
                if (root.GetComponent<VampireMelee>() == null) root.AddComponent<VampireMelee>();
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name.Contains("affinity aura")))
                {
                    var main = particles.main; main.startSize = target.Affinity == ElementType.Fire ? 0.5f : 0.18f;
                    main.startColor = ElementVisuals.ColorFor(target.Affinity) * 1.8f;
                    var emission = particles.emission; emission.rateOverTime = 40f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Vampire setup complete: validated humanoid clips, vampire avatars, blend tree and harmless melee.");
    }
}
