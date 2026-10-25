using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.ParticleSystem;

// Layered, texture-based affinity auras and status effects for the vampire prefabs.
// Its particle helpers are shared with VolcanoBiomeBuilder.
// Re-running is safe: every effect it owns is destroyed and rebuilt.
public static class ElementalEffectsSetup
{
    private const string Folder = "Assets/Combat/Effects";
    private const string Hovl = "Assets/Hovl Studio/AOE Magic spells Vol.1/Textures/";

    [MenuItem("Wulang/Combat/Upgrade elemental effects")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Combat", "Effects");
        foreach (var element in new[] { ElementType.Fire, ElementType.Ice, ElementType.Earth, ElementType.Lightning })
        {
            string path = "Assets/Combat/" + element + " vampire.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visuals = new SerializedObject(root.GetComponent<CombatTargetVisuals>());
                var skin = (SkinnedMeshRenderer)visuals.FindProperty("skin").objectReferenceValue;
                Replace(visuals, "affinityEffect", Aura(element, root.transform, skin));
                Replace(visuals, "frozenEffect", Frozen(root.transform, skin));
                Replace(visuals, "groundedEffect", Grounded(root.transform));
                Replace(visuals, "chargedEffect", Charged(root.transform, skin));
                Replace(visuals, "stunEffect", Stars(root.transform));
                visuals.FindProperty("arcMaterial").objectReferenceValue = Mat("Spark", "Spark5", true, 3f);
                var burn = (GameObject)visuals.FindProperty("burnEffect").objectReferenceValue;
                var oldEmbers = burn.transform.Find("Burn embers");
                if (oldEmbers != null) Object.DestroyImmediate(oldEmbers.gameObject);
                Embers(burn.transform, "Burn embers", skin, 30f, 1f);
                visuals.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void Replace(SerializedObject visuals, string field, GameObject effect)
    {
        var property = visuals.FindProperty(field);
        var old = property.objectReferenceValue;
        if (old != null) Object.DestroyImmediate(old is Component component ? component.gameObject : old);
        property.objectReferenceValue = property.type.Contains("Transform") ? effect.transform : effect;
        effect.SetActive(field == "affinityEffect");
    }

    private static GameObject Aura(ElementType element, Transform root, SkinnedMeshRenderer skin)
    {
        var aura = Group(root, element + " affinity aura");
        switch (element)
        {
            case ElementType.Fire:
                Embers(aura, "Embers", skin, 28f, 0.6f);
                var flames = Emitter(aura, "Flame licks", AssetDatabase.LoadAssetAtPath<Material>("Assets/GabrielAguiarProductions/Unique_Projectiles_Volume_1/Materials/Flames01_Add_4x4.mat"),
                    10f, new Vector2(0.45f, 0.8f), new Vector2(0.28f, 0.42f), new Color(1f, 0.5f, 0.15f, 0.55f), new Color(1f, 0.3f, 0.06f, 0.4f));
                OnSkin(flames, skin);
                var flameMain = flames.main;
                flameMain.gravityModifier = -0.25f;
                Flipbook(flames, 4, 4, false);
                break;
            case ElementType.Ice:
                Glints(aura, "Frost glints", skin, 12f);
                var snow = Emitter(aura, "Snowfall", Mat("Snowflake", "Snow4", true, 1.6f), 18f, new Vector2(2.5f, 3.5f), new Vector2(0.035f, 0.07f), new Color(0.85f, 0.95f, 1f, 0.9f), new Color(0.7f, 0.88f, 1f, 0.8f));
                Ring(snow, 0.7f, 2.3f, 1f);
                Drift(snow, -0.6f, 0.25f);
                Flipbook(snow, 2, 2, true);
                Mist(aura, "Cold mist", 5f, 0.35f, new Color(0.35f, 0.6f, 1f, 0.5f));
                break;
            case ElementType.Earth:
                var leaves = Emitter(aura, "Orbiting leaves", Mat("Leaf", "Leaf2", false, 1f), 9f, new Vector2(2.5f, 3.5f), new Vector2(0.12f, 0.2f), new Color(0.42f, 0.78f, 0.22f), new Color(0.8f, 0.72f, 0.28f));
                Ring(leaves, 0.6f, 0.3f, 1f);
                var orbit = leaves.velocityOverLifetime;
                orbit.enabled = true;
                orbit.space = ParticleSystemSimulationSpace.Local;
                orbit.orbitalY = 1.6f;
                orbit.y = 0.3f;
                Tumble(leaves);
                Noise(leaves, 0.2f, 0.8f);
                var spores = Emitter(aura, "Spores", Mat("Spore", "Point12", true, 2.5f), 14f, new Vector2(1.8f, 2.8f), new Vector2(0.025f, 0.05f), new Color(0.45f, 1f, 0.25f), new Color(0.8f, 0.95f, 0.3f));
                OnSkin(spores, skin);
                var sporeMain = spores.main;
                sporeMain.gravityModifier = -0.04f;
                Noise(spores, 0.4f, 1f);
                var pebbles = Emitter(aura, "Floating pebbles", Mat("Pebble", "Debris2", false, 1f), 2.5f, new Vector2(1.8f, 2.4f), new Vector2(0.05f, 0.09f), new Color(0.5f, 0.42f, 0.3f), new Color(0.38f, 0.33f, 0.27f));
                Ring(pebbles, 0.45f, 0.05f, 1f);
                Drift(pebbles, 0.22f, 0f);
                Flipbook(pebbles, 2, 2, true);
                Spin(pebbles, 1f);
                break;
            case ElementType.Lightning:
                Crackle(aura, "Crackle", skin, 0.09f, 0.6f);
                Sparks(aura, "Static sparks", skin, 0.35f);
                break;
        }
        AffinityLight(aura, ElementVisuals.ColorFor(element));
        return aura.gameObject;
    }

    // A soft element-coloured light grounds the aura on the body and the floor around it.
    private static void AffinityLight(Transform aura, Color color)
    {
        var light = new GameObject("Affinity light").AddComponent<Light>();
        light.transform.SetParent(aura, false);
        light.transform.localPosition = Vector3.up * 1.2f;
        light.type = LightType.Point;
        light.color = color;
        light.range = 3f;
        light.intensity = 1.5f;
        light.shadows = LightShadows.None;
    }

    private static GameObject Frozen(Transform root, SkinnedMeshRenderer skin)
    {
        var frozen = Group(root, "Frozen effect");
        Seal(frozen, "Frost seal", Mat("Frost ring", "Circle132", true, 3f), 2.3f, new Color(0.45f, 0.8f, 1f, 0.85f), 12f);
        var crystals = Lasting(frozen, "Ice crystals", Mat("Ice shard", "Romb3", true, 1.1f), 12, new Color(0.35f, 0.75f, 1f, 0.65f), new Color(0.55f, 0.88f, 1f, 0.8f));
        var main = crystals.main;
        main.startSize3D = true;
        main.startSizeX = new MinMaxCurve(0.18f, 0.3f);
        main.startSizeY = new MinMaxCurve(0.7f, 1.2f);
        main.startRotation = new MinMaxCurve(-0.35f, 0.35f);
        Ring(crystals, 0.5f, 0.3f, 0f);
        Pop(crystals);
        crystals.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        Mist(frozen, "Frost mist", 6f, 0.3f, new Color(0.5f, 0.8f, 1f, 0.5f));
        Glints(frozen, "Frost glints", skin, 14f);
        return frozen.gameObject;
    }

    private static GameObject Grounded(Transform root)
    {
        var grounded = Group(root, "Grounded effect");
        Seal(grounded, "Earth rune seal", Mat("Rune circle", "MagicCircle31", true, 2f), 1.9f, new Color(0.75f, 1f, 0.35f, 0.75f), 18f);
        var pebbles = Emitter(grounded, "Tremor pebbles", Mat("Pebble", "Debris2", false, 1f), 10f, new Vector2(0.5f, 0.8f), new Vector2(0.04f, 0.08f), new Color(0.5f, 0.42f, 0.3f), new Color(0.38f, 0.33f, 0.27f));
        var shape = pebbles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.55f;
        shape.radiusThickness = 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var main = pebbles.main;
        main.startSpeed = new MinMaxCurve(1.2f, 1.8f);
        main.gravityModifier = 1f;
        Flipbook(pebbles, 2, 2, true);
        Spin(pebbles, 3f);
        var motes = Emitter(grounded, "Earth motes", Mat("Spore", "Point12", true, 2.5f), 10f, new Vector2(1f, 1.5f), new Vector2(0.02f, 0.04f), new Color(0.75f, 1f, 0.3f), new Color(1f, 0.85f, 0.35f));
        Ring(motes, 0.6f, 0.05f, 1f);
        Drift(motes, 0.4f, 0.15f);
        return grounded.gameObject;
    }

    private static GameObject Charged(Transform root, SkinnedMeshRenderer skin)
    {
        var charged = Group(root, "Charged effect");
        Seal(charged, "Storm ring", Mat("Storm ring", "Circle82", true, 2.5f), 1.5f, new Color(0.55f, 0.3f, 1f, 0.8f), -70f);
        Crackle(charged, "Body crackle", skin, 0.06f, 0.8f);
        Sparks(charged, "Static sparks", skin, 0.25f);
        return charged.gameObject;
    }

    private static GameObject Stars(Transform root)
    {
        var stars = Lasting(Group(root, "Stun effect"), "Stun stars", Mat("Star", "Flare3", true, 3f), 4, new Color(1f, 0.85f, 0.45f), new Color(1f, 0.95f, 0.7f));
        var main = stars.main;
        main.startSize = new MinMaxCurve(0.2f, 0.26f);
        var shape = stars.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.radius = 0.3f;
        shape.radiusThickness = 0f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
        Spin(stars, 4f);
        // Local particles orbit with the spinning parent; world-space trails draw the orbit.
        var trails = stars.trails;
        trails.enabled = true;
        trails.worldSpace = true;
        trails.lifetime = 0.25f / 60f;
        trails.minVertexDistance = 0.01f;
        trails.dieWithParticles = true;
        trails.widthOverTrail = new MinMaxCurve(0.25f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        trails.colorOverTrail = new Gradient { alphaKeys = new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) } };
        var renderer = stars.GetComponent<ParticleSystemRenderer>();
        renderer.trailMaterial = Mat("Spark", "Spark5", true, 3f);
        return stars.transform.parent.gameObject;
    }

    private static void Embers(Transform parent, string name, SkinnedMeshRenderer skin, float rate, float lift)
    {
        var embers = Emitter(parent, name, Mat("Ember", "Point5", true, 3f), rate, new Vector2(0.8f, 1.6f), new Vector2(0.04f, 0.09f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.3f, 0.05f));
        OnSkin(embers, skin);
        var main = embers.main;
        main.startSpeed = new MinMaxCurve(0.1f * lift, 0.5f * lift);
        main.gravityModifier = -0.12f * lift;
        Noise(embers, 0.6f, 1.5f);
        Shrink(embers);
    }

    private static void Glints(Transform parent, string name, SkinnedMeshRenderer skin, float rate)
    {
        var glints = Emitter(parent, name, Mat("Glint", "Flare4", true, 4f), rate, new Vector2(0.5f, 0.9f), new Vector2(0.3f, 0.45f), new Color(0.75f, 0.95f, 1f), new Color(0.5f, 0.85f, 1f));
        OnSkin(glints, skin);
        var main = glints.main;
        main.startRotation = new MinMaxCurve(-0.3f, 0.3f);
        var twinkle = glints.sizeOverLifetime;
        twinkle.enabled = true;
        twinkle.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
    }

    private static void Mist(Transform parent, string name, float rate, float radius, Color color)
    {
        var mist = Emitter(parent, name, Mat("Frost mist", "Smoke3", true, 1.4f), rate, new Vector2(2.5f, 3.5f), new Vector2(0.7f, 1.1f), color, color * new Color(1f, 1f, 1f, 0.75f));
        Ring(mist, radius, 0.12f, 1f);
        var main = mist.main;
        main.startSpeed = new MinMaxCurve(0.1f, 0.25f);
        Spin(mist, 0.2f);
        var grow = mist.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new MinMaxCurve(1.7f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));
    }

    private static void Crackle(Transform parent, string name, SkinnedMeshRenderer skin, float interval, float probability)
    {
        var crackle = Emitter(parent, name, Mat("Crackle", "HandPaintedLightning2", true, 4f), 0f, new Vector2(0.07f, 0.14f), new Vector2(0.3f, 0.55f), new Color(0.9f, 0.75f, 1f), new Color(0.6f, 0.4f, 1f));
        OnSkin(crackle, skin);
        crackle.emission.SetBursts(new[] { new Burst(0f, 1, 3, 0, interval) { probability = probability } });
        var sheet = crackle.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = sheet.numTilesY = 3;
        // A random fixed frame per bolt; cycling frames reads as flicker noise.
        sheet.frameOverTime = new MinMaxCurve(0f, 0.999f);
    }

    private static void Sparks(Transform parent, string name, SkinnedMeshRenderer skin, float interval)
    {
        var sparks = Emitter(parent, name, Mat("Spark", "Spark5", true, 3f), 0f, new Vector2(0.15f, 0.3f), new Vector2(0.03f, 0.05f), new Color(0.85f, 0.75f, 1f), new Color(0.65f, 0.5f, 1f));
        OnSkin(sparks, skin);
        sparks.emission.SetBursts(new[] { new Burst(0f, 3, 6, 0, interval) { probability = 0.7f } });
        var main = sparks.main;
        main.startSpeed = new MinMaxCurve(1.5f, 3.5f);
        main.gravityModifier = 0.5f;
        main.startRotation = 0f;
        var renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0.06f;
        Shrink(sparks);
    }

    // One long-lived ground decal that fades in and spins for as long as the effect is shown.
    private static void Seal(Transform parent, string name, Material material, float size, Color color, float degreesPerSecond)
    {
        var seal = Lasting(parent, name, material, 1, color, color);
        seal.transform.localPosition = Vector3.up * 0.05f;
        var main = seal.main;
        main.startSize = size;
        main.startRotation = 0f;
        var spin = seal.rotationOverLifetime;
        spin.enabled = true;
        spin.z = degreesPerSecond * Mathf.Deg2Rad;
        Pop(seal);
        seal.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    private static ParticleSystem Lasting(Transform parent, string name, Material material, int count, Color a, Color b)
    {
        var lasting = Emitter(parent, name, material, 0f, new Vector2(60f, 60f), Vector2.one, a, b);
        var main = lasting.main;
        main.loop = false;
        main.duration = 1f;
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        lasting.emission.SetBursts(new[] { new Burst(0f, (short)count) });
        var fade = lasting.colorOverLifetime;
        fade.color = Fade(0.006f, 1f);
        return lasting;
    }

    internal static ParticleSystem Emitter(Transform parent, string name, Material material, float rate, Vector2 lifetime, Vector2 size, Color a, Color b)
    {
        var particles = new GameObject(name).AddComponent<ParticleSystem>();
        particles.transform.SetParent(parent, false);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = new MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = 0f;
        main.startSize = new MinMaxCurve(size.x, size.y);
        main.startColor = new MinMaxGradient(a, b);
        main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 200;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        shape.enabled = false;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        fade.color = Fade(0.15f, 0.7f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 3f;
        return particles;
    }

    internal static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    internal static void OnSkin(ParticleSystem particles, SkinnedMeshRenderer skin)
    {
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = skin;
    }

    internal static void Ring(ParticleSystem particles, float radius, float height, float thickness)
    {
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.position = Vector3.up * height;
        shape.radius = radius;
        shape.radiusThickness = thickness;
    }

    internal static void Drift(ParticleSystem particles, float rise, float noise)
    {
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = velocity.z = 0f;
        velocity.y = rise;
        if (noise > 0f) Noise(particles, noise, 0.6f);
    }

    internal static void Noise(ParticleSystem particles, float strength, float frequency)
    {
        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    internal static void Spin(ParticleSystem particles, float radiansPerSecond)
    {
        var spin = particles.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new MinMaxCurve(-radiansPerSecond, radiansPerSecond);
    }

    private static void Tumble(ParticleSystem particles)
    {
        var main = particles.main;
        main.startRotation3D = true;
        main.startRotationX = main.startRotationY = main.startRotationZ = new MinMaxCurve(0f, Mathf.PI * 2f);
        var spin = particles.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = spin.y = spin.z = new MinMaxCurve(-2f, 2f);
    }

    internal static void Shrink(ParticleSystem particles)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    // Grows in over the first ~0.5 s of a 60 s lifetime, overshooting slightly like ice snapping into place.
    private static void Pop(ParticleSystem particles)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.004f, 1.15f), new Keyframe(0.008f, 1f), new Keyframe(1f, 1f)));
    }

    internal static void Flipbook(ParticleSystem particles, int x, int y, bool randomFrame)
    {
        var sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = x;
        sheet.numTilesY = y;
        sheet.frameOverTime = randomFrame ? new MinMaxCurve(0f, 0.999f) : new MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
    }

    internal static Gradient Fade(float fadeIn, float fadeOut)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            fadeOut >= 1f
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f) }
                : new[] { new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f), new GradientAlphaKey(1f, Mathf.Max(fadeIn, 0.001f)), new GradientAlphaKey(1f, Mathf.Max(fadeOut, 0.002f)), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // Particle and line vertex colours clamp to 1, so HDR brightness for bloom lives in the material tint.
    internal static Material Mat(string name, string texture, bool additive, float intensity, string folder = Folder)
    {
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Hovl + texture + ".png"));
        material.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }
}
