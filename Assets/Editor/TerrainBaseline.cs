using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// Shared by the biome builders: the first build snapshots a terrain region (heights, paint,
// grass and every tree); every later build restores that snapshot before sculpting, so a
// rebuild never compounds its own edits. Delete the .bytes file only after restoring the terrain.
public static class TerrainBaseline
{
    private const int Version = 1;

    public static RectInt Region(Rect area, int resolution, float cell)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(area.xMin / cell)), z0 = Mathf.Max(0, Mathf.FloorToInt(area.yMin / cell));
        int x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(area.xMax / cell)), z1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(area.yMax / cell));
        return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
    }

    public static float HeightCell(TerrainData data) => data.size.x / (data.heightmapResolution - 1);
    public static float AlphaCell(TerrainData data) => data.size.x / data.alphamapResolution;
    public static float DetailCell(TerrainData data) => data.size.x / data.detailResolution;

    // Restores (or first records) the region and returns the trees as they were at the snapshot.
    public static TreeInstance[] Restore(Terrain terrain, Rect area, string path)
    {
        var data = terrain.terrainData;
        var h = Region(area, data.heightmapResolution, HeightCell(data));
        var a = Region(area, data.alphamapResolution, AlphaCell(data));
        var d = Region(area, data.detailResolution, DetailCell(data));
        if (!File.Exists(path))
        {
            using var writer = new BinaryWriter(new GZipStream(File.Create(path), System.IO.Compression.CompressionLevel.Optimal));
            writer.Write(Version);
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
            }
        }
        using var reader = new BinaryReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        if (reader.ReadInt32() != Version) throw new InvalidOperationException("Delete " + path + " after restoring the terrain.");
        var heights = new float[h.height, h.width];
        for (int z = 0; z < h.height; z++) for (int x = 0; x < h.width; x++) heights[z, x] = reader.ReadSingle();
        data.SetHeights(h.x, h.y, heights);
        int layers = reader.ReadInt32();
        // Layers added after the snapshot start empty in the restored region.
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
                widthScale = reader.ReadSingle(), heightScale = reader.ReadSingle(), rotation = reader.ReadSingle(),
                prototypeIndex = reader.ReadInt32(), color = Color.white, lightmapColor = Color.white,
            };
        terrain.Flush();
        return trees;
    }
}
