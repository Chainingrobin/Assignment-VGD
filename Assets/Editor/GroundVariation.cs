using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The open ground was flat as a table. This adds gentle, natural undulation, never hills: two
// octaves of smooth noise (about ±0.4 m over ~28 m and ±0.12 m over ~8 m), only on open, gently
// sloped ground. It fades to nothing near anything that sits on the terrain (props, buildings,
// camps, enemies, water, lava, trigger zones in the World and every region scene), on painted
// trails, shores and frozen ponds, inside the guardian arenas and across the whole volcano biome,
// so nothing ends up floating or buried and the sculpted places keep their shape.
// Run it after the biome builders and before "Populate regions" (whose camps, vampires and loot
// then sit on the new ground). Re-runnable: the first run stores the heights it started from and
// every run rebuilds from them, so variation never piles up.
public static class GroundVariation
{
    private const string Baseline = "Assets/Environment/Ground variation baseline.bytes";
    private static readonly int[] PathLayers = { 2, 10, 20, 21, 22, 25 }; // cobble, gravel, dirt, dirt-stone, sand, glacier ice

    [MenuItem("Wulang/Terrain/Add gentle ground variation")]
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var regions = SceneSplitSetup.Regions.Select(r => EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(r), OpenSceneMode.Additive)).ToArray();
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        int n = data.heightmapResolution;
        var heights = LoadOrStoreBaseline(data);
        float cell = data.size.x / (n - 1);

        // Keep-out weight per height sample: 0 near placed things, 1 on open ground.
        var open = new float[n, n];
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) open[z, x] = 1f;
        const float margin = 3f, fade = 7f;
        foreach (var bounds in Footprints())
        {
            int x0 = Mathf.Max(0, (int)((bounds.min.x - margin - fade - terrain.transform.position.x) / cell));
            int x1 = Mathf.Min(n - 1, (int)((bounds.max.x + margin + fade - terrain.transform.position.x) / cell) + 1);
            int z0 = Mathf.Max(0, (int)((bounds.min.z - margin - fade - terrain.transform.position.z) / cell));
            int z1 = Mathf.Min(n - 1, (int)((bounds.max.z + margin + fade - terrain.transform.position.z) / cell) + 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(terrain.transform.position.x + x * cell, terrain.transform.position.z + z * cell);
                    float dx = Mathf.Max(bounds.min.x - p.x, 0f, p.x - bounds.max.x), dz = Mathf.Max(bounds.min.z - p.y, 0f, p.y - bounds.max.z);
                    open[z, x] = Mathf.Min(open[z, x], Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) - margin) / fade));
                }
        }

        var alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        int changed = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(terrain.transform.position.x + x * cell, terrain.transform.position.z + z * cell);
                float weight = open[z, x] * Mathf.Clamp01(1f - Arena(p)) * (Volcano(p) ? 0f : 1f);
                if (weight <= 0f) continue;
                int ax = Mathf.Clamp(Mathf.RoundToInt((float)x / (n - 1) * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt((float)z / (n - 1) * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                float path = PathLayers.Where(l => l < data.alphamapLayers).Sum(l => alpha[az, ax, l]);
                weight *= Mathf.Clamp01(1f - path * 1.5f);
                // Only gentle ground: leave slopes and cliffs as sculpted.
                float slope = data.GetSteepness((float)x / (n - 1), (float)z / (n - 1));
                weight *= Mathf.Clamp01((22f - slope) / 8f);
                if (weight <= 0f) continue;
                float metres = 0.4f * (Mathf.PerlinNoise(p.x / 28f + 31.7f, p.y / 28f + 7.3f) * 2f - 1f)
                             + 0.12f * (Mathf.PerlinNoise(p.x / 8f + 3.1f, p.y / 8f + 19.9f) * 2f - 1f);
                heights[z, x] += metres * weight / data.size.y;
                changed++;
            }
        data.SetHeights(0, 0, heights);
        terrain.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        foreach (var scene in regions) EditorSceneManager.CloseScene(scene, true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return $"Ground variation on {changed} of {n * n} height samples (open, gentle ground only).";
    }

    // Everything standing on the terrain, as world-space boxes: renderers and colliders in the World
    // and region scenes, except the terrain itself, particles, and sky-sized objects.
    private static System.Collections.Generic.IEnumerable<Bounds> Footprints()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (!(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) && !(renderer is LineRenderer) && Usable(renderer.bounds))
                        yield return renderer.bounds;
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                    if (!(collider is TerrainCollider) && Usable(collider.bounds)) yield return collider.bounds;
            }
        }
    }

    private static bool Usable(Bounds bounds) => bounds.size.x < 250f && bounds.size.z < 250f && bounds.size.sqrMagnitude > 0.0001f;

    // 1 inside a guardian arena's floor (keep it flat for the fight), fading to 0 over 10 m outside it.
    private static float Arena(Vector2 p)
    {
        float forest = 1f - Mathf.Clamp01(((p - ForestBiomeBuilder.ArenaCenter).magnitude - 30f) / 10f);
        float snow = 1f - Mathf.Clamp01(((p - SnowBiomeBuilder.ArenaCenter).magnitude - SnowBiomeBuilder.ArenaRadius - 8f) / 10f);
        return Mathf.Max(forest, snow);
    }

    // The volcano biome is sculpted by its own builder; leave it as it is.
    private static bool Volcano(Vector2 p) => p.x > 630f && p.y > 440f;

    private static float[,] LoadOrStoreBaseline(TerrainData data)
    {
        int n = data.heightmapResolution;
        if (!File.Exists(Baseline))
        {
            var current = data.GetHeights(0, 0, n, n);
            using var writer = new BinaryWriter(File.Create(Baseline));
            writer.Write(n);
            foreach (var h in current) writer.Write(h);
            AssetDatabase.ImportAsset(Baseline);
            return current;
        }
        using var reader = new BinaryReader(File.OpenRead(Baseline));
        if (reader.ReadInt32() != n) throw new System.InvalidOperationException("Ground variation baseline is for a different heightmap size; delete it to start over.");
        var heights = new float[n, n];
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) heights[z, x] = reader.ReadSingle();
        return heights;
    }
}
