using UnityEditor;
using UnityEngine;

// The Editor's zoom crops the whole game image, including correctly anchored HUDs.
[InitializeOnLoad]
public static class GameViewAutoFit
{
    private static Rect lastWindow;
    private static Vector2 lastTarget;
    private static bool lastMaximized;
    static GameViewAutoFit() => EditorApplication.update += Update;

    private static void Update()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window.GetType().Name != "GameView") continue;
            var serialized = new SerializedObject(window);
            var target = serialized.FindProperty("m_TargetSize");
            var area = serialized.FindProperty("m_ZoomArea.m_DrawArea");
            var scale = serialized.FindProperty("m_ZoomArea.m_Scale");
            var translation = serialized.FindProperty("m_ZoomArea.m_Translation");
            if (target == null || area == null || scale == null || translation == null) return;
            if (lastWindow == window.position && lastTarget == target.vector2Value && lastMaximized == window.maximized) return;
            var size = target.vector2Value;
            var viewport = area.rectValue;
            if (size.x <= 0f || size.y <= 0f || viewport.width <= 0f || viewport.height <= 0f) return;
            float fit = Mathf.Min(viewport.width * EditorGUIUtility.pixelsPerPoint / size.x, viewport.height * EditorGUIUtility.pixelsPerPoint / size.y);
            scale.vector2Value = Vector2.one * fit;
            translation.vector2Value = viewport.size * 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            lastWindow = window.position;
            lastTarget = size;
            lastMaximized = window.maximized;
            window.Repaint();
        }
    }
}
