using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.ParticleSystem;
using FX = ElementalEffectsSetup;
using Object = UnityEngine.Object;

// Bakes the volcano, its lava-tube boss caldera and the surrounding lava fields as ordinary editable scene content.
// Re-running replaces everything under "Redwood Lava Cavern" except the objects the volcano preview cinematic references.
// Root frame: local -Z faces west (the armory), +Z east, +X south. Heights are root-local metres.
public static partial class VolcanoBiomeBuilder
{
    const string Folder = "Assets/Environment/Volcano";
    const string LavaSet = "Assets/Blink/Art/Textures/StylizedLavaTextures/";
    const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    const float Rim = 100f, MoltenLevel = 9f, ArenaLevel = 9.9f, PortalFloor = 6.8f, ChannelDrop = 0.9f;
    const float TunnelInner = -23f, TunnelOuter = -74f, WalkwayX = -2.4f;
    static readonly string[] Kept = { "Volcano preview landmark", "Summit smoke" };
    // World-space lava: rivers flow from their first point; both drain into the lake south-west of the cone.
    static readonly Vector2[] TubeRiver = { new(721, 637.7f), new(711, 636.5f), new(701, 631), new(693, 621), new(689, 608), new(690, 594), new(695, 582), new(699, 572) };
    static readonly Vector2[] FlankRiver = { new(750.5f, 597.5f), new(743, 590), new(735, 584), new(726, 578), new(717, 572), new(709, 566) };
    static readonly Vector2 LakeCenter = new(699, 561), LakeRadii = new(19, 12.5f);
    const float LakeAngle = 25f, LakeLevel = 3.6f;

    static Transform root;
    static Terrain terrain;
    static int ground;
    static Material slope, basalt, arenaFloor, riverLava, poolLava, fallLava;

    [MenuItem("Wulang/Volcano/Rebuild volcano biome")]
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode before rebuilding the volcano.");
        root = GameObject.Find("Redwood Lava Cavern")?.transform;
        terrain = Terrain.activeTerrain;
        if (root == null || terrain == null) throw new InvalidOperationException("The volcano root and world terrain are required.");
        ground = LayerMask.NameToLayer("Ground");
        foreach (var folder in new[] { Folder, Folder + "/Meshes" })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        Undo.SetCurrentGroupName("Rebuild volcano biome");
        foreach (var child in root.Cast<Transform>().ToArray())
            if (!Kept.Contains(child.name)) Undo.DestroyObjectImmediate(child.gameObject);

        Materials();
        Sculpt();
        Shell();
        Tunnel();
        Caldera();
        LavaFields();
        Landforms();
        Highlands();
        Dressing();
        Effects();
        Atmosphere();
        RetargetPreview();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        return "Volcano biome rebuilt: " + root.GetComponentsInChildren<MeshFilter>().Length + " meshes, "
            + root.GetComponentsInChildren<ParticleSystem>().Length + " particle systems, " + root.GetComponentsInChildren<Light>().Length + " lights.";
    }

    // ---------- Materials ----------

    static void Materials()
    {
        // Dark ash and basalt leave the glow to the lava itself; only sparse cracks emit.
        slope = Rock("Volcanic slope", "Dry_Lava/Dry_Lava", "Rock_Ground/Rock_Ground", Color.black, Color.black, .045f, .08f, .3f, new Color(.62f, .58f, .56f), new Color(.7f, .66f, .62f));
        // Plain basalt faces with Cliff_3's crack mask laid over them at low strength: veins, not wallpaper.
        basalt = Rock("Caldera basalt", "Cliff_5/Cliff_5", "Rock_Ground/Rock_Ground", new Color(.3f, .07f, .012f), Color.black, .045f, .09f, .25f, new Color(.75f, .74f, .78f), new Color(.75f, .7f, .68f), "Cliff_3/Cliff_3");
        arenaFloor = Rock("Arena floor", "Cliff_5/Cliff_5", "Lava_Ground_02/Lava_Ground_02", new Color(.3f, .07f, .012f), new Color(.45f, .1f, .02f), .05f, .075f, .5f, new Color(.75f, .74f, .78f), new Color(.85f, .8f, .78f), "Cliff_3/Cliff_3");
        riverLava = Lava("River lava", new Vector2(0, .09f), .28f, .8f, .03f, 1f, new Vector2(1, 1), .22f);
        poolLava = Lava("Pool lava", new Vector2(.012f, .018f), .33f, .9f, .03f, 1f, new Vector2(1, 1), .18f);
        fallLava = Lava("Falling lava", new Vector2(0, .55f), .05f, .25f, .06f, 1.25f, new Vector2(1, .3f), .3f);
    }

    static Material Rock(string name, string side, string top, Color sideGlow, Color topGlow, float tiling, float topTiling, float topBlend, Color sideTint, Color topTint, string glow = null)
    {
        var material = Asset(name, "Game/VolcanicRock");
        material.SetTexture("_BaseMap", Tex(side + "_BaseColor"));
        material.SetTexture("_BumpMap", Tex(side + "_Normal"));
        material.SetTexture("_EmissionMap", Tex((glow ?? side) + "_Emissive"));
        material.SetTexture("_TopMap", Tex(top + "_BaseColor"));
        material.SetTexture("_TopBumpMap", Tex(top + "_Normal"));
        material.SetTexture("_TopEmissionMap", Tex(top + "_Emissive"));
        material.SetColor("_EmissionColor", sideGlow);
        material.SetColor("_TopEmissionColor", topGlow);
        material.SetColor("_BaseColor", sideTint);
        material.SetColor("_TopColor", topTint);
        material.SetFloat("_Tiling", tiling);
        material.SetFloat("_TopTiling", topTiling);
        material.SetFloat("_TopBlend", topBlend);
        // Double-sided: the tube protrudes past the flank, so its walls are also seen from outside.
        material.SetFloat("_Cull", (float)CullMode.Off);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Lava(string name, Vector2 flow, float crust, float bankCrust, float wave, float heat, Vector2 tiling, float plateScale)
    {
        var material = Asset(name, "Game/Lava");
        material.SetTexture("_LavaMap", Tex("Lava/Lava_BaseColor"));
        material.SetTexture("_CrustMap", Tex("Lava_Ground_01/Lava_Ground_1_BaseColor"));
        material.SetTexture("_NoiseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hovl Studio/AOE Magic spells Vol.1/Textures/Noise34.png"));
        material.SetVector("_Tiling", tiling);
        material.SetFloat("_CrustScale", plateScale);
        material.SetColor("_HotColor", new Color(2.2f, 1.4f, 1f) * heat);
        material.SetColor("_CoolColor", new Color(1.1f, .45f, .3f) * heat);
        material.SetVector("_Flow", flow);
        material.SetFloat("_CrustCoverage", crust);
        material.SetFloat("_EdgeCrust", bankCrust);
        material.SetFloat("_Wave", wave);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Asset(string name, string shader)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find(shader);
        return material;
    }

    static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(LavaSet + path + ".png");

    // ---------- Volcano shell ----------

    static float Perlin(float x, float y) => Mathf.PerlinNoise(x, y);
    static float Gauss(float angle, float centreDegrees, float widthDegrees)
    {
        float d = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, centreDegrees) / widthDegrees;
        return Mathf.Exp(-d * d);
    }
    static float RimHeight(float a) => Rim + (Perlin(Mathf.Cos(a) * 2 + 5, Mathf.Sin(a) * 2 + 9) - .5f) * 8f - Gauss(a, 315, 8) * 6f;
    static float InnerRadius(float y, float a)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(5f, Rim, y));
        // Vertical basalt columns ribbing the caldera wall.
        return Mathf.Lerp(23.2f, 30f, Mathf.Sqrt(t)) + Mathf.Abs(Mathf.Sin(a * 22f)) * .9f + (Perlin(a * 3f + 11, y * .06f) - .5f) * 2.4f;
    }
    static float OuterRadius(float y, float a)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(-6f, Rim, y));
        // Concave stratovolcano profile with radial erosion gullies and the cascade's flank channel.
        float r = 32f + 50f * Mathf.Pow(1f - t, 1.5f)
            + Mathf.Sin(a * 9f + Perlin(a, y * .02f) * 4f) * (1.2f + 1.6f * t)
            + (Perlin(a * 2.2f + 3, y * .035f) - .5f) * 7f
            - Gauss(a, 315, 6) * 1.8f;
        return Mathf.Max(r, InnerRadius(y, a) + 3f);
    }
    static Vector3 Polar(float a, float r, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
    static bool PortalHole(float a, float y, float halfDegrees, float height) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270)) < halfDegrees && y < height;

    static void Shell()
    {
        var shell = Group(root, "Volcano shell");
        const int sectors = 160, rows = 46;
        float A(int j) => j * Mathf.PI * 2 / sectors;
        var outer = new Builder();
        outer.Grid(rows, sectors, (i, j) => { float y = Mathf.Lerp(-6f, RimHeight(A(j)), i / (float)rows); return Polar(A(j), OuterRadius(y, A(j)), y); },
            (i, j) => PortalHole(A(j) + Mathf.PI / sectors, Mathf.Lerp(-6f, Rim, (i + .5f) / rows), 7.5f, 17f), wrap: true);
        Solid(shell, "Outer flank", outer.Build("Volcano outer flank", p => new Vector3(p.x, .15f, p.z)), slope);

        var inner = new Builder();
        inner.Grid(rows, sectors, (i, j) => { float y = Mathf.Lerp(5f, RimHeight(A(j)), i / (float)rows); return Polar(A(j), InnerRadius(y, A(j)), y); },
            (i, j) => PortalHole(A(j) + Mathf.PI / sectors, Mathf.Lerp(5f, Rim, (i + .5f) / rows), 17f, 20.5f), wrap: true);
        Solid(shell, "Caldera wall", inner.Build("Volcano caldera wall", p => new Vector3(-p.x, 0, -p.z)), basalt);

        var crest = new Builder();
        crest.Grid(2, sectors, (i, j) =>
        {
            float a = A(j), y = RimHeight(a), ro = OuterRadius(y, a), ri = InnerRadius(y, a);
            return i == 0 ? Polar(a, ro, y) : i == 2 ? Polar(a, ri, y) : Polar(a, (ro + ri) * .5f, y + 1.2f + Perlin(a * 6, 2) * 1.8f);
        }, wrap: true);
        Solid(shell, "Crater rim", crest.Build("Volcano crater rim", p => Vector3.up), basalt);

        // The cascade spills through the rim notch and runs down the flank channel to the base.
        var cascade = new Builder();
        const int length = 90, across = 8;
        float centre = 315 * Mathf.Deg2Rad, travelled = 0;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i <= length; i++)
        {
            float y = Mathf.Lerp(RimHeight(centre) - .4f, 7.5f, i / (float)length);
            var mid = Polar(centre, OuterRadius(y, centre) + .3f, y);
            if (i > 0) travelled += Vector3.Distance(previous, mid);
            previous = mid;
            for (int k = 0; k <= across; k++)
            {
                float side = k / (float)across * 2 - 1, half = (2f + .5f * Mathf.Sin(i * .3f)) / OuterRadius(y, centre);
                float a = centre + side * half;
                cascade.Add(Polar(a, OuterRadius(y, a) + .3f + (1 - side * side) * .2f, y), new Vector2(side * .6f, travelled / 4f), Mathf.Abs(side));
            }
        }
        cascade.Quads(length, across);
        Surface(shell, "Flank lava cascade", cascade.Build("Flank lava cascade", p => new Vector3(p.x, 0, p.z)), fallLava, false);
    }

    // Root-local walking route: lava fields, through the tube, over the causeway, to the arena centre.
    internal static Vector3[] Route()
    {
        var points = new List<Vector3>();
        for (float z = TunnelOuter - 14; z <= TunnelInner; z += 1) points.Add(new Vector3(TubeCurve(z) + WalkwayX, TubeFloor(z), z));
        for (float z = TunnelInner + 1; z <= -13; z += 1) points.Add(new Vector3(WalkwayX, ArenaLevel, z));
        for (int i = 1; i <= 10; i++) points.Add(Vector3.Lerp(new Vector3(WalkwayX, ArenaLevel, -13), new Vector3(0, ArenaLevel, 0), i / 10f));
        return points.ToArray();
    }

    // ---------- Lava tube ----------

    static float TubeT(float z) => Mathf.InverseLerp(TunnelInner, TunnelOuter, z);
    static float TubeFloor(float z) => Mathf.Lerp(ArenaLevel, PortalFloor, TubeT(z));
    static float TubeCurve(float z) => 1.5f * Mathf.Sin(Mathf.PI * TubeT(z));

    static void Tunnel()
    {
        var tube = Group(root, "Lava tube");
        Vector2[] arch = { new(-6.2f, -.3f), new(-6.8f, 2.6f), new(-6.2f, 5.3f), new(-4.6f, 7.7f), new(-2.2f, 9f), new(.5f, 9.4f), new(3.2f, 8.9f), new(5.4f, 7.3f), new(6.6f, 4.7f), new(6.9f, 1.4f), new(6.8f, -1.6f) };
        const int segments = 70;
        float Z(int i) => Mathf.Lerp(TunnelInner + 1.5f, TunnelOuter - 1f, i / (float)segments);
        var walls = new Builder();
        walls.Grid(segments, arch.Length - 1, (i, k) =>
        {
            float z = Z(i), noise = (Perlin(z * .17f, k * .9f) - .5f) * 1.3f;
            var offset = arch[k] * (1 + noise / 8f);
            return new Vector3(TubeCurve(z) + offset.x, TubeFloor(z) + offset.y, z);
        });
        Solid(tube, "Tube walls", walls.Build("Lava tube walls", p => new Vector3(TubeCurve(p.z), TubeFloor(p.z) + 4.5f, p.z) - p), basalt);

        var path = new Builder();
        float[] walk = { -6.3f, -4f, -1.5f, 1f, 1.5f, 1.85f, 2.1f };
        float[] lift = { 0, .05f, 0, .05f, .12f, .12f, -ChannelDrop - .4f };
        path.Grid(segments, walk.Length - 1, (i, k) => { float z = Z(i); return new Vector3(TubeCurve(z) + walk[k], TubeFloor(z) + lift[k], z); });
        Solid(tube, "Tube walkway", path.Build("Lava tube walkway", p => p.x - TubeCurve(p.z) > 1.7f ? Vector3.right : Vector3.up), arenaFloor);

        var channel = new Builder();
        for (int i = 0; i <= segments; i++)
            for (int k = 0; k <= 6; k++)
            {
                float z = Z(i), side = k / 6f * 2 - 1;
                channel.Add(new Vector3(TubeCurve(z) + 4.4f + side * 2.6f, TubeFloor(z) - ChannelDrop, z), new Vector2(side * .65f, (TunnelInner - z) / 4f), Mathf.Abs(side));
            }
        channel.Quads(segments, 6);
        Surface(tube, "Tube lava channel", channel.Build("Lava tube channel", p => Vector3.up), riverLava, true);
    }

    // ---------- Boss caldera ----------

    static void Caldera()
    {
        var arena = Group(root, "Boss caldera");
        float IslandRadius(float a) => 15f + (Perlin(Mathf.Cos(a) * 1.5f + 20, Mathf.Sin(a) * 1.5f + 4) - .5f) * 2.2f;
        const int sectors = 120;
        float A(int j) => j * Mathf.PI * 2 / sectors;

        var top = new Builder();
        top.Grid(12, sectors, (i, j) =>
        {
            float f = i / 12f, a = A(j);
            var p = Polar(a, IslandRadius(a) * f, ArenaLevel);
            p.y += (Perlin(p.x * .15f + 40, p.z * .15f) - .5f) * .25f * (1 - f * f);
            return p;
        }, wrap: true);
        Solid(arena, "Arena floor", top.Build("Boss arena floor", p => Vector3.up), arenaFloor);
        var skirt = new Builder();
        skirt.Grid(4, sectors, (i, j) =>
        {
            float f = i / 4f, a = A(j);
            return Polar(a, IslandRadius(a) + f * 2.4f + Mathf.Sin(f * Mathf.PI) * (Perlin(a * 4, 9) - .3f) * 1.2f, Mathf.Lerp(ArenaLevel, MoltenLevel - 2f, f));
        }, wrap: true);
        Solid(arena, "Arena cliffs", skirt.Build("Boss arena cliffs", p => new Vector3(p.x, 0, p.z)), basalt);

        var moat = new Builder();
        for (int i = 0; i <= 7; i++)
            for (int j = 0; j < sectors; j++)
            {
                // The ring tucks 1 m under the caldera wall at every angle instead of running into the rock behind it.
                float a = A(j), wall = InnerRadius(MoltenLevel, a), r = Mathf.Lerp(13.5f, wall + 1f, i / 7f);
                var p = Polar(a, r, MoltenLevel);
                moat.Add(p, new Vector2(p.x, p.z) / 5f, Mathf.Clamp01(Mathf.Max(1 - (r - IslandRadius(a)) / 3f, (r - wall + 3f) / 3f)));
            }
        moat.Quads(7, sectors, wrap: true);
        Surface(arena, "Molten moat", moat.Build("Caldera molten moat", p => Vector3.up), poolLava, true);

        // Bridge from the lava tube to the island, low enough over the moat to read as a natural causeway.
        var bridge = new Builder();
        Vector2[] profile = { new(-4.6f, -3f), new(-4.2f, -1f), new(-3.7f, 0), new(0, .2f), new(3.7f, 0), new(4.2f, -1f), new(4.6f, -3f) };
        bridge.Grid(12, profile.Length - 1, (i, k) =>
        {
            float z = Mathf.Lerp(TunnelInner + 2f, -12.5f, i / 12f), wobble = (Perlin(z * .4f, k) - .5f) * .6f * (k == 3 ? 0 : 1);
            return new Vector3(WalkwayX + profile[k].x + wobble, ArenaLevel + profile[k].y, z);
        });
        Solid(arena, "Causeway", bridge.Build("Caldera causeway", p => new Vector3(p.x - WalkwayX, Mathf.Abs(p.x - WalkwayX) < 3.8f ? 4 : 0, 0)), arenaFloor);

        // The lavafall pours from a vent straight across from the tube, framing the arena from the entrance.
        Lavafall(arena, "Grand lavafall", 90, 18, 48f, 30);
        Lavafall(arena, "North lava trickle", 52, 2.6f, 34f, 6);
        Lavafall(arena, "South lava trickle", 128, 2.6f, 32f, 6);

        // Broken basalt columns give cover without blocking the causeway entry.
        foreach (float degrees in new[] { 40f, 140f, 220f, 320f })
        {
            float a = degrees * Mathf.Deg2Rad, height = 6.5f + Perlin(degrees, 3) * 4f;
            Place(arena, "Cover pillar", "Cliff_infernal", Polar(a, 9.5f, ArenaLevel + height * .5f - .4f), new Vector3(3.2f, height, 3.4f), degrees * 1.7f);
            Place(arena, "Pillar rubble", "Rock_infernal", Polar(a + .14f, 11.3f, ArenaLevel + .2f), new Vector3(2.2f, 1f, 1.8f), degrees);
        }
        for (int i = 0; i < 14; i++)
        {
            float a = (i * 25.7f + 8) * Mathf.Deg2Rad;
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270)) < 25 || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90)) < 20) continue;
            Place(arena, "Wall foot boulder", i % 2 == 0 ? "Rock_infernal" : "Cliff_infernal", Polar(a, 23f, MoltenLevel + .6f), new Vector3(5, 3.5f + i % 3, 4.5f), i * 47);
        }
        MouthRocks(arena, "Tube mouth rock", ArenaLevel, -22.5f, -1);
        for (int i = 0; i < 5; i++)
            Place(arena, "Lavafall vent rock", "Cliff_infernal", Polar((66 + i * 12) * Mathf.Deg2Rad, 25f, 50.5f + (i % 2) * 2.5f), new Vector3(7, 5, 5), i * 61);

        var spawn = new GameObject("Boss spawn point").transform;
        spawn.SetParent(arena, false);
        spawn.localPosition = new Vector3(0, ArenaLevel, 7f);
        spawn.localRotation = Quaternion.LookRotation(Vector3.back);
        var bounds = new GameObject("Boss arena bounds");
        bounds.transform.SetParent(arena, false);
        bounds.transform.localPosition = new Vector3(0, ArenaLevel + 6, 0);
        var trigger = bounds.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true; trigger.radius = 16f; trigger.height = 14f;
    }

    static void Lavafall(Transform parent, string name, float centreDegrees, float halfDegrees, float top, int columns)
    {
        var fall = new Builder();
        const int rows = 60;
        float centre = centreDegrees * Mathf.Deg2Rad;
        for (int i = 0; i <= rows; i++)
            for (int k = 0; k <= columns; k++)
            {
                float s = i / (float)rows, y = Mathf.Lerp(top, MoltenLevel - .4f, s);
                float side = k / (float)columns * 2 - 1, half = (halfDegrees + 1.5f * Mathf.Sin(s * 7 + centreDegrees)) * Mathf.Deg2Rad;
                float a = centre + side * half;
                // Lava leaves the vent with momentum, then hugs the wall as it falls.
                float r = InnerRadius(y, a) - .9f - 2.6f * Mathf.Exp(-(top - y) * .35f);
                fall.Add(Polar(a, r, y), new Vector2(side * half * r / 4f, (top - y) / 4f), side * side);
            }
        fall.Quads(rows, columns);
        Surface(parent, name, fall.Build(name, p => new Vector3(-p.x, 0, -p.z)), fallLava, false);
    }

    // ---------- Terrain ----------

    static Vector3 World(Vector2 xz) => new(xz.x, 0, xz.y);
    static Vector3 WorldOnGround(Vector2 xz, float lift = 0) => new(xz.x, terrain.SampleHeight(World(xz)) + terrain.transform.position.y + lift, xz.y);
    static float RadiusFromCone(float x, float z) => Vector2.Distance(new Vector2(x, z), new Vector2(root.position.x, root.position.z));

    static List<Vector2> Spline(Vector2[] points, float step)
    {
        var result = new List<Vector2>();
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Length - 1)];
            int count = Mathf.CeilToInt(Vector2.Distance(p1, p2) / step);
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)count;
                result.Add(.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t));
            }
        }
        result.Add(points[^1]);
        return result;
    }

    // Distance to a sampled river and the 0..1 position along it.
    static (float distance, float along) Nearest(List<Vector2> line, Vector2 p)
    {
        float best = float.MaxValue, along = 0;
        for (int i = 0; i < line.Count - 1; i++)
        {
            Vector2 a = line[i], b = line[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);
            if (d < best) { best = d; along = (i + t) / (line.Count - 1); }
        }
        return (best, along);
    }

    static float LakeDistance(Vector2 p)
    {
        var q = Quaternion.Euler(0, 0, -LakeAngle) * (Vector3)(p - LakeCenter);
        float angle = Mathf.Atan2(q.y, q.x);
        float scale = 1 + (Perlin(Mathf.Cos(angle) * 1.4f + 7, Mathf.Sin(angle) * 1.4f + 2) - .5f) * .3f;
        return (new Vector2(q.x / LakeRadii.x, q.y / LakeRadii.y).magnitude / scale - 1) * Mathf.Min(LakeRadii.x, LakeRadii.y);
    }

    static float RiverWidth(bool tube) => tube ? 3f : 2.6f;
    static float RiverSurface(bool tube, float along) => Mathf.Lerp(tube ? PortalFloor - ChannelDrop : 8.2f, LakeLevel, along);

    // Channel cross-section: bed under the lava, a levee just above it, then a blend back to the surrounding ground.
    static float Channel(float height, float distance, float halfWidth, float surface)
    {
        const float bank = 4.5f, blend = 9f;
        if (distance <= halfWidth) return surface - .4f;
        if (distance <= halfWidth + bank) return Mathf.Lerp(surface - .4f, surface + 1.2f, Mathf.SmoothStep(0, 1, (distance - halfWidth) / bank));
        return Mathf.Lerp(surface + 1.2f, height, Mathf.SmoothStep(0, 1, (distance - halfWidth - bank) / blend));
    }

    // ---------- Lava fields ----------

    static void LavaFields()
    {
        var fields = Group(root, "Lava fields");
        River(fields, "Tube lava river", TubeRiver, true);
        River(fields, "Flank lava river", FlankRiver, false);
        var lake = new Builder();
        const int rings = 14, sectors = 96;
        for (int i = 0; i <= rings; i++)
            for (int j = 0; j < sectors; j++)
            {
                float a = j * Mathf.PI * 2 / sectors, f = i / (float)rings * 1.2f;
                var offset = Quaternion.Euler(0, 0, LakeAngle) * new Vector3(Mathf.Cos(a) * LakeRadii.x * f, Mathf.Sin(a) * LakeRadii.y * f);
                var p = LakeCenter + (Vector2)offset;
                float distance = LakeDistance(p);
                lake.Add(Local(new Vector3(p.x, LakeLevel, p.y)), p / 6f, Mathf.Clamp01(1 + distance / 5f));
            }
        lake.Quads(rings, sectors, wrap: true);
        Surface(fields, "Lava lake", lake.Build("Lava lake", p => Vector3.up), poolLava, true);
    }

    static void River(Transform parent, string name, Vector2[] points, bool tube)
    {
        var line = Spline(points, .8f);
        var river = new Builder();
        const int across = 8;
        float travelled = 0;
        for (int i = 0; i < line.Count; i++)
        {
            var tangent = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
            var normal = new Vector2(-tangent.y, tangent.x);
            if (i > 0) travelled += Vector2.Distance(line[i], line[i - 1]);
            float along = i / (line.Count - 1f), half = RiverWidth(tube) + 1.8f + (Perlin(travelled * .08f, tube ? 1 : 2) - .5f) * 1.2f;
            for (int k = 0; k <= across; k++)
            {
                float side = k / (float)across * 2 - 1;
                var p = line[i] + normal * side * half;
                river.Add(Local(new Vector3(p.x, RiverSurface(tube, along), p.y)), new Vector2(side * half / 4f, travelled / 4f), Mathf.Abs(side));
            }
        }
        river.Quads(line.Count - 1, across);
        Surface(parent, name, river.Build(name, p => Vector3.up), riverLava, true);
    }

    static Vector3 Local(Vector3 world) => root.InverseTransformPoint(world);

    // ---------- Rocks, spires and portal ----------

    static void Dressing()
    {
        var rocks = Group(root, "Volcanic rocks");
        // Jagged crags crown the rim, leaving the cascade notch open.
        for (int i = 0; i < 22; i++)
        {
            float degrees = i * 360f / 22 + 5;
            if (Mathf.Abs(Mathf.DeltaAngle(degrees, 315)) < 14) continue;
            float a = degrees * Mathf.Deg2Rad, y = RimHeight(a);
            Place(rocks, "Rim crag", "Cliff_infernal", Polar(a, (OuterRadius(y, a) + InnerRadius(y, a)) * .5f, y + 2), new Vector3(9, 9 + i % 4 * 2.5f, 8), i * 37);
        }
        MouthRocks(rocks, "Portal rock", PortalFloor, TunnelOuter + 1, 1);
        for (int i = 0; i < 34; i++)
        {
            float degrees = i * 360f / 34 + (i % 3) * 4;
            if (Mathf.Abs(Mathf.DeltaAngle(degrees, 270)) < 18 || Mathf.Abs(Mathf.DeltaAngle(degrees, 315)) < 9) continue;
            float a = degrees * Mathf.Deg2Rad, r = 64 + (i % 4) * 3.5f;
            var world = root.TransformPoint(Polar(a, r, 0));
            Place(rocks, "Base boulder", i % 3 == 0 ? "Cliff_infernal" : "Rock_infernal", Local(WorldOnGround(new Vector2(world.x, world.z), 1.5f)), new Vector3(6 + i % 3 * 2, 4 + i % 4 * 1.5f, 6), i * 53);
        }
        // Basalt spire clusters frame the approach and the lake.
        Vector2[] spires = { new(653, 628), new(648, 600), new(724, 545), new(676, 545), new(714, 590), new(722, 693), new(752, 712), new(828, 578), new(758, 548), new(690, 530), new(665, 663), new(842, 705) };
        for (int i = 0; i < spires.Length; i++)
            for (int k = 0; k < 3; k++)
            {
                var p = spires[i] + new Vector2(Mathf.Cos(k * 2.1f + i), Mathf.Sin(k * 2.1f + i)) * (k == 0 ? 0 : 4.5f);
                float height = (k == 0 ? 16 : 9) + (i % 3) * 3;
                Place(rocks, "Basalt spire", "Cliff_infernal", Local(WorldOnGround(p, height * .45f)), new Vector3(4.5f, height, 4.5f), i * 41 + k * 77, new Vector3((k - 1) * 6, 0, (i % 2 * 2 - 1) * 4));
            }
        var random = new System.Random(4127);
        var tube = Spline(TubeRiver, 2);
        var flank = Spline(FlankRiver, 2);
        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < 70; attempt++)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2, r = 72 + (float)random.NextDouble() * 85;
            var world = root.position + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            var p = new Vector2(world.x, world.z);
            var local = Local(world);
            if (Nearest(tube, p).distance < 9 || Nearest(flank, p).distance < 8 || LakeDistance(p) < 5) continue;
            if (Heat(world.x, world.z) < .45f || MesaDistance(p) < 1.15f || CinderCones.Any(c => Vector2.Distance(p, c.at) < c.radius * .45f)) continue;
            if (Mathf.Abs(local.x - WalkwayX) < 16 && local.z < -60 && local.z > -110) continue;
            float u = (world.x - terrain.transform.position.x) / terrain.terrainData.size.x, v = (world.z - terrain.transform.position.z) / terrain.terrainData.size.z;
            if (terrain.terrainData.GetSteepness(u, v) > 30) continue;
            float size = 1.5f + (float)random.NextDouble() * 4f;
            Place(rocks, "Lava field boulder", random.Next(3) == 0 ? "Plate_infernal" : "Rock_infernal", Local(WorldOnGround(p, size * .25f)), new Vector3(size, size * .7f, size * 1.1f), random.Next(360));
            placed++;
        }
    }

    // Tumbled boulders frame a tube mouth and hide where it meets the shell; direction -1 faces into the caldera.
    static void MouthRocks(Transform parent, string name, float floor, float z, float direction)
    {
        (float x, float y, float dz, Vector3 size, string prefab)[] pile =
        {
            (-9.5f, 2.5f, 0, new Vector3(6, 6, 7), "Rock_infernal"), (10.5f, 2.5f, 1, new Vector3(6, 6, 7), "Rock_infernal"),
            (-8.5f, 8f, 3, new Vector3(6, 7, 8), "Cliff_infernal"), (9f, 8.5f, 3, new Vector3(6, 7, 8), "Cliff_infernal"),
            (-4.5f, 12.4f, 3.5f, new Vector3(8, 5, 9), "Rock_infernal"), (4.5f, 12.8f, 4, new Vector3(8, 5, 9), "Rock_infernal"),
            (0, 16f, 7, new Vector3(13, 6, 10), "Cliff_infernal")
        };
        for (int i = 0; i < pile.Length; i++)
        {
            var (x, y, dz, size, prefab) = pile[i];
            Place(parent, name, prefab, new Vector3(x, floor + y, z + dz * direction), size, i * 67 + (direction > 0 ? 0 : 33), new Vector3((i % 3 - 1) * 8, 0, (i % 2 * 2 - 1) * 6));
        }
    }

    // Fits an infernal rock prefab to a size at a root-local position, as walkable ground.
    static GameObject Place(Transform parent, string name, string prefab, Vector3 local, Vector3 size, float yaw, Vector3 tilt = default)
    {
        var rock = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rocks + prefab + ".prefab"), parent);
        rock.name = name;
        rock.transform.localPosition = Vector3.zero;
        rock.transform.localRotation = Quaternion.identity;
        var renderers = rock.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        rock.transform.position += root.TransformPoint(local) - bounds.center;
        rock.transform.Rotate(tilt.x, yaw, tilt.z, Space.Self);
        if (rock.GetComponentsInChildren<Collider>().Length == 0)
        {
            var filter = rock.GetComponentInChildren<MeshFilter>();
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        foreach (var collider in rock.GetComponentsInChildren<Collider>()) collider.gameObject.layer = ground;
        return rock;
    }

    // ---------- Particles ----------

    static void Effects()
    {
        var fx = Group(root, "Volcanic effects");
        var ember = FX.Mat("Ember", "Point5", true, 3f);
        var blob = FX.Mat("Lava droplet", "Point12", true, 4f, Folder);
        var smoke = FX.Mat("Volcanic smoke", "Smoke16", false, 1f, Folder);
        var ash = FX.Mat("Ash flake", "Spark5", false, 1f, Folder);
        var spark = FX.Mat("Spark", "Spark5", true, 3f);

        Summit(fx, smoke, ember);
        foreach (var name in new[] { "Grand lavafall", "North lava trickle", "South lava trickle", "Flank lava cascade" })
        {
            var surface = root.GetComponentsInChildren<MeshRenderer>().First(r => r.name == name);
            var drips = OnSurface(FX.Emitter(fx, name + " embers", ember, name.StartsWith("Grand") ? 30 : 10, new Vector2(1.2f, 2.4f), new Vector2(.06f, .16f), new Color(1, .55f, .15f), new Color(1, .3f, .05f)), surface);
            var main = drips.main; main.gravityModifier = -.08f; main.startSpeed = new MinMaxCurve(.3f, 1.2f);
            FX.Noise(drips, .8f, .6f);
        }
        Splash(fx, Polar(90 * Mathf.Deg2Rad, 21.5f, MoltenLevel), 1f, blob, smoke, ember);
        foreach (float degrees in new[] { 52f, 128f }) Splash(fx, Polar(degrees * Mathf.Deg2Rad, 23f, MoltenLevel), .35f, blob, smoke, ember);

        foreach (var name in new[] { "Molten moat", "Tube lava channel", "Tube lava river", "Flank lava river", "Lava lake" })
        {
            var surface = root.GetComponentsInChildren<MeshRenderer>().First(r => r.name == name);
            float area = surface.bounds.size.x * surface.bounds.size.z;
            var embers = OnSurface(FX.Emitter(fx, name + " embers", ember, Mathf.Clamp(area * .02f, 12, 60), new Vector2(1.5f, 3f), new Vector2(.05f, .12f), new Color(1, .6f, .2f), new Color(1, .28f, .04f)), surface);
            var main = embers.main; main.gravityModifier = -.12f; main.startSpeed = new MinMaxCurve(.2f, .8f); main.maxParticles = 400;
            FX.Noise(embers, .7f, .5f);
            var pops = OnSurface(FX.Emitter(fx, name + " bursts", blob, 0, new Vector2(.5f, .9f), new Vector2(.12f, .3f), new Color(1, .7f, .25f), new Color(1, .35f, .05f)), surface);
            pops.emission.SetBursts(new[] { new Burst(0, 2, 5, 0, .25f) { probability = .8f } });
            var popMain = pops.main; popMain.startSpeed = new MinMaxCurve(1.5f, 3.2f); popMain.gravityModifier = 1f;
            FX.Shrink(pops);
            var shape = pops.shape; shape.randomDirectionAmount = .25f;
            var wisps = OnSurface(FX.Emitter(fx, name + " heat haze", smoke, Mathf.Clamp(area * .002f, 2, 6), new Vector2(4, 7), new Vector2(2, 4), new Color(.55f, .32f, .2f, .22f), new Color(.3f, .26f, .24f, .18f)), surface);
            var wispMain = wisps.main; wispMain.startSpeed = new MinMaxCurve(.5f, 1.2f); wispMain.maxParticles = 60;
            FX.Spin(wisps, .2f);
            Grow(wisps, 2.2f);
        }
        // Rising heat fills the caldera and escapes through the open crater.
        var updraft = FX.Emitter(fx, "Caldera updraft", smoke, 3, new Vector2(10, 14), new Vector2(6, 10), new Color(.5f, .28f, .18f, .2f), new Color(.28f, .25f, .24f, .16f));
        updraft.transform.localPosition = new Vector3(0, MoltenLevel + 2, 0);
        FX.Ring(updraft, 18, 0, 1);
        FX.Drift(updraft, 5f, .6f);
        Grow(updraft, 3f);
        FX.Spin(updraft, .15f);

        Vector2[] vents = { new(735, 690), new(700, 688), new(760, 562), new(668, 642), new(726, 522), new(762, 706), new(652, 575), new(683, 657), new(808, 560) };
        foreach (var vent in vents)
        {
            var at = Local(WorldOnGround(vent, .2f));
            var plume = FX.Emitter(fx, "Fumarole steam", smoke, 5, new Vector2(5, 8), new Vector2(1.5f, 3), new Color(.75f, .72f, .7f, .35f), new Color(.6f, .58f, .57f, .25f));
            plume.transform.localPosition = at;
            var main = plume.main; main.startSpeed = new MinMaxCurve(2f, 3.5f); main.maxParticles = 60;
            var shape = plume.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8; shape.radius = .5f; shape.rotation = new Vector3(-90, 0, 0);
            Grow(plume, 3.5f);
            FX.Noise(plume, .6f, .4f);
            var spurts = FX.Emitter(plume.transform, "Fumarole sparks", spark, 0, new Vector2(.4f, .8f), new Vector2(.04f, .07f), new Color(1, .7f, .3f), new Color(1, .4f, .1f));
            spurts.emission.SetBursts(new[] { new Burst(0, 6, 12, 0, 2.5f) { probability = .5f } });
            var spurtMain = spurts.main; spurtMain.startSpeed = new MinMaxCurve(4, 8); spurtMain.gravityModifier = 1;
            var spurtShape = spurts.shape; spurtShape.enabled = true; spurtShape.shapeType = ParticleSystemShapeType.Cone; spurtShape.angle = 15; spurtShape.radius = .3f; spurtShape.rotation = new Vector3(-90, 0, 0);
            var renderer = spurts.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .05f;
            for (int k = 0; k < 3; k++)
                Place(fx, "Fumarole rock", "Rock_infernal", at + new Vector3(Mathf.Cos(k * 2.1f) * 1.4f, .3f, Mathf.Sin(k * 2.1f) * 1.4f), new Vector3(1.6f, 1f, 1.4f), k * 120);
        }

        // Ash drifts over the whole lava field; a few hot embers ride the same air.
        var fall = FX.Emitter(fx, "Ash fall", ash, 90, new Vector2(16, 22), new Vector2(.05f, .12f), new Color(.32f, .3f, .29f, .85f), new Color(.5f, .48f, .46f, .7f));
        fall.transform.localPosition = new Vector3(15, 55, -25);
        var ashMain = fall.main; ashMain.maxParticles = 2200; ashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        var ashShape = fall.shape; ashShape.enabled = true; ashShape.shapeType = ParticleSystemShapeType.Box; ashShape.scale = new Vector3(380, 2, 380);
        FX.Drift(fall, -2.4f, 1.2f);
        FX.Spin(fall, 2f);
        var drift = FX.Emitter(fx, "Drifting embers", ember, 25, new Vector2(8, 12), new Vector2(.06f, .12f), new Color(1, .55f, .2f), new Color(1, .3f, .05f));
        drift.transform.localPosition = new Vector3(15, 4, -25);
        var driftMain = drift.main; driftMain.maxParticles = 400; driftMain.simulationSpace = ParticleSystemSimulationSpace.World;
        var driftShape = drift.shape; driftShape.enabled = true; driftShape.shapeType = ParticleSystemShapeType.Box; driftShape.scale = new Vector3(300, 2, 300);
        FX.Drift(drift, 1.2f, 1.5f);
    }

    static void Summit(Transform fx, Material smoke, Material ember)
    {
        var plume = root.Find("Summit smoke");
        plume.localPosition = new Vector3(0, Rim - 4, 0);
        var particles = plume.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true; main.duration = 10; main.startLifetime = new MinMaxCurve(14, 20); main.startSpeed = new MinMaxCurve(3, 5.5f);
        main.startSize = new MinMaxCurve(10, 16); main.startRotation = new MinMaxCurve(0, Mathf.PI * 2); main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new MinMaxGradient(new Color(.24f, .22f, .21f, .75f), new Color(.36f, .33f, .31f, .6f));
        var emission = particles.emission; emission.rateOverTime = 9;
        var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = 14; shape.angle = 14; shape.rotation = new Vector3(-90, 0, 0);
        var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 1.6f; velocity.y = 1.2f; velocity.z = .5f;
        var color = particles.colorOverLifetime; color.enabled = true;
        // Underlit by the crater glow at first, then cooling to grey ash cloud.
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, .55f, .35f), 0), new GradientColorKey(Color.white, .3f), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = particles.sizeOverLifetime; size.enabled = true; size.size = new MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2.8f));
        var spin = particles.rotationOverLifetime; spin.enabled = true; spin.z = new MinMaxCurve(-.15f, .15f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = smoke; renderer.maxParticleSize = 3; renderer.shadowCastingMode = ShadowCastingMode.Off;
        particles.Play();

        var fountain = FX.Emitter(fx, "Crater ember fountain", ember, 35, new Vector2(3, 5), new Vector2(.35f, .7f), new Color(1, .6f, .2f), new Color(1, .3f, .05f));
        fountain.transform.localPosition = new Vector3(0, Rim - 10, 0);
        var fountainMain = fountain.main; fountainMain.startSpeed = new MinMaxCurve(10, 18); fountainMain.gravityModifier = .6f; fountainMain.maxParticles = 300;
        var fountainShape = fountain.shape; fountainShape.enabled = true; fountainShape.shapeType = ParticleSystemShapeType.Cone; fountainShape.angle = 18; fountainShape.radius = 8; fountainShape.rotation = new Vector3(-90, 0, 0);
        FX.Shrink(fountain);
    }

    static void Splash(Transform fx, Vector3 at, float scale, Material blob, Material smoke, Material ember)
    {
        var droplets = FX.Emitter(fx, "Lavafall splash", blob, 30 * scale, new Vector2(.7f, 1.3f), new Vector2(.2f, .5f), new Color(1, .7f, .25f), new Color(1, .35f, .05f));
        droplets.transform.localPosition = at;
        var main = droplets.main; main.startSpeed = new MinMaxCurve(4, 8); main.gravityModifier = 1.1f; main.maxParticles = 200;
        var shape = droplets.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35; shape.radius = 4 * scale; shape.rotation = new Vector3(-90, 0, 0);
        FX.Shrink(droplets);
        var steam = FX.Emitter(fx, "Lavafall steam", smoke, 5 * scale + 1, new Vector2(4, 6), new Vector2(3, 5), new Color(.6f, .35f, .24f, .35f), new Color(.35f, .3f, .28f, .25f));
        steam.transform.localPosition = at;
        var steamMain = steam.main; steamMain.startSpeed = new MinMaxCurve(1.5f, 3f);
        var steamShape = steam.shape; steamShape.enabled = true; steamShape.shapeType = ParticleSystemShapeType.Circle; steamShape.radius = 5 * scale; steamShape.rotation = new Vector3(90, 0, 0);
        Grow(steam, 2.5f);
        var sparks = FX.Emitter(fx, "Lavafall sparks", ember, 25 * scale, new Vector2(1.5f, 3), new Vector2(.06f, .14f), new Color(1, .6f, .2f), new Color(1, .3f, .05f));
        sparks.transform.localPosition = at;
        var sparkMain = sparks.main; sparkMain.startSpeed = new MinMaxCurve(2, 5); sparkMain.gravityModifier = -.1f;
        var sparkShape = sparks.shape; sparkShape.enabled = true; sparkShape.shapeType = ParticleSystemShapeType.Circle; sparkShape.radius = 5 * scale; sparkShape.rotation = new Vector3(90, 0, 0);
        FX.Noise(sparks, 1f, .6f);
    }

    static ParticleSystem OnSurface(ParticleSystem particles, MeshRenderer surface)
    {
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.MeshRenderer;
        shape.meshRenderer = surface;
        shape.alignToDirection = false;
        var main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        return particles;
    }

    static void Grow(ParticleSystem particles, float end)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new MinMaxCurve(end, AnimationCurve.Linear(0, 1 / end, 1, 1));
    }

    // ---------- Lights and atmosphere ----------

    static void Atmosphere()
    {
        var lights = Group(root, "Lava lights");
        Glow(lights, "Crater glow", new Vector3(0, Rim - 6, 0), 85, 9, new Color(1, .4f, .12f));
        Glow(lights, "Lavafall glow", Polar(90 * Mathf.Deg2Rad, 16, 20), 34, 9, new Color(1, .42f, .12f), LightShadows.Soft);
        for (int i = 0; i < 6; i++)
            Glow(lights, "Moat glow", Polar((30 + i * 60) * Mathf.Deg2Rad, 19.5f, MoltenLevel + 2.5f), 17, 6, new Color(1, .34f, .08f));
        for (int i = 0; i < 3; i++)
        {
            float z = Mathf.Lerp(TunnelInner - 6, TunnelOuter + 6, i / 2f);
            Glow(lights, "Tube glow", new Vector3(TubeCurve(z) + 4.4f, TubeFloor(z) + 1.2f, z), 14, 4, new Color(1, .35f, .08f));
        }
        Glow(lights, "Vent glow", Polar(90 * Mathf.Deg2Rad, 21, 49), 18, 7, new Color(1, .5f, .15f));
        foreach (var (points, tube) in new[] { (TubeRiver, true), (FlankRiver, false) })
        {
            var line = Spline(points, 1);
            for (int i = 6; i < line.Count; i += 22)
                Glow(lights, "River glow", Local(new Vector3(line[i].x, RiverSurface(tube, i / (line.Count - 1f)) + 2.5f, line[i].y)), 18, 4.5f, new Color(1, .32f, .07f));
        }
        Glow(lights, "Lake glow", Local(new Vector3(LakeCenter.x, LakeLevel + 4, LakeCenter.y)), 32, 7, new Color(1, .33f, .08f));
        for (int i = 0; i < 3; i++)
        {
            float y = Mathf.Lerp(Rim - 10, 14, i / 2f), a = 315 * Mathf.Deg2Rad;
            Glow(lights, "Cascade glow", Polar(a, OuterRadius(y, a) + 3, y), 22, 5, new Color(1, .38f, .1f));
        }

        var biome = Volume("Volcano biome atmosphere", Vector3.zero, 200, 60, 1, new Color(1f, .88f, .78f), 10, 8, .55f, .22f);
        biome.transform.SetParent(Group(root, "Atmosphere"), false);
        var caldera = Volume("Caldera heat", new Vector3(0, 25, 0), 30, 12, 2, new Color(1f, .8f, .66f), 16, 12, .85f, .32f);
        caldera.transform.SetParent(biome.transform.parent, false);
    }

    static void Glow(Transform parent, string name, Vector3 local, float range, float intensity, Color color, LightShadows shadows = LightShadows.None)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = local;
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        light.shadows = shadows;
    }

    static GameObject Volume(string name, Vector3 local, float radius, float blend, float priority, Color filter, float contrast, float saturation, float bloom, float vignette)
    {
        string path = Folder + "/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        foreach (var component in profile.components.ToArray()) { profile.Remove(component.GetType()); Object.DestroyImmediate(component, true); }
        var color = profile.Add<ColorAdjustments>(true);
        color.colorFilter.Override(filter); color.contrast.Override(contrast); color.saturation.Override(saturation);
        profile.Add<Bloom>(true).intensity.Override(bloom);
        var edge = profile.Add<Vignette>(true);
        edge.intensity.Override(vignette); edge.color.Override(new Color(.18f, .04f, .01f));
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        var go = new GameObject(name);
        go.transform.localPosition = local;
        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true; sphere.radius = radius;
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = false; volume.blendDistance = blend; volume.priority = priority; volume.sharedProfile = profile;
        return go;
    }

    // ---------- Cinematic and shared lava ----------

    // Orbit the plume, follow the cascade down, glide along the river into the tube and burst into the caldera.
    internal static void RetargetPreview()
    {
        root = GameObject.Find("Redwood Lava Cavern").transform;
        root.Find("Volcano preview landmark").localPosition = new Vector3(0, Rim, 0);
        Vector3[] positions =
        {
            new(170, 160, -190), new(95, 105, -175), new(85, 40, -115), new(45, 16, -110),
            // Two lined-up keys outside the mouth keep the spline from swinging into the tube walls.
            new(WalkwayX, PortalFloor + 7.5f, -100), new(WalkwayX, PortalFloor + 4f, -84), new(WalkwayX, TubeFloor(-52) + 3, -52), new(WalkwayX, ArenaLevel + 3.6f, -21), new(4, 24, -8)
        };
        Vector3[] targets =
        {
            new(0, 92, 0), new(10, 75, -15), new(45, 20, -50), new(5, 8, -82),
            new(WalkwayX, PortalFloor + 2.7f, -74), new(WalkwayX, PortalFloor + 2.8f, -60), new(WalkwayX, TubeFloor(-25) + 2.4f, -25), new(0, 18, 18), new(0, 30, 24)
        };
        BiomeCinematicSetup.AuthorFlight("Volcano Preview", new[] { 0, 2f, 4f, 5.6f, 7f, 7.9f, 9.4f, 10.7f, 12.6f },
            positions.Select(root.TransformPoint).ToArray(), targets.Select(root.TransformPoint).ToArray(),
            new[] { 62f, 56f, 52f, 50f, 52f, 58f, 66f, 72f, 74f });
    }

    // ---------- Scene objects ----------

    static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        if (parent == root) Undo.RegisterCreatedObjectUndo(group.gameObject, "Rebuild volcano biome");
        return group;
    }

    static GameObject Solid(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = Renderer(parent, name, mesh, material);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.layer = ground;
        return go;
    }

    // Lava with an optional solid floor just below its surface, so anything that falls in can still climb out.
    static GameObject Surface(Transform parent, string name, Mesh mesh, Material material, bool hazard)
    {
        var go = Renderer(parent, name, mesh, material);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        if (hazard)
        {
            var floor = new GameObject("Lava hazard floor");
            floor.transform.SetParent(go.transform, false);
            floor.transform.localPosition = Vector3.down * .3f;
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            floor.layer = ground;
        }
        return go;
    }

    static GameObject Renderer(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    sealed class Builder
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uvs = new(), edges = new();
        readonly List<int> triangles = new();

        public void Add(Vector3 position, Vector2 uv = default, float edge = 0)
        {
            vertices.Add(position); uvs.Add(uv); edges.Add(new Vector2(edge, 0));
        }

        // Rows x columns quads over vertices added row by row; wrapped grids close the ring without a seam.
        public void Quads(int rows, int columns, bool wrap = false, Func<int, int, bool> skip = null, int start = 0)
        {
            int stride = wrap ? columns : columns + 1;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                {
                    if (skip != null && skip(i, j)) continue;
                    int a = start + i * stride + j, b = start + i * stride + (wrap ? (j + 1) % columns : j + 1);
                    int c = b + stride, d = a + stride;
                    triangles.AddRange(new[] { a, b, c, a, c, d });
                }
        }

        public void Grid(int rows, int columns, Func<int, int, Vector3> vertex, Func<int, int, bool> skip = null, bool wrap = false)
        {
            int start = vertices.Count;
            for (int i = 0; i <= rows; i++)
                for (int j = 0; j < (wrap ? columns : columns + 1); j++)
                    Add(vertex(i, j));
            Quads(rows, columns, wrap, skip, start);
        }

        // Each triangle is wound to face the given direction, so no surface depends on hand-ordered indices.
        public Mesh Build(string name, Func<Vector3, Vector3> facing)
        {
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing((a + b + c) / 3)) < 0)
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            }
            string path = Folder + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, edges);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            UnityEngine.Assertions.Assert.IsTrue(mesh.bounds.size.sqrMagnitude > 0, name + " must have extent.");
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
