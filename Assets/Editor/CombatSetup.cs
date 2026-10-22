using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatSetup
{
    private const string Folder = "Assets/Combat";
    private const string ProjectileFolder = "Assets/GabrielAguiarProductions/Unique_Projectiles_Volume_1/Materials/";

    [MenuItem("Wulang/Combat/Set up training targets and HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before setting up combat.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Combat");
        var caster = Object.FindFirstObjectByType<SpellCaster>();
        var hud = Object.FindFirstObjectByType<ResponsiveHUD>();
        if (caster == null || hud == null) throw new System.InvalidOperationException("Open SampleScene with its player and HUD first.");
        var existing = GameObject.Find("Combat training");
        if (existing != null) throw new System.InvalidOperationException("Combat training already exists; edit its prefabs instead of rebuilding.");

        var arc = new Material(AssetDatabase.LoadAssetAtPath<Material>(ProjectileFolder + "Electric_Add.mat"));
        ConfigureArc(arc);
        AssetDatabase.CreateAsset(arc, Folder + "/Chain arc.mat");
        var steam = Steam();
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Vampire training idle.controller");
        var idle = controller.layers[0].stateMachine.AddState("Idle");
        idle.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Animations/Locomotion Pack/idle.anim");
        controller.layers[0].stateMachine.defaultState = idle;

        var training = new GameObject("Combat training");
        Undo.RegisterCreatedObjectUndo(training, "Create combat training");
        var terrain = Terrain.activeTerrain;
        // Training stays near the original vampire, outside the armory and cinematic routes.
        var positions = new[] { new Vector3(564, 0, 573), new Vector3(568, 0, 573), new Vector3(564, 0, 569.5f), new Vector3(568, 0, 569.5f) };
        var elements = new[] { ElementType.Fire, ElementType.Ice, ElementType.Earth, ElementType.Lightning };
        for (int i = 0; i < elements.Length; i++)
        {
            var prefab = Target(elements[i], controller, steam);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(training.transform);
            var position = positions[i];
            position.y = terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f : 0.05f;
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        var casterSettings = new SerializedObject(caster);
        casterSettings.FindProperty("aoeDamage").floatValue = 40f;
        casterSettings.FindProperty("chainMaterial").objectReferenceValue = arc;
        casterSettings.ApplyModifiedProperties();
        BuildHUD(caster, hud);
        var reticle = Rect(hud.Content, "Combat reticle", new Vector2(14, 14), Vector2.zero);
        reticle.anchorMin = reticle.anchorMax = reticle.pivot = Vector2.one * 0.5f;
        Panel(Rect(reticle, "Horizontal", new Vector2(14, 2), new Vector2(0, -6)), new Color(1, 1, 1, 0.85f));
        Panel(Rect(reticle, "Vertical", new Vector2(2, 14), new Vector2(6, 0)), new Color(1, 1, 1, 0.85f));
        var reference = GameObject.Find("Vampire");
        if (reference != null) reference.SetActive(false);
        // Keep the authored HUD visible outside Play mode too.
        hud.transform.localScale = Vector3.one;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(caster.gameObject.scene);
        EditorSceneManager.SaveScene(caster.gameObject.scene);
        Selection.activeGameObject = training;
    }

    private static GameObject Target(ElementType element, RuntimeAnimatorController controller, GameObject steam)
    {
        var root = new GameObject(element + " vampire target");
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Vampire/Vampire A Lusth.fbx"), root.transform);
        model.name = "Vampire model";
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var materials = skin.sharedMaterials;
        var tint = Color.Lerp(Color.white, ElementVisuals.ColorFor(element), 0.45f);
        var material = new Material(materials[0]) { name = element + " vampire skin" };
        material.SetColor("_BaseColor", tint);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", ElementVisuals.ColorFor(element) * 0.7f);
        AssetDatabase.CreateAsset(material, Folder + "/" + material.name + ".mat");
        materials[0] = material;
        skin.sharedMaterials = materials;

        var body = root.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.linearDamping = 2f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.38f;

        var target = root.AddComponent<CombatTarget>();
        var settings = new SerializedObject(target);
        settings.FindProperty("affinity").enumValueIndex = (int)element;
        settings.FindProperty("model").objectReferenceValue = model.transform;
        settings.FindProperty("steamPrefab").objectReferenceValue = steam;
        settings.ApplyModifiedPropertiesWithoutUndo();

        var iceSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Blink/Art/Textures/StylizedIceTextures/Ice_3/Ice_3.mat");
        var ice = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Frozen shell.mat");
        if (ice == null)
        {
            ice = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Frozen shell" };
            ice.SetTexture("_BaseMap", iceSource.GetTexture("_BaseMap"));
            ice.SetTexture("_BumpMap", iceSource.GetTexture("_BumpMap"));
            ice.EnableKeyword("_NORMALMAP");
            ice.SetColor("_BaseColor", new Color(0.35f, 0.75f, 1f, 0.7f));
            ice.SetFloat("_Smoothness", 0.85f);
            ice.SetFloat("_Surface", 1f);
            ice.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ice.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ice.SetFloat("_ZWrite", 0f);
            ice.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ice.renderQueue = 3000;
            AssetDatabase.CreateAsset(ice, Folder + "/Frozen shell.mat");
        }
        var frozen = new GameObject("Frozen ice coating");
        frozen.transform.SetParent(skin.transform, false);
        frozen.transform.localScale = Vector3.one * 1.012f;
        var shell = frozen.AddComponent<SkinnedMeshRenderer>();
        shell.sharedMesh = skin.sharedMesh;
        shell.bones = skin.bones;
        shell.rootBone = skin.rootBone;
        shell.localBounds = skin.localBounds;
        shell.sharedMaterials = Enumerable.Repeat(ice, skin.sharedMaterials.Length).ToArray();
        shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        frozen.SetActive(false);

        var flame = AssetDatabase.LoadAssetAtPath<Material>(ProjectileFolder + "Flames01_Add_4x4.mat");
        var spark = AssetDatabase.LoadAssetAtPath<Material>(ProjectileFolder + "Square02_Add.mat");
        var burn = Particles(root.transform, "Temporary burn flames", flame, new Color(1f, 0.3f, 0.08f, 0.8f), 35f, 0.35f, true, skin);
        burn.gameObject.SetActive(false);
        var aura = Particles(root.transform, element + " affinity aura", element == ElementType.Fire ? flame : spark,
            ElementVisuals.ColorFor(element) * new Color(1, 1, 1, 0.65f), 10f, element == ElementType.Fire ? 0.2f : 0.06f, element == ElementType.Fire, skin);
        var visuals = root.AddComponent<CombatTargetVisuals>();
        var visualSettings = new SerializedObject(visuals);
        visualSettings.FindProperty("skin").objectReferenceValue = skin;
        visualSettings.FindProperty("frozenShell").objectReferenceValue = frozen;
        visualSettings.FindProperty("burnEffect").objectReferenceValue = burn.gameObject;
        visualSettings.FindProperty("affinityEffect").objectReferenceValue = aura.gameObject;
        visualSettings.ApplyModifiedPropertiesWithoutUndo();
        ConfigureFeedback(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + element + " vampire.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static ParticleSystem Particles(Transform parent, string name, Material material, Color color, float rate, float size, bool flames, SkinnedMeshRenderer skin = null)
    {
        var effect = new GameObject(name);
        if (parent != null) effect.transform.SetParent(parent, false);
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.maxParticles = 100;
        main.gravityModifier = -0.08f;
        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        if (skin != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skin;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
        }
        var colors = particles.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
        colors.color = gradient;
        if (flames)
        {
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = sheet.numTilesY = 4;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
        }
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return particles;
    }

    private static GameObject Steam()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Hovl Studio/AOE Magic spells Vol.1/Materials/Smoke3bcg.mat");
        var particles = Particles(null, "Steam burst (2m)", material, new Color(0.85f, 0.95f, 1f, 0.85f), 0, 2f, false);
        var main = particles.main;
        main.loop = false;
        ConfigureSteam(particles.gameObject);
        var shape = particles.shape;
        shape.radius = 1f;
        var prefab = PrefabUtility.SaveAsPrefabAsset(particles.gameObject, Folder + "/Steam burst.prefab");
        Object.DestroyImmediate(particles.gameObject);
        return prefab;
    }

    private static void ConfigureSteam(GameObject root)
    {
        if (root.GetComponent<SteamCloud>() == null) root.AddComponent<SteamCloud>();
        var settings = new SerializedObject(root.GetComponent<SteamCloud>());
        settings.FindProperty("duration").floatValue = 7f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var particles = root.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = 7f;
        main.startLifetime = 7f;
        main.startSpeed = 0.04f;
        main.gravityModifier = 0f;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = particles.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 48) });
        var color = particles.colorOverLifetime;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.05f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
    }

    private static void ConfigureArc(Material material)
    {
        // Particle flipbook masks made thin LineRenderers almost transparent.
        material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        foreach (var texture in new[] { "_BaseMap", "_MainTex" })
            if (material.HasProperty(texture)) material.SetTexture(texture, null);
        material.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f));
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SoftParticlesEnabled", 0f);
        material.DisableKeyword("_SOFTPARTICLES_ON");
        material.DisableKeyword("_FADING_ON");
        material.DisableKeyword("_FLIPBOOKBLENDING_ON");
        material.DisableKeyword("_EMISSION");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureFeedback(GameObject root)
    {
        if (root.GetComponent<EnemyDetection>() == null) root.AddComponent<EnemyDetection>();
        if (root.GetComponent<EnemyOverheadUI>() == null) root.AddComponent<EnemyOverheadUI>();
        var body = root.GetComponent<Rigidbody>();
        body.mass = 2f;
        body.linearDamping = 2f;
        var spark = AssetDatabase.LoadAssetAtPath<Material>(ProjectileFolder + "Square02_Add.mat");
        var seismic = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Seismic burst.prefab");
        if (seismic == null)
        {
            var burst = Particles(null, "Seismic burst", spark, ElementVisuals.ColorFor(ElementType.Lightning) * 2f, 0, 0.18f, false);
            var main = burst.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.gravityModifier = 0f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90, 0, 0);
            shape.radius = 0.5f;
            var emission = burst.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 40) });
            var light = burst.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ElementVisuals.ColorFor(ElementType.Lightning);
            light.range = 4f;
            light.intensity = 2f;
            seismic = PrefabUtility.SaveAsPrefabAsset(burst.gameObject, Folder + "/Seismic burst.prefab");
            Object.DestroyImmediate(burst.gameObject);
        }
        var target = new SerializedObject(root.GetComponent<CombatTarget>());
        target.FindProperty("seismicPrefab").objectReferenceValue = seismic;
        target.ApplyModifiedPropertiesWithoutUndo();
        var visuals = new SerializedObject(root.GetComponent<CombatTargetVisuals>());
        if (visuals.FindProperty("stunEffect").objectReferenceValue == null)
        {
            var orbit = Particles(root.transform, "Stun sparks", spark, new Color(1f, 0.7f, 1f), 24f, 0.12f, false);
            var main = orbit.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var shape = orbit.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90, 0, 0);
            shape.radius = 0.35f;
            visuals.FindProperty("stunEffect").objectReferenceValue = orbit.transform;
            orbit.gameObject.SetActive(false);
        }
        visuals.FindProperty("model").objectReferenceValue = target.FindProperty("model").objectReferenceValue;
        var sparks = ((Transform)visuals.FindProperty("stunEffect").objectReferenceValue).GetComponent<ParticleSystem>();
        var sparkMain = sparks.main;
        sparkMain.startLifetime = 0.6f;
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        sparkMain.startColor = new Color(1.8f, 0.7f, 3f, 1f);
        var sparkEmission = sparks.emission;
        sparkEmission.rateOverTime = 36f;
        var burn = ((GameObject)visuals.FindProperty("burnEffect").objectReferenceValue).GetComponent<ParticleSystem>();
        var burnMain = burn.main;
        burnMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        var burnEmission = burn.emission;
        burnEmission.rateOverTime = 50f;
        visuals.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Wulang/Combat/Update feedback and overhead UI")]
    public static void UpdateFeedback()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before updating combat.");
        var caster = Object.FindFirstObjectByType<SpellCaster>();
        var casterSettings = new SerializedObject(caster);
        var spells = casterSettings.FindProperty("spells");
        for (int i = 0; i < spells.arraySize; i++)
        {
            var reference = spells.GetArrayElementAtIndex(i).FindPropertyRelative("projectilePrefab");
            var source = (GameObject)reference.objectReferenceValue;
            if (source.GetComponent<SpellProjectile>() != null) continue;
            var projectile = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                var demo = projectile.GetComponent<ProjectileMoveScript>();
                var gameplay = new SerializedObject(projectile.AddComponent<SpellProjectile>());
                gameplay.FindProperty("element").enumValueIndex = spells.GetArrayElementAtIndex(i).FindPropertyRelative("element").enumValueIndex;
                if (demo != null)
                {
                    gameplay.FindProperty("muzzlePrefab").objectReferenceValue = demo.muzzlePrefab;
                    gameplay.FindProperty("hitPrefab").objectReferenceValue = demo.hitPrefab;
                    Object.DestroyImmediate(demo);
                }
                gameplay.ApplyModifiedPropertiesWithoutUndo();
                reference.objectReferenceValue = PrefabUtility.SaveAsPrefabAsset(projectile, Folder + "/Ice projectile.prefab");
            }
            finally { Object.DestroyImmediate(projectile); }
        }
        casterSettings.ApplyModifiedProperties();
        ConfigureArc(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Chain arc.mat"));
        var steam = PrefabUtility.LoadPrefabContents(Folder + "/Steam burst.prefab");
        try { ConfigureSteam(steam); PrefabUtility.SaveAsPrefabAsset(steam, Folder + "/Steam burst.prefab"); }
        finally { PrefabUtility.UnloadPrefabContents(steam); }
        foreach (var element in new[] { ElementType.Fire, ElementType.Ice, ElementType.Earth, ElementType.Lightning })
        {
            var path = Folder + "/" + element + " vampire.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureFeedback(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var seismic = PrefabUtility.LoadPrefabContents(Folder + "/Seismic burst.prefab");
        try
        {
            var particles = seismic.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1.2f;
            main.startLifetime = 1.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startColor = new Color(1.8f, 0.7f, 3f, 1f);
            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 60) });
            seismic.GetComponent<Light>().intensity = 3f;
            PrefabUtility.SaveAsPrefabAsset(seismic, Folder + "/Seismic burst.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(seismic); }
        var hud = Object.FindFirstObjectByType<ResponsiveHUD>();
        var oldTarget = hud.Content.Find("Target info");
        if (oldTarget != null) Undo.DestroyObjectImmediate(oldTarget.gameObject);
        var rootHUD = hud.Content.Find("CombatHUD") as RectTransform;
        rootHUD.sizeDelta = new Vector2(296, 74);
        for (int i = 0; i < rootHUD.childCount; i++)
        {
            var card = (RectTransform)rootHUD.GetChild(i);
            card.sizeDelta = new Vector2(144, 74);
            card.anchoredPosition = new Vector2(i * 152, 0);
            SetText(card.Find("Caption"), i == 0 ? "LMB" : "RMB", 16, new Vector2(12, -8), new Vector2(46, 22));
            SetText(card.Find("Count"), i == 0 ? "15/15" : "5/5", 24, new Vector2(58, -5), new Vector2(80, 30));
            SetText(card.Find("Recovery"), i == 0 ? "R" : "", 16, new Vector2(12, -38), new Vector2(110, 22));
            var progress = (RectTransform)card.Find("Progress");
            progress.anchoredPosition = new Vector2(12, -65);
            progress.sizeDelta = new Vector2(120, 3);
            ((RectTransform)progress.GetChild(0)).sizeDelta = progress.sizeDelta;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveScene(hud.gameObject.scene);
    }

    private static void SetText(Transform transform, string text, float fontSize, Vector2 position, Vector2 dimensions)
    {
        var rect = (RectTransform)transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var label = rect.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size, Vector2 position, Vector2 dimensions)
    {
        var text = Rect(parent, name, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.text = value;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Image Panel(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static UnityEngine.UI.Image Bar(Transform parent, string name, float width, Vector2 position, Color color)
    {
        var background = Rect(parent, name, new Vector2(width, 4f), position);
        Panel(background, new Color(0.15f, 0.2f, 0.27f));
        var fill = Rect(background, "Fill", new Vector2(width, 4f), Vector2.zero);
        var image = Panel(fill, color);
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = UnityEngine.UI.Image.Type.Filled;
        image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        return image;
    }

    private static void BuildHUD(SpellCaster caster, ResponsiveHUD hud)
    {
        var root = Rect(hud.Content, "CombatHUD", new Vector2(296, 74), new Vector2(-24, 24));
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 0);
        var component = root.gameObject.AddComponent<CombatHUD>();
        var settings = new SerializedObject(component);
        settings.FindProperty("caster").objectReferenceValue = caster;
        var colors = new[] { new Color(0.4f, 0.7f, 1f), new Color(0.75f, 0.5f, 1f) };
        for (int i = 0; i < 2; i++)
        {
            var card = Rect(root, i == 0 ? "Projectile charges" : "Area charges", new Vector2(144, 74), new Vector2(i * 152, 0));
            Panel(card, new Color(0.045f, 0.065f, 0.1f, 0.94f));
            Text(card, "Caption", i == 0 ? "LMB" : "RMB", 16, new Vector2(12, -8), new Vector2(46, 22)).color = colors[i];
            settings.FindProperty(i == 0 ? "projectileCount" : "areaCount").objectReferenceValue = Text(card, "Count", i == 0 ? "15/15" : "5/5", 24, new Vector2(58, -5), new Vector2(80, 30));
            settings.FindProperty(i == 0 ? "reloadLabel" : "recoveryLabel").objectReferenceValue = Text(card, "Recovery", i == 0 ? "R" : "", 16, new Vector2(12, -38), new Vector2(110, 22));
            settings.FindProperty(i == 0 ? "reloadFill" : "recoveryFill").objectReferenceValue = Bar(card, "Progress", 120, new Vector2(12, -65), colors[i]);
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
