using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.ParticleSystem;
using FX = ElementalEffectsSetup;
using Object = UnityEngine.Object;

// Terrain half of the volcano biome. A heat field of lava-flow lobes around the cone shapes the lowland
// (flow tongues, cinder cones, a basalt mesa, fissures), its paint and its forest; the needle ranges behind
// the cone are smoothed into two smoking stratovolcanoes with lava running down their flanks.
public static partial class VolcanoBiomeBuilder
{
    const string BaselinePath = Folder + "/Terrain baseline v2.bytes";
    const int BaselineVersion = 2;
    // Lava-flow lobes run from the cone toward these ends; the armory side stays green.
    static readonly Vector2[] LobeEnds = { new(610, 600), new(650, 470), new(790, 460), new(750, 830), new(670, 740) };
    static readonly float[] LobeWidths = { 38, 34, 32, 30, 28 };
    // Breach angles are world degrees (0 = east, 90 = north), facing where their lava spills.
    static readonly (Vector2 at, float radius, float height, float breach)[] CinderCones =
        { (new(668, 702), 30, 24, 335), (new(742, 505), 24, 17, 69), (new(625, 662), 20, 13, 353) };
    static readonly (Vector2 at, float radius, float height, float crater, float breach)[] Massifs =
        { (new(938, 575), 105, 215, 26, 155), (new(868, 838), 100, 190, 22, 250) };
    static readonly Vector2 MesaCenter = new(735, 742), MesaSize = new(26, 17);
    const float MesaAngle = 30, MesaHeight = 9;
    static readonly Vector2[][] FissureLines =
    {
        new Vector2[] { new(690, 660), new(672, 650), new(655, 640), new(630, 620) },
        new Vector2[] { new(820, 580), new(828, 562), new(835, 545), new(830, 515) },
        new Vector2[] { new(700, 770), new(692, 783), new(690, 795), new(700, 820) }
    };
    static readonly List<List<Vector3>> mountainFlows = new();

    static float Sq(float x) => x * x;
    static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));
    static float Ground(Vector2 p) => terrain.SampleHeight(World(p)) + terrain.transform.position.y;

    // ---------- Shape functions ----------

    // 0 outside the lava fields, 1 on the cone and along the lobes; drives landforms, paint and vegetation.
    static float Heat(float x, float z)
    {
        var p = new Vector2(x, z) + Warp(x, z);
        float heat = 1 - Smooth(90, 175, Vector2.Distance(p, new Vector2(root.position.x, root.position.z)));
        return Mathf.Clamp01(Mathf.Max(heat, Lobe(p) * .95f) + (Perlin(x * .021f + 31, z * .021f + 7) - .5f) * .22f);
    }

    static Vector2 Warp(float x, float z) => new((Perlin(x * .011f, z * .011f) - .5f) * 40, (Perlin(x * .011f + 50, z * .011f + 50) - .5f) * 40);

    static float Lobe(Vector2 p)
    {
        var centre = new Vector2(root.position.x, root.position.z);
        float best = 0;
        for (int i = 0; i < LobeEnds.Length; i++)
        {
            var axis = LobeEnds[i] - centre;
            float length = axis.magnitude;
            axis /= length;
            float along = Vector2.Dot(p - centre, axis), across = Mathf.Abs(Vector2.Dot(p - centre, new Vector2(-axis.y, axis.x)));
            if (along < 0 || along > length) continue;
            float width = LobeWidths[i] * (.55f + .45f * Mathf.Sin(Mathf.PI * (along / length * .9f + .1f)));
            best = Mathf.Max(best, (1 - Smooth(.55f, 1f, across / width)) * (1 - Smooth(length * .82f, length, along)));
        }
        return best;
    }

    static float CinderHeight(Vector2 p, (Vector2 at, float radius, float height, float breach) cone)
    {
        float r = Vector2.Distance(p, cone.at);
        if (r >= cone.radius) return 0;
        const float crater = .32f;
        float t = r / cone.radius, angle = Mathf.Atan2(p.y - cone.at.y, p.x - cone.at.x) * Mathf.Rad2Deg;
        float notch = 1 - .4f * Mathf.Exp(-Sq(Mathf.DeltaAngle(angle, cone.breach) / 35f));
        float outer = cone.height * Mathf.Pow(1 - Mathf.Max(t, crater), 1.3f) * notch;
        return t >= crater ? cone.height * Mathf.Pow(1 - t, 1.3f) * notch : outer - cone.height * .5f * (1 - Sq(t / crater));
    }

    static float MassifHeight(Vector2 p, (Vector2 at, float radius, float height, float crater, float breach) massif)
    {
        float r = Vector2.Distance(p, massif.at);
        if (r >= massif.radius) return 0;
        float t = r / massif.radius, angle = Mathf.Atan2(p.y - massif.at.y, p.x - massif.at.x);
        float breach = Mathf.Exp(-Sq(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, massif.breach) / 28f));
        float rimT = massif.crater / massif.radius;
        float rim = massif.height * Mathf.Pow(1 - rimT, 1.5f) - breach * massif.height * .12f;
        if (r < massif.crater) return rim - massif.height * .2f * (1 - Sq(r / massif.crater));
        // Radial erosion gullies scored into a concave stratovolcano, deepest mid-slope.
        float gully = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 9 + Perlin(t * 3, angle) * 3)), 6) * 9 * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .2f) / .8f));
        return massif.height * Mathf.Pow(1 - t, 1.5f) - gully - breach * massif.height * .12f * (1 - Smooth(rimT, rimT + .35f, t));
    }

    static float MesaDistance(Vector2 p)
    {
        var q = Quaternion.Euler(0, 0, -MesaAngle) * (Vector3)(p - MesaCenter);
        return Mathf.Pow(Mathf.Pow(Mathf.Abs(q.x / MesaSize.x), 4) + Mathf.Pow(Mathf.Abs(q.y / MesaSize.y), 4), .25f);
    }

    // Highland weight: the volcanic side of the ranges (MountainLandscapeBuilder's basalt zone) above the valley floor.
    static float Highland(float x, float z, float originalHeight) => MountainLandscapeBuilder.VolcanoWeight(x, z) * Smooth(25, 60, originalHeight);

    // ---------- Sculpt ----------

    static void Sculpt()
    {
        var data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Sculpt volcanic lava fields");
        var trees = Baseline(data);
        var tube = Spline(TubeRiver, 1f);
        var flank = Spline(FlankRiver, 1f);
        var fissures = FissureLines.Select(f => Spline(f, 1f)).ToArray();
        Vector3 origin = terrain.transform.position;
        float cell = data.size.x / (data.heightmapResolution - 1);
        var region = Region(data.heightmapResolution, cell);
        var original = data.GetHeights(region.x, region.y, region.width, region.height);
        var blurred = Blur(original, 8);
        var heights = new float[region.height, region.width];
        for (int z = 0; z < region.height; z++)
            for (int x = 0; x < region.width; x++)
            {
                float wx = origin.x + (region.x + x) * cell, wz = origin.z + (region.y + z) * cell;
                var p = new Vector2(wx, wz);
                float fade = BorderFade(x, z, region.width, region.height);
                float h = original[z, x] * data.size.y, r = RadiusFromCone(wx, wz);

                // Needle ranges on the volcanic side become broad ridged massifs with two stratovolcanoes.
                float highland = Highland(wx, wz, h) * fade;
                float massif = Massifs.Max(m => MassifHeight(p, m));
                if (highland > 0)
                {
                    float ridges = Mathf.Pow(1 - Mathf.Abs(Perlin(wx * .012f + 3, wz * .012f + 9) * 2 - 1), 2) * 18 - 6;
                    float calm = blurred[z, x] * data.size.y * .9f + ridges * Smooth(40, 120, h);
                    h = Mathf.Lerp(h, Mathf.Max(calm * (1 - .45f * Smooth(0, 200, massif)), massif), highland);
                }
                // The massifs' skirts reach down into the valley floor.
                h = Mathf.Lerp(h, Mathf.Max(h, massif), fade * MountainLandscapeBuilder.VolcanoWeight(wx, wz));

                // Lowland: a lava shield toward the cone, flat-topped flow tongues with walkable fronts and pressure ridges.
                h += 6.5f * (1 - Smooth(70, 175, r)) * (.8f + .4f * Perlin(wx * .02f, wz * .02f)) * fade;
                float lobe = Lobe(p + Warp(wx, wz));
                float tongue = Smooth(.35f, .6f, lobe) * (2.8f + 1.4f * Perlin(wx * .03f + 17, wz * .03f));
                tongue += Mathf.Pow(1 - Mathf.Abs(Perlin(wx * .06f + 5, wz * .06f + 11) * 2 - 1), 3) * .9f * lobe;
                h += tongue * fade * (1 - highland);
                foreach (var cone in CinderCones) h += CinderHeight(p, cone);
                h += MesaHeight * Smooth(1.06f, .9f, MesaDistance(p)) * (1 + (Perlin(wx * .08f, wz * .08f) - .5f) * .15f);
                foreach (var fissure in fissures)
                {
                    float d = Nearest(fissure, p).distance;
                    if (d < 4) h -= 1.8f * (1 - Smooth(1f, 3.2f, d));
                }

                h = Mathf.Lerp(h, Mathf.Min(h, 4f), Smooth(64, 56, r));
                var local = root.InverseTransformPoint(new Vector3(wx, 0, wz));
                float apron = (1 - Smooth(9, 15, Mathf.Abs(local.x))) * (1 - Smooth(-90, -104, local.z)) * Smooth(-58, -64, local.z);
                h = Mathf.Lerp(h, PortalFloor - .12f - Mathf.Max(0, -74 - local.z) * .03f, apron);
                var (dt, at) = Nearest(tube, p);
                if (dt < 20) h = Channel(h, dt, RiverWidth(true), RiverSurface(true, at));
                var (df, af) = Nearest(flank, p);
                if (df < 20) h = Channel(h, df, RiverWidth(false), RiverSurface(false, af));
                float dl = LakeDistance(p);
                if (dl < 16) h = Channel(h, dl + 1.5f, 1.5f, LakeLevel);
                heights[z, x] = Mathf.Clamp01((h - origin.y) / data.size.y);
            }
        data.SetHeights(region.x, region.y, heights);
        terrain.Flush();
        mountainFlows.Clear();
        foreach (var m in Massifs)
            foreach (float spread in new[] { 0f, m.breach < 200 ? 50f : -45f })
            {
                float a = (m.breach + spread) * Mathf.Deg2Rad;
                mountainFlows.Add(Trace(m.at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m.crater * 1.15f, 90));
            }
        var lava = new List<List<Vector2>> { tube, flank };
        lava.AddRange(fissures);
        lava.AddRange(mountainFlows.Select(f => f.Select(v => new Vector2(v.x, v.z)).ToList()));
        Paint(data, lava, original, region, cell);
        Vegetation(data, trees, lava);
        terrain.Flush();
        Physics.SyncTransforms();
    }

    // Lava finds its way down: steepest descent with a little momentum, until the slope gives out.
    static List<Vector3> Trace(Vector2 start, int steps)
    {
        var path = new List<Vector3>();
        Vector2 p = start, direction = Vector2.zero;
        for (int i = 0; i < steps; i++)
        {
            float h = Ground(p);
            path.Add(new Vector3(p.x, h, p.y));
            var gradient = new Vector2(Ground(p + Vector2.right * 2) - Ground(p - Vector2.right * 2), Ground(p + Vector2.up * 2) - Ground(p - Vector2.up * 2));
            if (gradient.magnitude < .2f || h < 18) break;
            direction = (direction * .55f - gradient.normalized * .45f).normalized;
            p += direction * 3;
        }
        return path;
    }

    static float[,] Blur(float[,] source, int radius)
    {
        int h = source.GetLength(0), w = source.GetLength(1);
        var a = (float[,])source.Clone();
        var b = new float[h, w];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++) sum += a[z, Mathf.Clamp(x + k, 0, w - 1)];
                    b[z, x] = sum / (2 * radius + 1);
                }
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++) sum += b[Mathf.Clamp(z + k, 0, h - 1), x];
                    a[z, x] = sum / (2 * radius + 1);
                }
        }
        return a;
    }

    // Ground this builder reshapes; other terrain checks leave it to VolcanoChecks.
    internal static bool Sculpts(float x, float z) => x >= 540 && z >= 380;

    // Fades edits out toward the edited region's border so no straight seam shows.
    static float BorderFade(int x, int z, int width, int height) => Mathf.SmoothStep(0, 1, Mathf.Min(Mathf.Min(x, z), Mathf.Min(width - 1 - x, height - 1 - z)) / 14f);

    // Texels of a terrain map covering the lava fields and the volcanic ranges behind them.
    static RectInt Region(int resolution, float cell)
    {
        Vector3 origin = terrain.transform.position;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((540 - origin.x) / cell)), z0 = Mathf.Max(0, Mathf.FloorToInt((380 - origin.z) / cell));
        int x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((1000 - origin.x) / cell)), z1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((1000 - origin.z) / cell));
        return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
    }

    // The first build snapshots the untouched region (heights, paint, grass, trees); every build restores it
    // before sculpting, so rebuilding never compounds the lift, carving, paint, thinning or charring.
    static TreeInstance[] Baseline(TerrainData data)
    {
        var h = Region(data.heightmapResolution, data.size.x / (data.heightmapResolution - 1));
        var a = Region(data.alphamapResolution, data.size.x / data.alphamapResolution);
        var d = Region(data.detailResolution, data.size.x / data.detailResolution);
        if (!File.Exists(BaselinePath))
        {
            using var writer = new BinaryWriter(new GZipStream(File.Create(BaselinePath), System.IO.Compression.CompressionLevel.Optimal));
            writer.Write(BaselineVersion);
            foreach (float value in data.GetHeights(h.x, h.y, h.width, h.height)) writer.Write(value);
            writer.Write(data.alphamapLayers);
            foreach (float value in data.GetAlphamaps(a.x, a.y, a.width, a.height)) writer.Write(value);
            writer.Write(data.detailPrototypes.Length);
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                foreach (int value in data.GetDetailLayer(d.x, d.y, d.width, d.height, layer)) writer.Write((short)value);
            writer.Write(data.treeInstanceCount);
            foreach (var tree in data.treeInstances)
            {
                writer.Write(tree.position.x); writer.Write(tree.position.y); writer.Write(tree.position.z);
                writer.Write(tree.widthScale); writer.Write(tree.heightScale); writer.Write(tree.rotation); writer.Write(tree.prototypeIndex);
                writer.Write(tree.color.r); writer.Write(tree.color.g); writer.Write(tree.color.b); writer.Write(tree.color.a);
                writer.Write(tree.lightmapColor.r); writer.Write(tree.lightmapColor.g); writer.Write(tree.lightmapColor.b); writer.Write(tree.lightmapColor.a);
            }
        }
        using var reader = new BinaryReader(new GZipStream(File.OpenRead(BaselinePath), CompressionMode.Decompress));
        if (reader.ReadInt32() != BaselineVersion) throw new InvalidOperationException("Restore the original terrain and delete " + BaselinePath + " before rebuilding.");
        var heights = new float[h.height, h.width];
        for (int z = 0; z < h.height; z++) for (int x = 0; x < h.width; x++) heights[z, x] = reader.ReadSingle();
        data.SetHeights(h.x, h.y, heights);
        int layers = reader.ReadInt32();
        // Layers this builder appended after the snapshot start empty.
        var maps = new float[a.height, a.width, data.alphamapLayers];
        for (int z = 0; z < a.height; z++) for (int x = 0; x < a.width; x++) for (int l = 0; l < layers; l++) maps[z, x, l] = reader.ReadSingle();
        data.SetAlphamaps(a.x, a.y, maps);
        int details = reader.ReadInt32();
        for (int layer = 0; layer < details; layer++)
        {
            var density = new int[d.height, d.width];
            for (int z = 0; z < d.height; z++) for (int x = 0; x < d.width; x++) density[z, x] = reader.ReadInt16();
            data.SetDetailLayer(d.x, d.y, layer, density);
        }
        var trees = new TreeInstance[reader.ReadInt32()];
        for (int i = 0; i < trees.Length; i++)
            trees[i] = new TreeInstance
            {
                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                widthScale = reader.ReadSingle(), heightScale = reader.ReadSingle(), rotation = reader.ReadSingle(), prototypeIndex = reader.ReadInt32(),
                color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                lightmapColor = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte())
            };
        return trees;
    }

    // ---------- Paint and vegetation ----------

    static TerrainLayer Layer(string name, string set, float tile)
    {
        string path = Folder + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = Tex(set + "_BaseColor");
        layer.normalMapTexture = Tex(set + "_Normal");
        layer.tileSize = Vector2.one * tile;
        layer.smoothness = .08f;
        // The pack's crust colours are near-molten; darken them so the real lava stays the brightest thing.
        layer.diffuseRemapMax = new Vector4(.62f, .5f, .45f, 1);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static void Paint(TerrainData data, List<List<Vector2>> lava, float[,] originalHeights, RectInt heightRegion, float heightCell)
    {
        var layers = data.terrainLayers.ToList();
        int Index(TerrainLayer layer) { int i = layers.IndexOf(layer); if (i < 0) { layers.Add(layer); i = layers.Count - 1; } return i; }
        int crust = Index(Layer("Volcanic lava crust", "Lava_Ground_01/Lava_Ground_1", 8));
        int cracked = Index(Layer("Volcanic cracked ground", "Lava_Ground_02/Lava_Ground_02", 10));
        int ash = Index(AssetDatabase.LoadAssetAtPath<TerrainLayer>(LavaSet + "Rock_Ground/Rock_Ground.terrainlayer"));
        // Vertical basalt columns hide the stretching terrain textures show on steep faces.
        int cliff = Index(AssetDatabase.LoadAssetAtPath<TerrainLayer>(LavaSet + "Cliff_5/Cliff_5.terrainlayer"));
        int basalt = layers.FindIndex(l => l != null && l.name == "Volcanic mountain basalt");
        int scorched = layers.FindIndex(l => l != null && l.name == "Forest_Ground_12");
        if (basalt < 0) basalt = ash;
        if (scorched < 0) scorched = ash;
        data.terrainLayers = layers.ToArray();
        float cell = data.size.x / data.alphamapResolution;
        Vector3 origin = terrain.transform.position;
        var region = Region(data.alphamapResolution, cell);
        int x0 = region.x, z0 = region.y, w = region.width, h = region.height;
        var maps = data.GetAlphamaps(x0, z0, w, h);
        int count = layers.Count;
        var target = new float[count];
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float wx = origin.x + (x0 + x + .5f) * cell, wz = origin.z + (z0 + z + .5f) * cell;
                float u = (wx - origin.x) / data.size.x, v = (wz - origin.z) / data.size.z;
                float height = data.GetInterpolatedHeight(u, v), slope = data.GetSteepness(u, v);
                int hx = Mathf.Clamp(Mathf.RoundToInt((wx - origin.x) / heightCell) - heightRegion.x, 0, heightRegion.width - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt((wz - origin.z) / heightCell) - heightRegion.y, 0, heightRegion.height - 1);
                var p = new Vector2(wx, wz);
                float heat = Heat(wx, wz);
                // Massif skirts that grew over the valley count as highland too, so no meadow paint climbs them.
                float highland = Mathf.Max(Highland(wx, wz, originalHeights[hz, hx] * data.size.y), Smooth(2, 25, Massifs.Max(m => MassifHeight(p, m))));
                float lowland = Smooth(.12f, .34f, heat);
                float strength = Mathf.Max(lowland, highland) * BorderFade(x, z, w, h);
                if (strength <= 0) continue;
                float lavaDistance = LakeDistance(p);
                foreach (var line in lava) lavaDistance = Mathf.Min(lavaDistance, Nearest(line, p).distance - 3f);
                float steep = Smooth(30, 48, slope), near = (1 - Smooth(1f, 6.5f, lavaDistance)) * (1 - steep);
                float highShare = highland / Mathf.Max(lowland + highland, 1e-4f);
                float scorch = (1 - highShare) * (1 - Smooth(.3f, .55f, heat));
                float plates = Smooth(.5f, .7f, Perlin(wx * .028f + 7, wz * .028f)) * .7f * (1 - highShare) * Smooth(.55f, .8f, heat);
                // Cooled flow tongues stay dark basalt; the ground between them is lighter ash, so the lobes read from above.
                float tongue = Smooth(.35f, .6f, Lobe(p + Warp(wx, wz)));
                float ashShare = Mathf.Lerp(Mathf.Lerp(.8f, .15f, tongue), .1f + .8f * Smooth(70, 170, height), highShare);
                Array.Clear(target, 0, count);
                target[crust] += near;
                float rest = 1 - near;
                target[cliff] += rest * steep;
                rest *= 1 - steep;
                target[scorched] += rest * scorch;
                rest *= 1 - scorch;
                target[cracked] += rest * plates;
                rest *= 1 - plates;
                target[ash] += rest * ashShare;
                target[basalt] += rest * (1 - ashShare);
                for (int l = 0; l < count; l++) maps[z, x, l] = Mathf.Lerp(maps[z, x, l], target[l], strength);
                float sum = 0;
                for (int l = 0; l < count; l++) sum += maps[z, x, l];
                for (int l = 0; l < count; l++) maps[z, x, l] /= Mathf.Max(sum, 1e-4f);
            }
        data.SetAlphamaps(x0, z0, maps);
    }

    // Forest burns from the outside in: thinned, then charred snags, then nothing on the hot ground.
    static void Vegetation(TerrainData data, TreeInstance[] baseline, List<List<Vector2>> lava)
    {
        var charredPrefab = CharredTree();
        var prototypes = data.treePrototypes.ToList();
        int charred = prototypes.FindIndex(t => t.prefab == charredPrefab);
        if (charred < 0) { prototypes.Add(new TreePrototype { prefab = charredPrefab }); charred = prototypes.Count - 1; data.treePrototypes = prototypes.ToArray(); }
        Vector3 origin = terrain.transform.position;
        var random = new System.Random(911);
        var kept = new List<TreeInstance>();
        foreach (var source in baseline)
        {
            var tree = source;
            var p = Vector3.Scale(tree.position, data.size) + origin;
            float heat = Heat(p.x, p.z), highland = Highland(p.x, p.z, p.y);
            if (heat > .5f || highland > .5f) continue;
            if (heat > .27f)
            {
                if (random.NextDouble() < .7) { tree.prototypeIndex = charred; tree.color = Color.white; kept.Add(tree); }
                continue;
            }
            if (heat > .17f && random.NextDouble() < .35) continue;
            kept.Add(tree);
        }
        // Extra charred stands where the flows ate into the forest.
        for (int attempt = 0, added = 0; attempt < 2500 && added < 220; attempt++)
        {
            var p = new Vector2(560 + (float)random.NextDouble() * 400, 400 + (float)random.NextDouble() * 460);
            float heat = Heat(p.x, p.y);
            if (heat < .3f || heat > .62f || lava.Any(l => Nearest(l, p).distance < 9) || LakeDistance(p) < 6) continue;
            if (CinderCones.Any(c => Vector2.Distance(p, c.at) < c.radius * .5f) || MesaDistance(p) < 1.2f) continue;
            float u = (p.x - origin.x) / data.size.x, v = (p.y - origin.z) / data.size.z;
            if (data.GetSteepness(u, v) > 25) continue;
            float scale = .8f + (float)random.NextDouble() * .5f;
            kept.Add(new TreeInstance { position = new Vector3(u, 0, v), widthScale = scale, heightScale = scale * (.9f + (float)random.NextDouble() * .3f), rotation = (float)random.NextDouble() * Mathf.PI * 2, prototypeIndex = charred, color = Color.white, lightmapColor = Color.white });
            added++;
        }
        data.SetTreeInstances(kept.ToArray(), true);

        float cell = data.size.x / data.detailResolution;
        var region = Region(data.detailResolution, cell);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var detail = data.GetDetailLayer(region.x, region.y, region.width, region.height, layer);
            for (int z = 0; z < region.height; z++)
                for (int x = 0; x < region.width; x++)
                {
                    float wx = origin.x + (region.x + x + .5f) * cell, wz = origin.z + (region.y + z + .5f) * cell;
                    float keep = Mathf.Lerp(1, 1 - Smooth(.15f, .4f, Heat(wx, wz)), BorderFade(x, z, region.width, region.height));
                    detail[z, x] = Mathf.RoundToInt(detail[z, x] * keep * keep);
                }
            data.SetDetailLayer(region.x, region.y, layer, detail);
        }
    }

    // A leafless, fire-blackened variant of the broadleaf tree; faint embers still glow in the bark.
    static GameObject CharredTree()
    {
        string path = Folder + "/Charred tree.prefab";
        var bark = Asset("Charred bark", "Universal Render Pipeline/Lit");
        bark.SetTexture("_BaseMap", Tex("Cliff_5/Cliff_5_BaseColor"));
        bark.SetColor("_BaseColor", new Color(.3f, .27f, .25f));
        bark.SetTexture("_EmissionMap", Tex("Cliff_3/Cliff_3_Emissive"));
        bark.SetColor("_EmissionColor", new Color(.35f, .07f, .01f));
        bark.EnableKeyword("_EMISSION");
        bark.SetFloat("_Smoothness", .1f);
        EditorUtility.SetDirty(bark);
        // Fully clipped, so the foliage submesh draws nothing, shadows included.
        var hidden = Asset("Hidden foliage", "Universal Render Pipeline/Unlit");
        hidden.SetColor("_BaseColor", new Color(0, 0, 0, 0));
        hidden.SetFloat("_AlphaClip", 1);
        hidden.SetFloat("_Cutoff", 1);
        hidden.EnableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(hidden);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/BroadleafTree_01_Green.prefab");
        var copy = Object.Instantiate(source);
        try
        {
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && m.name.Contains("Bark") ? bark : hidden).ToArray();
            return PrefabUtility.SaveAsPrefabAsset(copy, path);
        }
        finally { Object.DestroyImmediate(copy); }
    }

    // ---------- Landforms and highlands ----------

    static void Landforms()
    {
        var group = Group(root, "Lava landforms");
        var smoke = FX.Mat("Volcanic smoke", "Smoke16", false, 1f, Folder);
        var ember = FX.Mat("Ember", "Point5", true, 3f);
        for (int i = 0; i < CinderCones.Length; i++)
        {
            var cone = CinderCones[i];
            float floor = Ground(cone.at);
            Plume(group, "Cinder cone smoke", Local(new Vector3(cone.at.x, floor + 1, cone.at.y)), smoke, i == 0 ? 2 : 1.2f, 1.4f);
            if (i != 0) continue;
            // The largest cone still holds a lava pool in its crater.
            var pool = new Builder();
            float level = floor + 1.2f, radius = cone.radius * .32f * .85f;
            for (int ring = 0; ring <= 6; ring++)
                for (int j = 0; j < 48; j++)
                {
                    float a = j * Mathf.PI * 2 / 48, f = ring / 6f * 1.15f;
                    var w = cone.at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * f;
                    pool.Add(Local(new Vector3(w.x, level, w.y)), w / 5f, Mathf.Clamp01(f));
                }
            pool.Quads(6, 48, wrap: true);
            var surface = Surface(group, "Cinder crater lava", pool.Build("Cinder crater lava", q => Vector3.up), poolLava, true);
            Glow(group, "Cinder crater glow", Local(new Vector3(cone.at.x, level + 4, cone.at.y)), 40, 8, new Color(1, .38f, .1f));
            Embers(group, "Cinder crater embers", surface.GetComponent<MeshRenderer>(), ember, 25);
        }
        for (int i = 0; i < FissureLines.Length; i++)
        {
            var line = Spline(FissureLines[i], .8f);
            var crack = new Builder();
            float travelled = 0;
            for (int k = 0; k < line.Count; k++)
            {
                var tangent = (line[Mathf.Min(k + 1, line.Count - 1)] - line[Mathf.Max(k - 1, 0)]).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                if (k > 0) travelled += Vector2.Distance(line[k], line[k - 1]);
                float level = Ground(line[k]) + .45f, half = 1.6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / (line.Count - 1f) * .9f + .05f));
                for (int s = 0; s <= 4; s++)
                {
                    float side = s / 4f * 2 - 1;
                    var q = line[k] + normal * side * half;
                    crack.Add(Local(new Vector3(q.x, level, q.y)), new Vector2(side * half / 4f, travelled / 4f), Mathf.Abs(side));
                }
            }
            crack.Quads(line.Count - 1, 4);
            var surface = Surface(group, "Lava fissure", crack.Build("Lava fissure " + i, q => Vector3.up), riverLava, true);
            Embers(group, "Fissure embers", surface.GetComponent<MeshRenderer>(), ember, 18);
            for (int k = line.Count / 4; k < line.Count; k += line.Count / 2)
                Glow(group, "Fissure glow", Local(new Vector3(line[k].x, Ground(line[k]) + 2, line[k].y)), 16, 4, new Color(1, .32f, .07f));
            Plume(group, "Fissure steam", Local(new Vector3(line[line.Count / 2].x, Ground(line[line.Count / 2]), line[line.Count / 2].y)), smoke, .8f, 1f);
        }
        // Basalt columns wall the mesa's cliffs.
        for (int i = 0; i < 26; i++)
        {
            float a = i * Mathf.PI * 2 / 26;
            var dir = Quaternion.Euler(0, 0, MesaAngle) * new Vector3(Mathf.Cos(a) * MesaSize.x, Mathf.Sin(a) * MesaSize.y);
            var edge = MesaCenter + (Vector2)dir * 1.0f;
            float top = Ground(MesaCenter);
            Place(group, "Mesa basalt column", "Cliff_infernal", Local(new Vector3(edge.x, top - MesaHeight * .45f, edge.y)), new Vector3(5, MesaHeight + 3 + i % 3 * 1.5f, 5), i * 53);
        }
    }

    static void Highlands()
    {
        var group = Group(root, "Volcanic highlands");
        var smoke = FX.Mat("Volcanic smoke", "Smoke16", false, 1f, Folder);
        var ember = FX.Mat("Ember", "Point5", true, 3f);
        // The ranges were reshaped, so their old crags and fissures would float or sink: replace them.
        var outcrops = GameObject.Find("Background mountain outcrops");
        if (outcrops != null)
            foreach (var crag in outcrops.transform.Cast<Transform>().ToArray())
            {
                // Fissures keep world-space vertices under an origin transform, so judge by bounds.
                var renderer = crag.GetComponentInChildren<Renderer>();
                var p = renderer != null ? renderer.bounds.center : crag.position;
                if (p.x > 540 && p.z > 380 && MountainLandscapeBuilder.VolcanoWeight(p.x, p.z) > .01f) Undo.DestroyObjectImmediate(crag.gameObject);
            }
        foreach (var m in Massifs)
        {
            float floor = Ground(m.at);
            var crater = new Builder();
            for (int ring = 0; ring <= 6; ring++)
                for (int j = 0; j < 48; j++)
                {
                    float a = j * Mathf.PI * 2 / 48, f = ring / 6f;
                    var w = m.at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m.crater * .6f * f;
                    crater.Add(Local(new Vector3(w.x, floor + 3, w.y)), w / 6f, f);
                }
            crater.Quads(6, 48, wrap: true);
            Surface(group, "Massif crater lava", crater.Build("Massif crater lava " + Array.IndexOf(Massifs, m), q => Vector3.up), poolLava, false);
            Glow(group, "Massif crater glow", Local(new Vector3(m.at.x, floor + 14, m.at.y)), 110, 7, new Color(1, .4f, .12f));
            Plume(group, "Massif plume", Local(new Vector3(m.at.x, floor + 8, m.at.y)), smoke, 6, 9);
        }
        for (int i = 0; i < mountainFlows.Count; i++)
        {
            var path = mountainFlows[i];
            if (path.Count < 4) continue;
            var flow = new Builder();
            float travelled = 0;
            for (int k = 0; k < path.Count; k++)
            {
                var a = new Vector2(path[Mathf.Max(k - 1, 0)].x, path[Mathf.Max(k - 1, 0)].z);
                var b = new Vector2(path[Mathf.Min(k + 1, path.Count - 1)].x, path[Mathf.Min(k + 1, path.Count - 1)].z);
                var normal = new Vector2(-(b - a).normalized.y, (b - a).normalized.x);
                if (k > 0) travelled += Vector3.Distance(path[k], path[k - 1]);
                float half = Mathf.Lerp(4.5f, 2.2f, k / (path.Count - 1f));
                for (int s = 0; s <= 6; s++)
                {
                    float side = s / 6f * 2 - 1;
                    var q = new Vector2(path[k].x, path[k].z) + normal * side * half;
                    // Draped on the slope, crowned slightly so the flow reads as a raised tongue.
                    flow.Add(Local(new Vector3(q.x, Ground(q) + .3f + (1 - side * side) * .25f, q.y)), new Vector2(side * half / 4f, travelled / 4f), Mathf.Abs(side));
                }
            }
            flow.Quads(path.Count - 1, 6);
            var surface = Surface(group, "Mountain lava flow", flow.Build("Mountain lava flow " + i, q => Vector3.up), fallLava, false);
            Embers(group, "Mountain flow embers", surface.GetComponent<MeshRenderer>(), ember, 20);
            for (int k = 6; k < path.Count; k += 16)
                Glow(group, "Mountain flow glow", Local(path[k] + Vector3.up * 4), 30, 5, new Color(1, .36f, .08f));
        }
        // Infernal crags crest the reshaped ridges.
        var random = new System.Random(77);
        var data = terrain.terrainData;
        for (int attempt = 0, placed = 0; attempt < 1500 && placed < 34; attempt++)
        {
            var p = new Vector2(820 + (float)random.NextDouble() * 180, 420 + (float)random.NextDouble() * 560);
            float u = p.x / data.size.x, v = p.y / data.size.z, height = Ground(p), slope = data.GetSteepness(u, v);
            if (MountainLandscapeBuilder.VolcanoWeight(p.x, p.y) < .3f || height < 45 || slope < 22 || slope > 55) continue;
            if (mountainFlows.Any(f => f.Any(q => Vector2.Distance(new Vector2(q.x, q.z), p) < 10))) continue;
            float size = 12 + (float)random.NextDouble() * 14;
            Place(group, "Highland crag", "Cliff_infernal", Local(new Vector3(p.x, height + size * .25f, p.y)), new Vector3(size, size * 1.3f, size * .8f), random.Next(360));
            placed++;
        }
    }

    static ParticleSystem Plume(Transform parent, string name, Vector3 local, Material smoke, float rate, float width)
    {
        var plume = FX.Emitter(parent, name, smoke, rate, new Vector2(10, 16), new Vector2(4 * width, 7 * width), new Color(.3f, .28f, .27f, .6f), new Color(.42f, .4f, .38f, .45f));
        plume.transform.localPosition = local;
        var main = plume.main;
        main.startSpeed = new MinMaxCurve(1.5f, 3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        var shape = plume.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = width; shape.rotation = new Vector3(-90, 0, 0);
        var drift = plume.velocityOverLifetime;
        drift.enabled = true; drift.space = ParticleSystemSimulationSpace.World; drift.x = 1.2f; drift.y = .8f; drift.z = .4f;
        Grow(plume, 3f);
        FX.Spin(plume, .15f);
        var renderer = plume.GetComponent<ParticleSystemRenderer>();
        renderer.maxParticleSize = 3;
        return plume;
    }

    static void Embers(Transform parent, string name, MeshRenderer surface, Material ember, float rate)
    {
        var embers = OnSurface(FX.Emitter(parent, name, ember, rate, new Vector2(1.5f, 3f), new Vector2(.05f, .12f), new Color(1, .6f, .2f), new Color(1, .28f, .04f)), surface);
        var main = embers.main;
        main.gravityModifier = -.12f;
        main.startSpeed = new MinMaxCurve(.2f, .8f);
        main.maxParticles = 300;
        FX.Noise(embers, .7f, .5f);
    }
}
