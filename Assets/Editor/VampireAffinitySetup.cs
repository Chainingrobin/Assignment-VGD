using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VampireAffinitySetup
{
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        foreach (var element in new[]{ElementType.Fire, ElementType.Ice, ElementType.Earth, ElementType.Lightning})
        {
            string source = element == ElementType.Ice ? "Assets/Blink/Art/Textures/StylizedIceTextures/Ice_3/Ice_3.mat" :
                element == ElementType.Fire ? "Assets/Blink/Art/Textures/StylizedLavaTextures/Lava_Dry_Lava/Lava_Dry_Lava.mat" :
                element == ElementType.Earth ? "Assets/Free Realistic Textures/Textures/Rocky_Grass_17/Rocky_Grass_17.mat" :
                "Assets/Blink/Art/Textures/StylizedLavaTextures/Rock_Ground/Rock_Ground.mat";
            var original = AssetDatabase.LoadAssetAtPath<Material>(source);
            if (original == null) throw new System.InvalidOperationException(source);
            var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/" + element + " vampire skin.mat");
            skin.shader = Shader.Find("Universal Render Pipeline/Lit");
            skin.CopyPropertiesFromMaterial(original);
            var albedo = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : null;
            if (albedo == null && original.HasProperty("_MainTex")) albedo = original.GetTexture("_MainTex");
            if (albedo == null) throw new System.InvalidOperationException("Missing skin texture: " + source);
            skin.SetTexture("_BaseMap", albedo);
            if (element == ElementType.Fire || element == ElementType.Lightning)
            {
                string textureName = element == ElementType.Fire ? "Lava_Dry_Lava" : "Rock_Ground";
                skin.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Blink/Art/Textures/StylizedLavaTextures/" + textureName + "/" + textureName + "_BaseColor.png"));
            }
            skin.SetTextureScale("_BaseMap", Vector2.one);
            if (element == ElementType.Fire)
                skin.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Blink/Art/Textures/StylizedLavaTextures/Lava_Dry_Lava/Lava_Dry_Lava_Emissive.png"));
            skin.SetColor("_BaseColor", element == ElementType.Lightning ? new Color(0.7f,0.3f,1f) : Color.white);
            skin.SetFloat("_Surface", 0); skin.SetFloat("_ZWrite", 1);
            skin.SetFloat("_SrcBlend", 1); skin.SetFloat("_DstBlend", 0);
            skin.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); skin.renderQueue = 2000;
            skin.EnableKeyword("_EMISSION");
            skin.SetColor("_EmissionColor", element == ElementType.Fire ? new Color(1.2f,0.16f,0.015f) : element == ElementType.Lightning ? new Color(0.4f,0.06f,0.8f) : Color.black);
            if (element == ElementType.Lightning) skin.SetTexture("_EmissionMap", null);
            if (element == ElementType.Ice)
            {
                var snow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Blink/Art/Textures/StylizedIceTextures/Snow_4/Snow_4.mat");
                skin.SetTexture("_DetailAlbedoMap", snow.GetTexture("_BaseMap"));
                skin.SetFloat("_DetailAlbedoMapScale", 0.35f);
                skin.EnableKeyword("_DETAIL_SCALED");
                skin.SetFloat("_Smoothness", 0.8f);
            }
            EditorUtility.SetDirty(skin);
            string path = "Assets/Combat/" + element + " vampire.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (element == ElementType.Lightning)
                {
                    var visual = new SerializedObject(root.GetComponent<CombatTargetVisuals>());
                    var renderer = (Renderer)visual.FindProperty("skin").objectReferenceValue;
                    var surfaces = renderer.sharedMaterials;
                    for (int i = 0; i < surfaces.Length; i++) if (!surfaces[i].name.ToLowerInvariant().Contains("eye")) surfaces[i] = skin;
                    renderer.sharedMaterials = surfaces;
                }
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!renderer.name.Contains("Frozen")) { renderer.gameObject.SetActive(true); renderer.enabled = true; renderer.updateWhenOffscreen = true; }
                foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name.Contains("affinity aura")))
                {
                    p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=p.main; main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.065f); main.startLifetime=new ParticleSystem.MinMaxCurve(0.4f,1.1f);
                    main.startColor=ElementVisuals.ColorFor(element); main.startSpeed=0.2f;
                    var sheet=p.textureSheetAnimation; sheet.enabled=false;
                    p.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Vampire animations/Soft mote.mat");
                    var emission=p.emission; emission.rateOverTime=element==ElementType.Lightning ? 0 : 5;
                    emission.SetBursts(element==ElementType.Lightning ? new[]{new ParticleSystem.Burst(0.2f,5,5,1,0.01f)} : new ParticleSystem.Burst[0]);
                    main.duration=2.5f;
                    if(element==ElementType.Ice) p.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Cutscenes/Freeze crystal glow.mat");
                    if(element==ElementType.Earth) { main.gravityModifier=0.5f; main.startColor=new ParticleSystem.MinMaxGradient(new Color(0.25f,0.5f,0.1f),new Color(0.4f,0.3f,0.15f)); }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach(var t in Object.FindObjectsByType<CombatTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            foreach(var r in t.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if(!r.name.Contains("Frozen")) { r.gameObject.SetActive(true); r.enabled=true; }
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes();
        foreach(var t in Object.FindObjectsByType<CombatTarget>(FindObjectsSortMode.None))
        {
            var settings = new SerializedObject(t.GetComponent<CombatTargetVisuals>());
            var renderer = (Renderer)settings.FindProperty("skin").objectReferenceValue;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) throw new System.InvalidOperationException("Hidden skin: " + t.name);
        }
    }
}
