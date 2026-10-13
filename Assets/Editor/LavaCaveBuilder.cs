using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Bakes ordinary editable meshes and prefab instances; no runtime generation.
public static class LavaCaveBuilder
{
    const string Folder = "Assets/Environment/LavaCave";
    const string Textures = "Assets/Blink/Art/Textures/StylizedLavaTextures/";
    const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    static Transform root;
    static Material cliff, ground, lava;
    static int meshIndex;

    public static string ImproveVolcanic()
    {
        const float lavafallZ = 16f;
        if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play mode before editing the volcano.");
        root = GameObject.Find("Redwood Lava Cavern")?.transform;
        var terrain = Terrain.activeTerrain;
        if (root == null || terrain == null) throw new System.InvalidOperationException("Volcano and terrain are required.");
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Improve volcanic lava and grounding");
        foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.gameObject.layer = LayerMask.NameToLayer("Ground");
        lava = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Flowing Lava.mat");
        Undo.RecordObject(lava, "Brighten lava");
        lava.SetColor("_EmissionColor", new Color(6f, 2.1f, .3f));
        lava.SetFloat("_Smoothness", .72f);
        EditorUtility.SetDirty(lava);
        var apron = root.Find("Grounded volcanic apron").GetComponent<Collider>();
        float Height(float x, float z)
        {
            var world = root.TransformPoint(new Vector3(x, 0, z));
            float h = terrain.SampleHeight(world) + terrain.transform.position.y;
            if (apron.Raycast(new Ray(new Vector3(world.x, 100, world.z), Vector3.down), out var hit, 200)) h = Mathf.Max(h, hit.point.y);
            return root.InverseTransformPoint(new Vector3(world.x, h, world.z)).y;
        }
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
        for (int trail = 0; trail < 3; trail++)
        {
            vertices.Clear(); uvs.Clear(); triangles.Clear();
            const int rows = 300, columns = 24;
            for (int i = 0; i <= rows; i++)
            {
                float t = i / (float)rows;
                float z = trail == 0 ? lavafallZ - t * (lavafallZ + 73) : -38 - t * 50;
                float x = trail == 0 ? 4 + Mathf.Sin(t * 11) * 2 : (trail == 1 ? -1 : 1) * (22 + t * 38) + Mathf.Sin(t * 15) * 3;
                float width = (trail == 0 ? 2.5f : 1.45f) * (.7f + .45f * Mathf.PerlinNoise(t * 16, trail + 2));
                width *= Mathf.SmoothStep(0, 1, (1 - t) * 15);
                for (int j = 0; j <= columns; j++)
                {
                    float side = j / (float)columns * 2 - 1;
                    float px = x + side * width;
                    float pz = z + Mathf.Pow(Mathf.Abs(side), 3) * Mathf.Sin(t * 90 + j * .4f) * .3f;
                    vertices.Add(new Vector3(px, Height(px, pz) + .055f + (1 - side * side) * .035f, pz));
                    uvs.Add(new Vector2(px / 4, pz / 4));
                    if (i < rows && j < columns) { int k = i * (columns + 1) + j; Quad(triangles, k, k + 1, k + columns + 2, k + columns + 1, false); }
                }
            }
            LavaSurface("Grounded lava trail " + trail, vertices, uvs, triangles, lava);
        }
        // Closed cross-sections give the falling lava depth from every viewing angle.
        vertices.Clear(); uvs.Clear(); triangles.Clear();
        const int rings = 80, sides = 32;
        float bottom = Height(4, lavafallZ) + .09f;
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= sides; j++)
        {
            float t = i / (float)rings, a = j * Mathf.PI * 2 / sides;
            float ripple = 1 + .08f * Mathf.Sin(t * 42 + a * 3);
            vertices.Add(new Vector3(4 + Mathf.Cos(a) * (4.2f + .5f * (1 - t)) * ripple,
                Mathf.Lerp(bottom, 39, t), lavafallZ + Mathf.Sin(a) * 1.8f * ripple + Mathf.Sin(t * 9) * .4f));
            uvs.Add(new Vector2(j / (float)sides * 3, t * 10));
            if (i < rings && j < sides) { int k = i * (sides + 1) + j; Quad(triangles, k, k + 1, k + sides + 2, k + sides + 1, true); }
        }
        LavaSurface("High volcanic lavafall", vertices, uvs, triangles, lava);
        var poolMaterials = new Material[3];
        for (int i = 0; i < poolMaterials.Length; i++)
        {
            string path = Folder + "/Lava pool " + i + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(lava); AssetDatabase.CreateAsset(mat, path); }
            Undo.RecordObject(mat, "Vary lava pool warmth");
            mat.SetColor("_EmissionColor", new Color(5 + i, 1.4f + i * .65f, .12f + i * .12f));
            mat.SetFloat("_Smoothness", .65f + i * .12f); EditorUtility.SetDirty(mat); poolMaterials[i] = mat;
        }
        for (int pool = 0; pool < 10; pool++)
        {
            float x = pool == 0 || pool == 9 ? 4 : (pool % 2 == 0 ? 1 : -1) * (12 + pool * 4.7f);
            float z = pool == 0 || pool == 9 ? lavafallZ : -24 - pool * 6.1f;
            float rx = pool == 0 || pool == 9 ? 6 : 2.4f + pool % 3 * 1.2f, rz = pool == 0 || pool == 9 ? 4 : 2 + pool % 4 * .8f;
            vertices.Clear(); uvs.Clear(); triangles.Clear();
            const int radial = 24, segments = 96;
            for (int r = 0; r <= radial; r++)
            for (int j = 0; j <= segments; j++)
            {
                float a = j * Mathf.PI * 2 / segments, f = r / (float)radial;
                float edge = 1 + .16f * Mathf.Sin(a * 3 + pool) + .1f * Mathf.Sin(a * 7 + pool * 2);
                float px = x + Mathf.Cos(a) * rx * f * edge, pz = z + Mathf.Sin(a) * rz * f * edge;
                float y = pool == 9 ? 39.03f : Height(px, pz) + .06f + (1 - f * f) * .05f;
                vertices.Add(new Vector3(px, y, pz)); uvs.Add(new Vector2(px / 4, pz / 4));
                if (r < radial && j < segments) { int k = r * (segments + 1) + j; Quad(triangles, k, k + segments + 1, k + segments + 2, k + 1, true); }
            }
            LavaSurface(pool == 9 ? "Lavafall source pool" : "Liquid lava pool " + pool, vertices, uvs, triangles, poolMaterials[pool % 3]);
            string lightName = "Pool glow " + pool;
            var glow = root.Find(lightName);
            if (glow == null) { glow = new GameObject(lightName).transform; glow.SetParent(root, false); Undo.RegisterCreatedObjectUndo(glow.gameObject, "Add pool glow"); glow.gameObject.AddComponent<Light>(); }
            glow.localPosition = new Vector3(x, pool == 9 ? 41 : Height(x, z) + 1.8f, z);
            var light = glow.GetComponent<Light>(); light.color = new Color(1, .25f + pool % 3 * .08f, .045f); light.range = 11 + pool % 3 * 2; light.intensity = 28 + pool % 3 * 6;
        }
        foreach (var light in root.GetComponentsInChildren<Light>())
        {
            if (light.name == "Lava radiance") { light.intensity = 32; light.range = 18; }
            if (light.name.StartsWith("Lavafall radiance")) { light.intensity = 48; light.range = 26; light.transform.localPosition = new Vector3(4, light.transform.localPosition.y, lavafallZ - 3); }
        }
        var smoke = root.Find("Summit smoke");
        if (smoke == null) { smoke = new GameObject("Summit smoke").transform; smoke.SetParent(root, false); Undo.RegisterCreatedObjectUndo(smoke.gameObject, "Add summit smoke"); smoke.gameObject.AddComponent<ParticleSystem>(); }
        smoke.localPosition = new Vector3(0, 64, 0);
        var ps = smoke.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.duration = 12; main.startLifetime = new ParticleSystem.MinMaxCurve(9, 15); main.startSpeed = new ParticleSystem.MinMaxCurve(2, 4); main.startSize = new ParticleSystem.MinMaxCurve(4, 7); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.maxParticles = 250; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.rateOverTime = 12;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = 3.5f; shape.angle = 12; shape.rotation = new Vector3(-90, 0, 0);
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.x = .7f; velocity.y = .6f; velocity.z = .25f;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.6f));
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(new Color(.18f,.15f,.14f),0), new GradientColorKey(new Color(.38f,.37f,.36f),1) }, new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(.55f,.15f), new GradientAlphaKey(.3f,.65f), new GradientAlphaKey(0,1) }); color.color = gradient;
        string smokePath = Folder + "/Summit smoke.mat";
        var smokeMat = AssetDatabase.LoadAssetAtPath<Material>(smokePath);
        if (smokeMat == null) { smokeMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(smokeMat, smokePath); }
        smokeMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hovl Studio/AOE Magic spells Vol.1/Textures/Smoke3.png"));
        smokeMat.SetFloat("_Surface", 1); smokeMat.SetFloat("_Blend", 0); smokeMat.SetFloat("_SrcBlend", 5); smokeMat.SetFloat("_DstBlend", 10); smokeMat.SetFloat("_ZWrite", 0); smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); smokeMat.SetOverrideTag("RenderType", "Transparent"); smokeMat.renderQueue = 3000;
        EditorUtility.SetDirty(smokeMat); ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
        ps.Play();
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveScene(root.gameObject.scene);
        return "Ground layers fixed; three conforming streams, nine irregular pools, wide tubular lavafall and summit smoke saved.";
    }

    static void LavaSurface(string name, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Material material)
    {
        string path = Folder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        else { Undo.RecordObject(mesh, "Reshape lava"); mesh.Clear(); }
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        UnityEngine.Assertions.Assert.IsTrue(mesh.bounds.size.x > 0 && mesh.bounds.size.z > 0, "Lava surface must have width and depth.");
        EditorUtility.SetDirty(mesh);
        var surface = root.Find(name);
        if (surface == null) { surface = new GameObject(name).transform; surface.SetParent(root, false); Undo.RegisterCreatedObjectUndo(surface.gameObject, "Add lava surface"); surface.gameObject.AddComponent<MeshFilter>(); surface.gameObject.AddComponent<MeshRenderer>(); surface.gameObject.AddComponent<LavaFlow>(); }
        surface.localPosition = Vector3.zero; surface.localRotation = Quaternion.identity; surface.localScale = Vector3.one;
        surface.GetComponent<MeshFilter>().sharedMesh = mesh; surface.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public static string RebuildVolcanic()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Leave Play mode before rebuilding.");
        var old=GameObject.Find("Redwood Lava Cavern");
        if(old==null) throw new System.InvalidOperationException("Existing cavern is required.");
        Undo.DestroyObjectImmediate(old);
        root=new GameObject("Redwood Lava Cavern").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Reshape volcanic cavern");
        root.position=new Vector3(795,0,642);
        // Local south now faces west, toward the armory.
        root.rotation=Quaternion.Euler(0,90,0);
        cliff=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cave Basalt.mat");
        ground=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cave Floor.mat");
        lava=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Flowing Lava.mat");
        lava.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
        lava.EnableKeyword("_EMISSION");lava.SetColor("_EmissionColor",new Color(4,1.5f,.25f));EditorUtility.SetDirty(lava);
        meshIndex=100;
        var terrain=Terrain.activeTerrain;
        float Height(float x,float z) {var p=root.TransformPoint(new Vector3(x,0,z));return terrain.SampleHeight(p)+terrain.transform.position.y;}
        var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        // An irregular, tapered mountain with a tall inner chamber; the low western fissure is left open.
        const int sectors=40,levels=6;
        float[] radii={49,41,34,27,19,10,1.5f};
        float[] heights={-2,13,26,39,49,59,65};
        for(int l=0;l<=levels;l++)for(int a=0;a<=sectors;a++)
        {
            float angle=a*2*Mathf.PI/sectors;
            float jitter=Mathf.PerlinNoise(a*.77f,l*.81f)*5;
            float radius=radii[l]+jitter;
            float x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius;
            float y=l==0?Height(x,z)-2:heights[l]+Mathf.Sin(a*1.9f+l)*2.3f;
            v.Add(new Vector3(x,y,z));uv.Add(new Vector2(a*.7f,l*1.6f));
            if(l<levels && a<sectors)
            {
                bool doorway=l==0 && a>=27 && a<=32;
                if(!doorway){int i=l*(sectors+1)+a;Quad(triangles,i,i+1,i+sectors+2,i+sectors+1,false);}
            }
        }
        MeshObject("Jagged volcanic mountain",v,uv,triangles,cliff,true);
        // The entire floor follows the original ground, eliminating a raised floating slab.
        v.Clear();uv.Clear();triangles.Clear();
        for(int z=0;z<=50;z++)for(int x=0;x<=50;x++)
        {
            float px=-50+x*2,pz=-50+z*2;
            v.Add(new Vector3(px,Height(px,pz)+.025f,pz));uv.Add(new Vector2(px/7,pz/7));
            if(x<50&&z<50){int i=z*51+x;Quad(triangles,i,i+51,i+52,i+1,false);}
        }
        MeshObject("Grounded volcanic apron",v,uv,triangles,ground,true);
        // Angular buttresses and talus replace the regular semicircular portal.
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.PI*2/24;
            if(i>=17&&i<=20)continue;
            float x=Mathf.Cos(angle)*43,z=Mathf.Sin(angle)*43;
            float h=18+(i%5)*5;
            Rock("Mountain buttress "+i,"Cliff_basic",new Vector3(x,Height(x,z)+h*.35f,z),new Vector3(18,h,20),i*37);
        }
        Rock("Broken left portal","Cliff_basic",new Vector3(-17,12,-39),new Vector3(12,29,19),-17);
        Rock("Broken right portal","Rock_basic",new Vector3(17,9,-41),new Vector3(14,23,17),31);
        Rock("Slanted portal lintel","Cliff_basic",new Vector3(0,19,-39),new Vector3(34,10,17),11);
        for(int i=0;i<12;i++)
        {
            float a=i*2.4f, radius=8+i%3*7;
            Rock("Summit crag "+i,"Cliff_basic",new Vector3(Mathf.Cos(a)*radius,48-i%3*4,Mathf.Sin(a)*radius),new Vector3(13,26+i%4*3,15),i*51);
        }
        for(int i=0;i<52;i++)
        {
            float a=i*2.39996f,radius=52+(i%7)*4;
            float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius;
            if(z<-35&&Mathf.Abs(x)<15)continue;
            float h=2+i%6*1.4f;
            Rock("Grounded volcanic debris "+i,"Rock_basic",new Vector3(x,Height(x,z)+h*.3f,z),new Vector3(4+i%4,h,5+i%5),i*71);
        }
        // A winding ascent with generous shelves at three elevations, all within jump height.
        float start=Height(-21,-24)+.6f;
        for(int i=0;i<38;i++)
        {
            float a=-2.28f-i*.115f,radius=31-i*.18f;
            float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius,top=start+i*.7f;
            var rock=Rock("Ascent rock "+(i+1),"Plate_basic",new Vector3(x,top-.7f,z),new Vector3(6,1.6f,4.6f),-a*Mathf.Rad2Deg);
            foreach(var c in rock.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            var landing=Box("Ascent landing "+(i+1),new Vector3(x,top-.18f,z),new Vector3(4.8f,.36f,3.2f),ground);
            landing.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
            if(i==11||i==23||i==37)
            {
                var shelf=Rock("Exploration shelf "+i,"Plate_basic",new Vector3(x*.89f,top-1,z*.89f),new Vector3(12,2,10),i*7);
                foreach(var c in shelf.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                Box("Shelf landing "+i,new Vector3(x*.89f,top-.3f,z*.89f),new Vector3(9,.6f,8),ground);
            }
        }
        // Continuous waterfall from the high vent to the terrain, then a ground-hugging outflow.
        v.Clear();uv.Clear();triangles.Clear();
        float bottom=Height(4,17)+.035f;
        for(int i=0;i<=36;i++)
        {
            float y=Mathf.Lerp(bottom,39,i/36f),z=17+Mathf.Sin(i*.18f)*.5f;
            v.Add(new Vector3(1+Mathf.Sin(i*.7f)*.3f,y,z));v.Add(new Vector3(7+Mathf.Sin(i*.9f)*.4f,y,z));
            uv.Add(new Vector2(0,i*.3f));uv.Add(new Vector2(1,i*.3f));if(i<36)Quad(triangles,i*2,i*2+1,i*2+3,i*2+2,false);
        }
        MeshObject("High volcanic lavafall",v,uv,triangles,lava,false).AddComponent<LavaFlow>();
        Rock("Lavafall basalt spine","Cliff_basic",new Vector3(4,19,23),new Vector3(13,44,11),0);
        for(int trail=0;trail<3;trail++)
        {
            v.Clear();uv.Clear();triangles.Clear();
            for(int i=0;i<=100;i++)
            {
                float z=trail==0?17-i*.9f:-38-i*.5f;
                float x=trail==0?4+Mathf.Sin(i*.11f)*2:(trail==1?-1:1)*(22+i*.38f)+Mathf.Sin(i*.15f)*3;
                float width=trail==0?2.1f:1.1f;
                for(int side=-1;side<=1;side+=2){float px=x+side*width;v.Add(new Vector3(px,Height(px,z)+.045f,z));uv.Add(new Vector2(side<0?0:1,i*.16f));}
                if(i<100)Quad(triangles,i*2,i*2+2,i*2+3,i*2+1,false);
                if(i%25==0){var g=new GameObject("Lava radiance");g.transform.SetParent(root,false);g.transform.localPosition=new Vector3(x,Height(x,z)+2,z);var light=g.AddComponent<Light>();light.color=new Color(1,.22f,.025f);light.range=15;light.intensity=22;}
            }
            MeshObject("Grounded lava trail "+trail,v,uv,triangles,lava,false).AddComponent<LavaFlow>();
        }
        for(int i=0;i<3;i++){var g=new GameObject("Lavafall radiance "+i);g.transform.SetParent(root,false);g.transform.localPosition=new Vector3(3,9+i*12,14);var light=g.AddComponent<Light>();light.color=new Color(1,.32f,.06f);light.range=24;light.intensity=35;}
        var data=terrain.terrainData;Undo.RegisterCompleteObjectUndo(data,"Clear volcanic footprint");
        data.treeInstances=data.treeInstances.Where(t=>{var p=root.InverseTransformPoint(Vector3.Scale(t.position,data.size)+terrain.transform.position);return p.x*p.x+p.z*p.z>56*56;}).ToArray();
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.gameObject.scene);
        Selection.activeGameObject=root.gameObject;SceneView.lastActiveSceneView?.FrameSelected();
        return "Volcanic cave rebuilt: 65m summit, west-facing fissure, 38 ascent rocks, 3 exploration shelves and terrain-conforming lava.";
    }

    public static string Build()
    {
        if (Application.isPlaying || GameObject.Find("Redwood Lava Cavern") != null)
            throw new System.InvalidOperationException("Build only in Edit mode with no existing Redwood Lava Cavern.");
        System.IO.Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var terrain = Terrain.activeTerrain;
        float baseY = 0f;
        for (float x = 747; x <= 813; x += 2)
            for (float z = 603; z <= 679; z += 2)
                baseY = Mathf.Max(baseY, terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y);
        baseY += 0.4f;
        root = new GameObject("Redwood Lava Cavern").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create lava cavern");
        root.position = new Vector3(780, baseY, 610);
        cliff = Material("Cave Basalt", "Dry_Lava", 0.22f);
        ground = Material("Cave Floor", "Rock_Ground", 0.12f);
        lava = Material("Flowing Lava", "Lava", 0.55f);
        lava.SetTexture("_EmissionMap", Texture("Lava", "Emissive"));
        lava.SetColor("_EmissionColor", new Color(2.3f, 1.1f, 0.45f));
        lava.EnableKeyword("_EMISSION");
        lava.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;

        // Double-walled arch with a closed back and an open southern entrance.
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        int rings = 13, slices = 18;
        for (int side = 0; side < 2; side++)
        for (int z = 0; z <= rings; z++)
        for (int a = 0; a <= slices; a++)
        {
            float theta = a * Mathf.PI / slices;
            float depth = z * 5f;
            float width = Mathf.Lerp(14, 25, Mathf.SmoothStep(0, 1, depth / 18f));
            float irregular = Mathf.PerlinNoise(a * 0.63f, z * 0.71f) * 2.6f;
            float radius = width + irregular + side * 4f;
            var p = new Vector3(Mathf.Cos(theta) * radius,
                Mathf.Sin(theta) * (17 + irregular + side * 4f) - 0.35f, depth);
            v.Add(p); uv.Add(new Vector2(a * 0.7f, depth / 8));
            if (z < rings && a < slices)
            {
                int i = side * (rings + 1) * (slices + 1) + z * (slices + 1) + a;
                Quad(tris, i, i + 1, i + slices + 2, i + slices + 1, side == 0);
            }
        }
        int outer = (rings + 1) * (slices + 1);
        for (int a = 0; a < slices; a++) Quad(tris, a, outer + a, outer + a + 1, a + 1, false);
        MeshObject("Basalt cavern shell", v, uv, tris, cliff, true);
        Rock("Rear cliff", "Cliff_basic", new Vector3(0, 10, 65), new Vector3(58, 25, 10), 0);
        // Broad solid floor, with a gently sloped entrance connecting to existing ground.
        Box("Exploration floor", new Vector3(0, -0.55f, 32), new Vector3(54, 1, 66), ground);
        var rv = new List<Vector3>(); var ru = new List<Vector2>(); var rt = new List<int>();
        for (int z = 0; z <= 12; z++)
        for (int x = 0; x <= 8; x++)
        {
            float lx = (x - 4) * 3.5f, lz = -24 + z * 2;
            float h = terrain.SampleHeight(root.position + new Vector3(lx, 0, lz)) - baseY + 0.1f;
            rv.Add(new Vector3(lx, Mathf.Lerp(h, 0, z / 12f), lz)); ru.Add(new Vector2(lx / 7, lz / 7));
            if (z < 12 && x < 8) {int i=z*9+x; Quad(rt,i,i+9,i+10,i+1,false);}
        }
        MeshObject("Rocky entrance slope", rv, ru, rt, ground, true);
        // Prefab rock faces break the silhouette and disguise the shell's joins.
        for (int i = 0; i < 12; i++)
        {
            float z = 5 + i * 5;
            float x = i < 3 ? 15 + i * 3 : 26;
            Rock("West wall outcrop " + i, "Cliff_basic", new Vector3(i == 0 ? -23 : -x, 5, z), new Vector3(9, 14 + i % 3, 10), i * 17);
            Rock("East wall outcrop " + i, "Rock_basic", new Vector3(x, 5, z), new Vector3(10, 13, 11), i * 31);
        }
        for (int i = 0; i < 9; i++)
            Rock("Roof crown " + i, "Rock_basic", new Vector3(Mathf.Sin(i * 2) * 9, 20, 5 + i * 7), new Vector3(22, 10, 16), i * 39);
        // Wide, level landing tops: 0.7 m rises fit the player's 1.2 m jump.
        for (int i = 0; i < 11; i++)
        {
            float top = 0.7f * (i + 1), z = 9 + i * 3.8f;
            var g = Rock("Jump ledge " + (i + 1), "Plate_basic", new Vector3(-16, top - 0.65f, z), new Vector3(6, 1.3f, 3), i * 13);
            foreach(var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Box("Ledge landing " + (i + 1), new Vector3(-16, top - 0.18f, z), new Vector3(4.8f, .36f, 2.8f), ground);
            Rock("Ledge support " + i, "Rock_basic", new Vector3(-18, top / 2 - 1, z), new Vector3(7, Mathf.Max(2,top), 3.1f), 0);
        }
        Box("Waterfall overlook", new Vector3(-13, 7.5f, 53), new Vector3(12, 1, 9), ground);
        Rock("Overlook rock base", "Cliff_basic", new Vector3(-14, 3, 55), new Vector3(16, 9, 12), 15);
        // Lava snakes down the middle; both banks stay wide enough for exploration.
        v.Clear(); uv.Clear(); tris.Clear();
        for (int i = 0; i <= 26; i++)
        {
            float z=6+i*2, center=3+Mathf.Sin(i*.25f)*3, width=3+Mathf.Sin(i*.7f)*.6f;
            v.Add(new Vector3(center-width,.08f,z)); v.Add(new Vector3(center+width,.08f,z));
            uv.Add(new Vector2(0,i*.35f)); uv.Add(new Vector2(1,i*.35f));
            if(i<26) Quad(tris,i*2,i*2+2,i*2+3,i*2+1,false);
        }
        MeshObject("Flowing lava channel",v,uv,tris,lava,false).AddComponent<LavaFlow>();
        for(int i=0;i<13;i++)
        {
            float z=6+i*4, center=3+Mathf.Sin(i*.5f)*3;
            for(int side=-1;side<=1;side+=2)
                Rock("Lava bank", "Rock_basic",new Vector3(center+side*4,-.05f,z),new Vector3(2.4f,1.1f,4.5f),i*47);
        }
        // Ribbon UVs scroll down the fall and continue into the channel.
        v.Clear();uv.Clear();tris.Clear();
        for(int i=0;i<=16;i++)
        {
            float y=i*.75f, z=58+Mathf.Sin(i*.2f)*.5f;
            v.Add(new Vector3(.2f+Mathf.Sin(i*1.7f)*.3f,y+.15f,z));v.Add(new Vector3(6.5f+Mathf.Sin(i*1.2f)*.35f,y+.15f,z));
            uv.Add(new Vector2(0,i*.22f));uv.Add(new Vector2(1,i*.22f));
            if(i<16)Quad(tris,i*2,i*2+1,i*2+3,i*2+2,false);
        }
        MeshObject("Lava waterfall",v,uv,tris,lava,false).AddComponent<LavaFlow>();
        Rock("Waterfall basalt face","Cliff_basic",new Vector3(3.5f,6,61),new Vector3(13,15,5),0);
        Box("Lava source",new Vector3(3.3f,12.1f,60),new Vector3(6.4f,.15f,4),lava).AddComponent<LavaFlow>();
        for(int i=0;i<6;i++)
        {
            var g=new GameObject("Lava glow "+i);g.transform.SetParent(root,false);
            g.transform.localPosition=new Vector3(3,3,10+i*9);
            var light=g.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.24f,.055f);
            light.range=19;light.intensity=25;light.shadows=i==5?LightShadows.Soft:LightShadows.None;
        }
        var fill=new GameObject("Overlook warm light");fill.transform.SetParent(root,false);fill.transform.localPosition=new Vector3(-13,11,51);
        var fl=fill.AddComponent<Light>();fl.type=LightType.Point;fl.color=new Color(1,.48f,.2f);fl.range=18;fl.intensity=3;

        // Brush-equivalent local terrain painting, clearing only vegetation under the new cave.
        var data=terrain.terrainData;Undo.RegisterCompleteObjectUndo(data,"Paint cavern footprint");
        var trees=data.treeInstances; data.treeInstances=trees.Where(t=>{var p=Vector3.Scale(t.position,data.size)+terrain.transform.position-root.position;return !(Mathf.Abs(p.x)<33 && p.z>-25 && p.z<72);}).ToArray();
        var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(Textures+"Rock_Ground/Rock_Ground.terrainlayer");
        var layers=data.terrainLayers.ToList();int index=layers.IndexOf(layer);if(index<0){index=layers.Count;layers.Add(layer);data.terrainLayers=layers.ToArray();}
        int ax=Mathf.FloorToInt(744/data.size.x*data.alphamapWidth),az=Mathf.FloorToInt(582/data.size.z*data.alphamapHeight);
        int aw=Mathf.CeilToInt(72/data.size.x*data.alphamapWidth),ah=Mathf.CeilToInt(105/data.size.z*data.alphamapHeight);
        var maps=data.GetAlphamaps(ax,az,aw,ah);
        for(int z=0;z<ah;z++)for(int x=0;x<aw;x++)
        {
            float edge=Mathf.Min(Mathf.Min(x,aw-1-x),Mathf.Min(z,ah-1-z));float blend=Mathf.SmoothStep(0,1,edge/5);
            for(int l=0;l<layers.Count;l++)maps[z,x,l]=Mathf.Lerp(maps[z,x,l],l==index?1:0,blend);
        }
        data.SetAlphamaps(ax,az,maps);EditorUtility.SetDirty(data);
        Refine();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);
        Selection.activeGameObject=root.gameObject;SceneView.lastActiveSceneView?.FrameSelected();
        return "Created cavern at "+root.position+"; removed "+(trees.Length-data.treeInstanceCount)+" overlapping vegetation instances.";
    }

    public static void Refine()
    {
        root=GameObject.Find("Redwood Lava Cavern").transform;
        cliff=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cave Basalt.mat");
        cliff.SetTexture("_BaseMap",Texture("Dry_Lava","BaseColor"));
        cliff.SetTexture("_BumpMap",Texture("Dry_Lava","Normal"));
        cliff.SetTexture("_OcclusionMap",Texture("Dry_Lava","AmbientOcclusion"));
        EditorUtility.SetDirty(cliff);
        lava=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Flowing Lava.mat");
        lava.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
        lava.EnableKeyword("_EMISSION");lava.SetColor("_BaseColor",new Color(1,.45f,.12f));
        EditorUtility.SetDirty(lava);
        for(int i=0;i<9;i++)
        {
            float a=i*Mathf.PI/8;
            Rock("Entrance arch boulder "+i,"Rock_basic",new Vector3(Mathf.Cos(a)*17,Mathf.Sin(a)*19,0),new Vector3(9,8,8),i*29);
        }
        // Match floor UV density to the entrance instead of stretching one tile over 65 metres.
        foreach(var name in new[]{"Exploration floor","Waterfall overlook"})
        {
            var g=root.Find(name).gameObject;var mesh=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++) {var p=Vector3.Scale(vertices[i],g.transform.localScale);uv[i]=Mathf.Abs(normals[i].y)>.5f?new Vector2(p.x,p.z)/7:new Vector2(p.x+p.z,p.y)/7;}
            mesh.uv=uv;AssetDatabase.CreateAsset(mesh,Folder+"/"+name+" mesh.asset");g.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        var data=Terrain.activeTerrain.terrainData;
        int x0=Mathf.FloorToInt(747/data.size.x*data.detailWidth),z0=Mathf.FloorToInt(585/data.size.z*data.detailHeight);
        int w=Mathf.CeilToInt(66/data.size.x*data.detailWidth),h=Mathf.CeilToInt(95/data.size.z*data.detailHeight);
        for(int i=0;i<data.detailPrototypes.Length;i++)data.SetDetailLayer(x0,z0,i,new int[h,w]);
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.gameObject.scene);
    }

    static Texture2D Texture(string set,string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+set+"/"+set+"_"+suffix+".png");
    static Material Material(string name,string set,float smooth)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        m.SetTexture("_BaseMap",Texture(set,"BaseColor"));m.SetTexture("_BumpMap",Texture(set,"Normal"));m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_OcclusionMap",Texture(set,"AmbientOcclusion"));m.EnableKeyword("_OCCLUSIONMAP");m.SetFloat("_Smoothness",smooth);
        m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    static void Quad(List<int> t,int a,int b,int c,int d,bool flip)
    {if(flip){t.AddRange(new[]{a,c,b,a,d,c});}else t.AddRange(new[]{a,b,c,a,c,d});}
    static GameObject MeshObject(string name,List<Vector3> v,List<Vector2> uv,List<int> t,Material mat,bool solid)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/CaveMesh"+(meshIndex++)+".asset");
        var g=new GameObject(name);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(solid){g.AddComponent<MeshCollider>().sharedMesh=mesh;g.layer=LayerMask.NameToLayer("Ground");}return g;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.layer=LayerMask.NameToLayer("Ground");g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
    static GameObject Rock(string name,string prefab,Vector3 p,Vector3 size,float yaw)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rocks+prefab+".prefab"),root);
        g.name=name;g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.identity;
        var renderers=g.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.sharedMaterial=cliff;}
        g.transform.localScale=Vector3.Scale(g.transform.localScale,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        g.transform.position+=root.TransformPoint(p)-bounds.center;g.transform.Rotate(0,yaw,0,Space.Self);
        if(g.GetComponentsInChildren<Collider>().Length==0){var mf=g.GetComponentInChildren<MeshFilter>();mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;}
        foreach(var collider in g.GetComponentsInChildren<Collider>())collider.gameObject.layer=LayerMask.NameToLayer("Ground");
        return g;
    }
}
