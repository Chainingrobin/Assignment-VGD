using System.IO;
using UnityEngine;
using UnityEditor;

public static class MountainLandscapePreview
{
    public static string Render(string suffix)
    {
        Directory.CreateDirectory("Temp/MountainQA");
        var go = new GameObject("Mountain QA camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.nearClipPlane = .2f; camera.farClipPlane = 1800; camera.fieldOfView = 65;
        var target = new RenderTexture(1200,800,24); camera.targetTexture = target;
        var image = new Texture2D(1200,800,TextureFormat.RGB24,false); var active = RenderTexture.active;
        var positions = new[] { new Vector3(575,38,550), new Vector3(650,48,310), new Vector3(700,58,650), new Vector3(500,530,430) };
        var aims = new[] { new Vector3(155,135,560), new Vector3(930,115,180), new Vector3(980,135,710), new Vector3(520,5,460) };
        var names = new[] { "Default", "Ice", "Volcanic", "Overview" };
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = positions[i]; camera.transform.LookAt(aims[i]); camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1200,800),0,0); image.Apply();
                File.WriteAllBytes("Temp/MountainQA/" + names[i] + " " + suffix + ".png", image.EncodeToPNG());
            }
            return "Rendered default, ice, volcanic and overview mountain views: " + suffix;
        }
        finally
        {
            RenderTexture.active = active; camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(go);
        }
    }
}
