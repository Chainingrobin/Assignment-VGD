using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CoinIcon : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        var rim = new Color(0.52f, 0.33f, 0.09f);
        // Milled rim around a lighter face, with the square hole of the yeopjeon pickups.
        Disc(mesh, rect.center, radius, rim, rim);
        Disc(mesh, rect.center, radius * 0.8f, new Color(1f, 0.92f, 0.55f), new Color(0.88f, 0.62f, 0.18f));
        Square(mesh, rect.center, radius * 0.36f, rim);
        Square(mesh, rect.center, radius * 0.24f, new Color(0.12f, 0.08f, 0.03f));
    }

    private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color inner, Color outer)
    {
        const int segments = 32;
        int start = mesh.currentVertCount;
        mesh.AddVert(center, inner, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, outer, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
    }

    private static void Square(VertexHelper mesh, Vector2 center, float half, Color color)
    {
        int start = mesh.currentVertCount;
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) })
            mesh.AddVert(center + corner * half, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
