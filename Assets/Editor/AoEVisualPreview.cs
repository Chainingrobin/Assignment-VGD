using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class AoEVisualPreview
{
    public static string Render()
    {
        var center = new Vector3(-1100, 10, -1100);
        var fixture = new GameObject("AoE visual preview"); fixture.hideFlags = HideFlags.HideAndDontSave;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.SetParent(fixture.transform);
        ground.transform.position = center - Vector3.up * .1f; ground.transform.localScale = new Vector3(28, .2f, 28);
        ground.transform.rotation = Quaternion.Euler(0, 0, 18);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", new Color(.2f,.23f,.25f));
        ground.GetComponent<Renderer>().sharedMaterial = material;
        var cameraGo = new GameObject("AoE QA camera"); cameraGo.transform.SetParent(fixture.transform);
        var camera = cameraGo.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = center + new Vector3(14, 12, -16); camera.transform.LookAt(center);
        camera.fieldOfView = 55; camera.nearClipPlane = .1f; camera.farClipPlane = 70;
        var target = new RenderTexture(1024,768,24); camera.targetTexture = target;
        var texture = new Texture2D(1024,768,TextureFormat.RGB24,false);
        var originalActive = RenderTexture.active; bool fog = RenderSettings.fog;
        GameObject ice = null;
        try
        {
            RenderSettings.fog = false; Physics.SyncTransforms();
            ice = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/Ise attack.prefab"), center, Quaternion.identity);
            ice.hideFlags = HideFlags.HideAndDontSave;
            TerrainAoE.AddTo(ice, ElementType.Ice, null, 1); SpellElementLight.AddTo(ice, ElementType.Ice, true);
            var root = ice.GetComponent<ParticleSystem>(); root.Simulate(.02f,true,true);
            var follow = ice.GetComponent<TerrainAoE>(); var glow = ice.GetComponentInChildren<SpellElementLight>();
            for (int i = 0; i < 13; i++)
            {
                root.Simulate(.02f,true,false);
                typeof(TerrainAoE).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(follow,null);
                typeof(SpellElementLight).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(glow,null);
            }
            camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1024,768),0,0); texture.Apply();
            Directory.CreateDirectory("Temp/AoEQA"); File.WriteAllBytes("Temp/AoEQA/Ice slope.png",texture.EncodeToPNG());
            return "Temp/AoEQA/Ice slope.png";
        }
        finally
        {
            RenderSettings.fog = fog; RenderTexture.active = originalActive; camera.targetTexture = null;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(material);
            if (ice != null) Object.DestroyImmediate(ice); Object.DestroyImmediate(fixture);
        }
    }
}
