using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Imports the Mixamo boss characters (Maw J Laygo, Mutant, Warrok, Ganfaul) and Abe, sets up
// Humanoid rigs and URP materials, builds their animator controllers from the Creature Pack
// and Pro Magic Pack clips, and creates the boss prefabs in Assets/Bosses (outside
// Assets/Combat so the vampire tools never touch them). Re-runnable.
public static class BossSetup
{
    private const string Characters = "Assets/Characters/Bosses";
    private const string Clips = Characters + "/Animations";
    // The vampires' grounded copies of the Creature Pack clips (VampireAnimationSetup). The raw
    // Creature FBX clips keep their root height unbaked, which floated the brutes.
    private const string VampireClips = "Assets/Combat/Vampire animations";
    public const string Output = "Assets/Bosses";

    public const string EarthBoss = Output + "/Earth Guardian (Maw).prefab";
    public const string IceBoss = Output + "/Ice Guardian (Mutant).prefab";
    public const string FireBoss = Output + "/Fire Guardian (Warrok).prefab";
    public const string FinalBoss = Output + "/Final Boss (Ganfaul).prefab";
    public const string AbePrefab = Output + "/Helper (Abe).prefab";

    private static readonly string[] Models = { "Maw/Maw", "Mutant/Mutant", "Warrok/Warrok", "Ganfaul/Ganfaul", "Abe/Abe" };

    [MenuItem("Wulang/Bosses/Build bosses")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets", "Bosses");
        foreach (var model in Models) ImportModel($"{Characters}/{model}.fbx");
        foreach (var path in Directory.GetFiles(Clips, "*.fbx")) ImportClip(path.Replace('\\', '/'));
        TelegraphMaterial();

        var brute = Controller(Output + "/Brute.controller", Vampire("breathing idle"), Vampire("Walk forward"),
            Vampire("Run forward"), Vampire("dying"),
            ("Roar", Clip(Clips, "mutant roaring")), ("Punch", Clip(Clips, "mutant punch")), ("Swipe", Vampire("Melee swipe")),
            ("Leap", Clip(Clips, "mutant jump attack")), ("Attack2H", Clip(Clips, "Standing 2H Magic Attack 01")),
            ("AreaCast", Clip(Clips, "Standing 2H Magic Area Attack 01")), ("Cast", Clip(Clips, "Standing 2H Cast Spell 01")),
            ("HitLarge", Clip(Clips, "Standing React Large From Front")));
        var caster = Controller(Output + "/Caster.controller", Clip(Clips, "standing idle"), Clip(Clips, "Standing Walk Forward"),
            Clip(Clips, "Standing Run Forward"), Clip(Clips, "Standing React Death Backward"),
            ("Roar", Clip(Clips, "Standing 2H Cast Spell 01")), ("Attack1H", Clip(Clips, "Standing 1H Magic Attack 01")),
            ("Attack2H", Clip(Clips, "Standing 2H Magic Attack 01")), ("Attack2HB", Clip(Clips, "Standing 2H Magic Attack 02")),
            ("AreaCast", Clip(Clips, "Standing 2H Magic Area Attack 01")), ("Cast", Clip(Clips, "Standing 2H Cast Spell 01")),
            ("Leap", Clip(Clips, "mutant jump attack")), ("Block", Clip(Clips, "Standing Block Idle")),
            ("HitLarge", Clip(Clips, "Standing React Large From Front")));

        var steam = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Combat/Steam burst.prefab");
        Boss(EarthBoss, $"{Characters}/Maw/Maw.fbx", ElementType.Earth, brute, steam, 1.8f, 500f, EarthMoves(brute));
        Boss(IceBoss, $"{Characters}/Mutant/Mutant.fbx", ElementType.Ice, brute, steam, 2f, 1000f, IceMoves(brute));
        Boss(FireBoss, $"{Characters}/Warrok/Warrok.fbx", ElementType.Fire, brute, steam, 2f, 1200f, FireMoves(brute));
        Boss(FinalBoss, $"{Characters}/Ganfaul/Ganfaul.fbx", ElementType.Lightning, caster, steam, 2f, 1600f, FinalMoves(caster));
        FinalBossPhases();
        foreach (var boss in new[] { EarthBoss, IceBoss, FireBoss, FinalBoss }) RootMotionSetup.AddBody(boss);
        Abe();
        AbeSeated();
        AssetDatabase.SaveAssets();
        Debug.Log("BossSetup: 4 bosses and Abe built in " + Output);
    }

    // Humanoid, avatar from its own skeleton, embedded textures extracted for URP Lit materials.
    private static void ImportModel(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new FileNotFoundException(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.SaveAndReimport();
        MapMixamoIfNeeded(importer, path);
        string textures = Path.GetDirectoryName(path).Replace('\\', '/') + "/Textures";
        if (!AssetDatabase.IsValidFolder(textures))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), "Textures");
            importer.ExtractTextures(textures);
            AssetDatabase.Refresh();
            importer.SaveAndReimport();
        }
    }

    // Unity's auto-mapper fails on rigs with missing finger bones (the Mutant has none on its
    // left hand). Mixamo names are fixed, so map the required bones explicitly.
    private static readonly (string human, string mixamo)[] MixamoBones =
    {
        ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Spine1"), ("UpperChest", "Spine2"), ("Neck", "Neck"), ("Head", "Head"),
        ("LeftUpperLeg", "LeftUpLeg"), ("LeftLowerLeg", "LeftLeg"), ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToeBase"),
        ("RightUpperLeg", "RightUpLeg"), ("RightLowerLeg", "RightLeg"), ("RightFoot", "RightFoot"), ("RightToes", "RightToeBase"),
        ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftArm"), ("LeftLowerArm", "LeftForeArm"), ("LeftHand", "LeftHand"),
        ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightArm"), ("RightLowerArm", "RightForeArm"), ("RightHand", "RightHand"),
    };

    private static void MapMixamoIfNeeded(ModelImporter importer, string path)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar != null && avatar.isValid && avatar.isHuman) return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var names = model.GetComponentsInChildren<Transform>().Select(t => t.name).ToHashSet();
        var description = importer.humanDescription;
        description.human = MixamoBones.Where(b => names.Contains("mixamorig:" + b.mixamo))
            .Select(b => new HumanBone { humanName = b.human, boneName = "mixamorig:" + b.mixamo, limit = new HumanLimit { useDefaultValues = true } })
            .ToArray();
        importer.humanDescription = description;
        importer.SaveAndReimport();
        avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman) throw new System.InvalidOperationException("Could not build a Humanoid avatar for " + path);
    }

    private static void ImportClip(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        bool loop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("talk");
        // Only strides and the leap really travel; every other action plays on the spot.
        bool travels = lower.Contains("walk") || lower.Contains("run") || lower.Contains("jump attack");
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.loopTime = loop;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            // Height baked into the pose (as the vampires' clips are): unbaked, a cast's or slam's crouch was
            // thrown away as root motion while the legs still bent, lifting the feet up to a metre off the floor.
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            // Unbaked hip sway became root motion, so RootMotionBody slid the whole boss on every punch and cast.
            clip.lockRootPositionXZ = !travels;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // Re-grounds the existing bosses and Abe without rebuilding their prefabs: re-imports every
    // boss clip with the root settings above and points the Brute controller at the vampire clips
    // in place, so prefab, region, arena and story references all survive.
    [MenuItem("Wulang/Bosses/Fix animation roots")]
    public static void FixRoots()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        foreach (var path in Directory.GetFiles(Clips, "*.fbx")) ImportClip(path.Replace('\\', '/'));
        var brute = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/Brute.controller");
        var states = brute.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var tree = (BlendTree)states.First(s => s.name == "Locomotion").motion;
        var children = tree.children;
        children[0].motion = Vampire("breathing idle");
        children[1].motion = Vampire("Walk forward");
        children[2].motion = Vampire("Run forward");
        tree.children = children;
        states.First(s => s.name == "Swipe").motion = Vampire("Melee swipe");
        states.First(s => s.name == "Death").motion = Vampire("dying");
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(brute);
        // Abe sits at his camps now (AbeSeated), so the standing foot IK and ground offset are the bosses' only.
        foreach (var path in new[] { EarthBoss, IceBoss, FireBoss, FinalBoss }) Ground(path);
        AssetDatabase.SaveAssets();
        Debug.Log("BossSetup: boss and Abe clips re-grounded; Brute uses the vampire locomotion, swipe and death clips.");
    }

    private const string Gestures = Clips + "/Gestures";
    public const string UpperBodyMask = Output + "/Upper body.mask";
    public const string StandingAbe = Output + "/NPC standing.overrideController";

    // Abe sits by his camp fires: Sitting Idle is his default pose, and an upper-body "Gestures"
    // layer (legs masked out, so he stays seated) holds one state per clip of the Gestures Pack
    // for NpcGestures to play while he talks. He loses the standing foot IK. Re-runnable.
    [MenuItem("Wulang/Bosses/Abe sitting and gestures")]
    public static void AbeSeated()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        ImportClip(Clips + "/Sitting Idle.fbx");
        var gestures = Directory.GetFiles(Gestures, "*.fbx").Select(p => p.Replace('\\', '/')).ToArray();
        foreach (var path in gestures) ImportClip(path);

        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMask);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, UpperBodyMask);
        }
        for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            mask.SetHumanoidBodyPartActive(part, part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head or AvatarMaskBodyPart.LeftArm
                or AvatarMaskBodyPart.RightArm or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers);
        EditorUtility.SetDirty(mask);

        var npc = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/NPC.controller");
        var machine = npc.layers[0].stateMachine;
        var sit = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Sit") ?? machine.AddState("Sit");
        sit.motion = Clip(Clips, "Sitting Idle");
        sit.iKOnFeet = false;
        machine.defaultState = sit;
        while (npc.layers.Length > 1) npc.RemoveLayer(1);
        npc.AddLayer("Gestures");
        var layers = npc.layers;
        layers[1].avatarMask = mask;
        layers[1].defaultWeight = 1f;
        // Additive: the gestures are standing clips, and overriding the seated arms put his hands in the ground.
        layers[1].blendingMode = AnimatorLayerBlendingMode.Additive;
        npc.layers = layers;
        var gestureMachine = npc.layers[1].stateMachine;
        var empty = gestureMachine.AddState("Empty");
        gestureMachine.defaultState = empty;
        foreach (var path in gestures)
        {
            var state = gestureMachine.AddState(Path.GetFileNameWithoutExtension(path));
            state.motion = Clip(Gestures, Path.GetFileNameWithoutExtension(path));
            var back = state.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.88f;
            back.duration = 0.3f;
        }
        EditorUtility.SetDirty(npc);

        // In the Sanctum he stands to make his offer: the same controller (gestures included) with
        // the sitting idle swapped for the standing one.
        AssetDatabase.DeleteAsset(StandingAbe);
        var standingController = new AnimatorOverrideController(npc) { name = "NPC standing" };
        standingController[Clip(Clips, "Sitting Idle")] = Clip(Clips, "standing idle");
        AssetDatabase.CreateAsset(standingController, StandingAbe);

        var root = PrefabUtility.LoadPrefabContents(AbePrefab);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // the look-at must track even off screen edges
            var standing = animator.GetComponent<RootMotionBody>();
            if (standing != null) Object.DestroyImmediate(standing);
            if (animator.GetComponent<NpcGestures>() == null) animator.gameObject.AddComponent<NpcGestures>();
            // Seated, he is about 1.3 m tall: the capsule the interaction ray finds follows.
            var capsule = root.GetComponent<CapsuleCollider>();
            capsule.center = Vector3.up * 0.65f;
            capsule.height = 1.3f;
            PrefabUtility.SaveAsPrefabAsset(root, AbePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"BossSetup: Abe sits, with {gestures.Length} upper-body gestures.");
    }

    // Measures how far the rig's soles sit off the ground in its idle pose and stores it on its
    // RootMotionBody, which shifts the body by that much so the feet rest on the ground.
    private static void Ground(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var probe = Object.Instantiate(prefab);
        probe.hideFlags = HideFlags.HideAndDontSave;
        float offset;
        try
        {
            foreach (var behaviour in probe.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var animator = probe.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            var locomotion = (AnimatorController)animator.runtimeAnimatorController;
            var tree = (BlendTree)locomotion.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.motion;
            var soles = CharacterAnimationChecks.Soles(probe, animator, (AnimationClip)tree.children[0].motion, 10);
            offset = soles[soles.Length / 2];
        }
        finally { Object.DestroyImmediate(probe); }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var settings = new SerializedObject(root.GetComponentInChildren<RootMotionBody>());
            settings.FindProperty("groundOffset").floatValue = offset;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log($"BossSetup: {prefab.name} idle soles {offset:+0.00;-0.00} m, body shifted to compensate.");
    }

    private static AnimationClip Vampire(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{VampireClips}/{name}.anim");
        if (clip == null) throw new FileNotFoundException($"No vampire clip {VampireClips}/{name}.anim (run the vampire animation setup first)");
        return clip;
    }

    private static AnimationClip Clip(string folder, string name)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath($"{folder}/{name}.fbx").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) throw new FileNotFoundException($"No animation clip in {folder}/{name}.fbx");
        return clip;
    }

    // Locomotion blend (Speed: idle 0, walk 1.6, run 4), one-shot action states that return to
    // Locomotion, and a terminal Death state, matching what CombatTarget and BossBrain play.
    private static AnimatorController Controller(string path, AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip death, params (string state, AnimationClip clip)[] actions)
    {
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed";
        tree.AddChild(idle);
        tree.AddChild(walk);
        tree.AddChild(run);
        machine.defaultState = locomotion;
        foreach (var (state, clip) in actions)
        {
            var action = machine.AddState(state);
            action.motion = clip;
            var back = action.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.2f;
        }
        machine.AddState("Death").motion = death;
        RootMotionSetup.Configure(controller);
        return controller;
    }

    private static BossBrain.BossAttack Move(string name, BossBrain.AttackKind kind, string state, AnimatorController controller,
        float hitAt, float minRange, float maxRange, float damage, float radius, float cooldown, ElementType element = ElementType.None,
        int count = 1, bool atSelf = false, bool line = false, bool lobbed = false, bool phaseTwo = false)
    {
        var clip = controller.layers[0].stateMachine.states.First(s => s.state.name == state).state.motion as AnimationClip;
        return new BossBrain.BossAttack
        {
            name = name, kind = kind, state = state, element = element, windup = clip.length * hitAt, duration = clip.length * 0.9f,
            minRange = minRange, maxRange = maxRange, damage = damage, radius = radius, cooldown = cooldown, count = count,
            atSelf = atSelf, line = line, lobbed = lobbed, phaseTwoOnly = phaseTwo,
        };
    }

    // Earth Guardian, the tutorial boss: slow, long warnings, shows off Earth's spikes and boulders.
    private static BossBrain.BossAttack[] EarthMoves(AnimatorController c) => new[]
    {
        Move("Swipe", BossBrain.AttackKind.Melee, "Swipe", c, 0.45f, 0f, 4.5f, 14f, 3f, 3f),
        Move("Spike Line", BossBrain.AttackKind.Ground, "AreaCast", c, 0.6f, 5f, 22f, 18f, 2.2f, 6f, ElementType.Earth, count: 5, line: true),
        Move("Boulder Throw", BossBrain.AttackKind.Projectile, "Attack2H", c, 0.5f, 8f, 30f, 16f, 2.5f, 5f, ElementType.Earth, lobbed: true),
        Move("Quake Leap", BossBrain.AttackKind.Ground, "Leap", c, 0.55f, 4f, 14f, 22f, 5f, 8f, ElementType.Earth, atSelf: true, phaseTwo: true),
    };

    private static BossBrain.BossAttack[] IceMoves(AnimatorController c) => new[]
    {
        Move("Punch", BossBrain.AttackKind.Melee, "Punch", c, 0.45f, 0f, 4f, 18f, 3f, 2.5f),
        Move("Swipe", BossBrain.AttackKind.Melee, "Swipe", c, 0.45f, 0f, 4.5f, 16f, 3.2f, 3.5f),
        Move("Glacial Slam", BossBrain.AttackKind.Ground, "Leap", c, 0.55f, 3f, 12f, 24f, 5.5f, 7f, ElementType.Ice, atSelf: true),
        Move("Shard Volley", BossBrain.AttackKind.Projectile, "Attack2H", c, 0.5f, 7f, 30f, 12f, 1.5f, 5f, ElementType.Ice, count: 3),
        Move("Ice Rain", BossBrain.AttackKind.Ground, "AreaCast", c, 0.6f, 6f, 30f, 18f, 3f, 8f, ElementType.Ice, count: 4, phaseTwo: true),
    };

    private static BossBrain.BossAttack[] FireMoves(AnimatorController c) => new[]
    {
        Move("Punch", BossBrain.AttackKind.Melee, "Punch", c, 0.45f, 0f, 4f, 20f, 3f, 2.5f),
        Move("Swipe", BossBrain.AttackKind.Melee, "Swipe", c, 0.45f, 0f, 4.5f, 18f, 3.2f, 3.5f),
        Move("Fireball Volley", BossBrain.AttackKind.Projectile, "Attack2H", c, 0.5f, 7f, 30f, 15f, 2.5f, 5f, ElementType.Fire, count: 3, lobbed: true),
        Move("Meteor Rain", BossBrain.AttackKind.Ground, "AreaCast", c, 0.6f, 5f, 30f, 20f, 3f, 7f, ElementType.Fire, count: 5),
        Move("Eruption Slam", BossBrain.AttackKind.Ground, "Leap", c, 0.55f, 3f, 12f, 28f, 6f, 8f, ElementType.Fire, atSelf: true, phaseTwo: true),
    };

    // Ganfaul: the same three kinds of attack, always in his current phase's element.
    private static BossBrain.BossAttack[] FinalMoves(AnimatorController c) => new[]
    {
        Move("Strike", BossBrain.AttackKind.Melee, "Attack1H", c, 0.45f, 0f, 4.5f, 18f, 3.2f, 2.5f),
        Move("Bolt", BossBrain.AttackKind.Projectile, "Attack2H", c, 0.5f, 6f, 35f, 14f, 2f, 3.5f, count: 3),
        Move("Barrage", BossBrain.AttackKind.Projectile, "Attack2HB", c, 0.5f, 6f, 35f, 16f, 2.5f, 5f, count: 3, lobbed: true),
        Move("Element Storm", BossBrain.AttackKind.Ground, "AreaCast", c, 0.6f, 4f, 35f, 20f, 3.5f, 6f, count: 3),
        Move("Fault Line", BossBrain.AttackKind.Ground, "Cast", c, 0.6f, 6f, 30f, 20f, 2f, 7f, count: 6, line: true, phaseTwo: true),
        Move("Crashing Leap", BossBrain.AttackKind.Ground, "Leap", c, 0.55f, 3f, 14f, 26f, 5.5f, 8f, atSelf: true, phaseTwo: true),
    };

    private static void Boss(string path, string fbx, ElementType element, AnimatorController controller, GameObject steam,
        float scale, float health, BossBrain.BossAttack[] moves)
    {
        string label = Path.GetFileNameWithoutExtension(path);
        CombatSetup.Target(element, controller, steam, fbx, path, label, 0.2f);
        ElementalEffectsSetup.ApplyTo(path, element);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = label;
            root.transform.localScale = Vector3.one * scale;
            // Bosses don't wear the vampires' overhead badge; they get the big top bar instead.
            Object.DestroyImmediate(root.GetComponent<EnemyOverheadUI>());
            Object.DestroyImmediate(root.GetComponent<EnemyDetection>());
            var body = root.GetComponent<Rigidbody>();
            body.mass = 40f;
            body.linearDamping = 1f;
            var target = new SerializedObject(root.GetComponent<CombatTarget>());
            target.FindProperty("maxHealth").floatValue = health;
            target.FindProperty("trainingRespawnDelay").floatValue = 0f;
            target.FindProperty("statusScale").floatValue = 0.3f;
            target.ApplyModifiedPropertiesWithoutUndo();
            var brain = root.GetComponent<BossBrain>() ?? root.AddComponent<BossBrain>();
            brain.Configure(moves, 30f, "Roar", "HitLarge");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Ganfaul cycles elements: four auras toggled by ElementPhases, and he resists anything
    // that isn't the current phase's weakness.
    private static void FinalBossPhases()
    {
        var root = PrefabUtility.LoadPrefabContents(FinalBoss);
        try
        {
            var visuals = new SerializedObject(root.GetComponent<CombatTargetVisuals>());
            var skin = (SkinnedMeshRenderer)visuals.FindProperty("skin").objectReferenceValue;
            var lightningAura = (GameObject)visuals.FindProperty("affinityEffect").objectReferenceValue;
            visuals.FindProperty("affinityEffect").objectReferenceValue = null;
            visuals.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name.EndsWith(" phase aura")) Object.DestroyImmediate(child.gameObject);
            var auras = new GameObject[4];
            foreach (var element in new[] { ElementType.Ice, ElementType.Fire, ElementType.Lightning, ElementType.Earth })
                auras[(int)element - 1] = element == ElementType.Lightning ? lightningAura
                    : ElementalEffectsSetup.ExtraAura(element, root.transform, skin);
            var phases = root.GetComponent<ElementPhases>() ?? root.AddComponent<ElementPhases>();
            var settings = new SerializedObject(phases);
            var list = settings.FindProperty("auras");
            list.arraySize = 4;
            for (int i = 0; i < 4; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = auras[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var target = new SerializedObject(root.GetComponent<CombatTarget>());
            target.FindProperty("nonWeaknessMultiplier").floatValue = 0.25f;
            target.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, FinalBoss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Abe: no combat, an idle/talk controller, a collider so the interaction ray can find him,
    // and a single-use-off Interactable for the shop.
    private static void Abe()
    {
        var npc = Controller(Output + "/NPC.controller", Clip(Clips, "standing idle"), Clip(Clips, "Standing Walk Forward"),
            Clip(Clips, "Standing Run Forward"), Clip(Clips, "Standing React Death Backward"), ("Talk", Clip(Clips, "Talk01")));
        var root = new GameObject("Helper (Abe)");
        var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/Abe/Abe.fbx"), root.transform);
        model.name = "Model";
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = npc;
        animator.applyRootMotion = false;
        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up * 0.9f;
        collider.height = 1.8f;
        collider.radius = 0.4f;
        var interactable = root.AddComponent<Interactable>();
        var settings = new SerializedObject(interactable);
        settings.FindProperty("prompt").stringValue = "Press F to trade with [Abe]";
        settings.FindProperty("singleUse").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var merchant = root.AddComponent<Merchant>();
        var shop = new SerializedObject(merchant);
        var stock = shop.FindProperty("stock");
        stock.arraySize = 1;
        stock.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(ItemsSetup.PotionPath);
        shop.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, AbePrefab);
        Object.DestroyImmediate(root);
    }

    // The warning ring under ground attacks: the existing rune-circle texture on an unlit
    // transparent material, tinted per element at runtime.
    private static void TelegraphMaterial()
    {
        const string path = "Assets/Resources/BossTelegraph.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var rune = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Effects/Rune circle.mat");
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "BossTelegraph" };
        if (rune != null && rune.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", rune.GetTexture("_BaseMap"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f); // additive
        material.SetColor("_BaseColor", Color.white);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        AssetDatabase.CreateAsset(material, path);
    }
}
