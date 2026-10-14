using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class MountainLandscapeChecks
{
    public static string Run()
    {
        Assert.IsFalse(Application.isPlaying);
        var terrain = Terrain.activeTerrain; var data = terrain.terrainData;
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/New Terrain.asset");
        Assert.AreEqual(MountainLandscapeBuilder.Folder + "/Refined mountain terrain.asset", AssetDatabase.GetAssetPath(data));
        Assert.AreNotEqual(source, data); Assert.AreEqual(data, terrain.GetComponent<TerrainCollider>().terrainData);
        Assert.AreEqual(15, data.terrainLayers.Length);
        foreach (var texture in data.alphamapTextures)
            Assert.AreEqual(MountainLandscapeBuilder.Folder + "/Refined mountain terrain.asset", AssetDatabase.GetAssetPath(texture), "Terrain paint textures must be owned by the working asset.");
        int n = source.heightmapResolution, changed = 0, preserved = 0;
        var old = source.GetHeights(0,0,n,n); var actual = data.GetHeights(0,0,n,n);
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            Assert.IsTrue(actual[z,x] >= 0 && actual[z,x] <= 1);
            if (old[z,x] * source.size.y <= 38) { Assert.IsTrue(Mathf.Abs(old[z,x] - actual[z,x]) < .000001f, "Preserve valley and gameplay ground heights."); preserved++; }
            else if (Mathf.Abs(old[z,x] - actual[z,x]) > .000001f) changed++;
        }
        Assert.IsTrue(changed > 10000 && preserved > 100000);
        var paint = data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
        int ice = 0, snow = 0, basalt = 0;
        for (int z = 0; z < data.alphamapHeight; z++) for (int x = 0; x < data.alphamapWidth; x++)
        {
            float sum = 0; for (int i = 0; i < 15; i++) sum += paint[z,x,i];
            Assert.IsTrue(Mathf.Abs(sum - 1) < .025f, "Terrain painting must stay normalized within 8-bit texture rounding.");
            if (paint[z,x,12] > .25f) ice++; if (paint[z,x,5] > .35f) snow++; if (paint[z,x,14] > .5f) basalt++;
        }
        Assert.IsTrue(ice > 1000 && snow > 1000 && basalt > 1000);
        foreach (var point in new[] { new Vector3(591,0,575),new Vector3(646,0,382),new Vector3(620,0,253),new Vector3(795,0,642) })
            Assert.IsTrue(Mathf.Abs(data.GetInterpolatedHeight(point.x/1000,point.z/1000) - source.GetInterpolatedHeight(point.x/1000,point.z/1000)) < .01f, "Keep player spawn, statue, river and volcano ground intact.");
        var root = GameObject.Find("Background mountain outcrops"); Assert.IsNotNull(root);
        Assert.IsTrue(root.GetComponentsInChildren<LODGroup>().Length > 20);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>()) Assert.IsTrue(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported);
        int caps = 0, fissures = 0;
        foreach (Transform child in root.transform)
        {
            if (child.name.StartsWith("Snow crown"))
            {
                caps++; var mesh = child.GetComponent<MeshFilter>().sharedMesh; Assert.IsTrue(mesh.triangles.Length > 0);
                int index = int.Parse(child.name.Split(' ').Last()); var crag = root.transform.Find("Glacial ridge " + index);
                Assert.IsNotNull(crag);
                var sourceFilter = crag.GetComponentInChildren<MeshFilter>(); var originalVertices = sourceFilter.sharedMesh.vertices;
                var capVertices = mesh.vertices;
                Assert.AreEqual(originalVertices.Length, capVertices.Length);
                for (int i = 0; i < capVertices.Length; i++) Assert.IsTrue(Vector3.Distance(capVertices[i], sourceFilter.transform.TransformPoint(originalVertices[i])) < .06f, "Snow caps must conform to the crag rather than float above it.");
            }
            if (child.name.StartsWith("Molten mountain fissure"))
            {
                fissures++; var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                foreach(var vertex in mesh.vertices)
                {
                    var collider = terrain.GetComponent<TerrainCollider>();
                    if (collider.Raycast(new Ray(new Vector3(vertex.x,620,vertex.z),Vector3.down),out var hit,650))
                        Assert.IsTrue(vertex.y - hit.point.y >= .10f && vertex.y - hit.point.y <= .25f,"Lava fissures must fit the ground.");
                }
                Assert.IsTrue(child.GetComponentInChildren<Light>().enabled);
            }
        }
        Assert.IsTrue(caps > 0 && fissures > 3);
        // Check the authored flight clips without playing their timelines or consuming the one-time previews.
        foreach (var preview in UnityEngine.Object.FindObjectsByType<BiomePreview>(FindObjectsSortMode.None))
        {
            var timeline = (TimelineAsset)preview.GetComponent<PlayableDirector>().playableAsset;
            var animationTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().First();
            var animation = (AnimationPlayableAsset)animationTrack.GetClips().First().asset;
            var dummy = new GameObject("Mountain camera clearance check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                for (int i = 0; i < 90; i++)
                {
                    animation.clip.SampleAnimation(dummy,i/89f*animation.clip.length); var position = dummy.transform.position;
                    float ground = terrain.SampleHeight(position);
                    Assert.IsTrue(position.y - ground > 1f, "Mountain edits must not bury the biome preview flight.");
                    Assert.IsFalse(root.GetComponentsInChildren<Collider>().Any(c => c.bounds.Contains(position)), "Outcrops must not enclose preview cameras.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(dummy); }
        }
        return "PASS: " + changed + " sharpened height samples, " + preserved + " preserved low-ground samples; normalized ice/snow/basalt painting; " + caps + " fitted snow caps, " + fissures + " grounded fissures; spawn/statue/river/volcano and both preview flights preserved.";
    }
}
