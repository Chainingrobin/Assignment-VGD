using System.Linq;
using UnityEditor;
using UnityEngine;

// Character skins read as skin, not wet plastic. Every character had one material with a single,
// often high, smoothness (Maw's was 1.0), so skin mirrored the grey sky like oil. Now:
//  - where the model ships a specular map (Abe, Warrok, the vampires) the material uses URP's
//    specular workflow with that map, so the map decides what shines: skin reflects little while
//    armour, claws and eyes keep their highlights;
//  - without one (Mutant, Ganfaul) the smoothness is simply low;
//  - smoothness suits the element: earth dry and matte, fire charred, lightning a little sheen,
//    ice frosty;
//  - Maw's whole-body element glow (which flattened him) becomes his own glow map, so only his
//    runes and cracks glow.
// Re-runnable.
public static class CharacterMaterialsSetup
{
    private const string Characters = "Assets/Characters/";

    [MenuItem("Wulang/Characters/Tune skin materials")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        Tune("Assets/Combat/Earth Earth Guardian (Maw) skin.mat", 0.15f, null, Characters + "Bosses/Maw/Textures/MAW_glow.png", ElementType.Earth);
        Tune("Assets/Combat/Ice Ice Guardian (Mutant) skin.mat", 0.35f, null);
        Tune("Assets/Combat/Fire Fire Guardian (Warrok) skin.mat", 0.3f, Characters + "Bosses/Warrok/Textures/bear_specular.png");
        Tune("Assets/Combat/Lightning Final Boss (Ganfaul) skin.mat", 0.28f, null);
        var vampireSpecular = Characters + "Vampire/Vampire_specular.png";
        Tune("Assets/Combat/Earth vampire skin.mat", 0.2f, vampireSpecular);
        Tune("Assets/Combat/Fire vampire skin.mat", 0.3f, vampireSpecular);
        Tune("Assets/Combat/Lightning vampire skin.mat", 0.35f, vampireSpecular);
        Tune("Assets/Combat/Ice vampire skin.mat", 0.5f, vampireSpecular);
        Tune(AbeMaterial(), 0.3f, Characters + "Bosses/Abe/Textures/Ch39_1001_Specular.png");
        AssetDatabase.SaveAssets();
        Debug.Log("CharacterMaterialsSetup: skins tuned per element; specular maps drive the shine where models have them.");
    }

    private static void Tune(string path, float smoothness, string specular, string glow = null, ElementType element = ElementType.None)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new System.IO.FileNotFoundException(path);
        Tune(material, smoothness, specular, glow, element);
    }

    private static void Tune(Material material, float smoothness, string specular, string glow = null, ElementType element = ElementType.None)
    {
        material.SetFloat("_Metallic", 0f);
        material.SetTexture("_MetallicGlossMap", null);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        var map = specular != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(specular) : null;
        if (specular != null && map == null) throw new System.IO.FileNotFoundException(specular);
        material.SetFloat("_WorkflowMode", map != null ? 0f : 1f);
        SetKeyword(material, "_SPECULAR_SETUP", map != null);
        material.SetTexture("_SpecGlossMap", map);
        material.SetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f));
        SetKeyword(material, "_METALLICSPECGLOSSMAP", map != null);
        if (glow != null)
        {
            var glowMap = AssetDatabase.LoadAssetAtPath<Texture2D>(glow);
            if (glowMap == null) throw new System.IO.FileNotFoundException(glow);
            material.SetTexture("_EmissionMap", glowMap);
            material.SetColor("_EmissionColor", ElementVisuals.ColorFor(element) * 2f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
    }

    private static void SetKeyword(Material material, string keyword, bool on)
    {
        if (on) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    // Abe's material lives inside his FBX and can't be edited; give his prefab an editable copy.
    private static Material AbeMaterial()
    {
        const string path = "Assets/Bosses/Abe skin.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var root = PrefabUtility.LoadPrefabContents(BossSetup.AbePrefab);
        try
        {
            var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMaterial != null);
            if (material == null)
            {
                material = new Material(skin.sharedMaterial) { name = "Abe skin" };
                AssetDatabase.CreateAsset(material, path);
            }
            skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, BossSetup.AbePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return material;
    }
}
