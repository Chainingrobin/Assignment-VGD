using UnityEngine;
using UnityEngine.UI;

// Small vector HUD icons (heart, potion flask, quest scroll) drawn as meshes like CoinIcon and
// ElementIcon, so they stay crisp at any resolution without imported artwork.
[RequireComponent(typeof(CanvasRenderer))]
public class HUDIcon : Graphic
{
    public enum Shape { Heart, Potion, Quest }

    [SerializeField] private Shape shape;
    public Shape IconShape { get => shape; set { shape = value; SetVerticesDirty(); } }

    public static HUDIcon Create(Transform parent, Shape shape, float size, Vector2 position, Vector2 anchor)
    {
        var icon = UIKit.Rect(parent, shape + " icon", Vector2.one * size, position, anchor).gameObject.AddComponent<HUDIcon>();
        icon.shape = shape;
        icon.raycastTarget = false;
        return icon;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var r = rectTransform.rect;
        Vector2 P(float x, float y) => r.center + new Vector2(x * r.width, y * r.height) * 0.5f;
        switch (shape)
        {
            case Shape.Heart:
                var red = new Color(0.86f, 0.18f, 0.16f);
                var dark = new Color(0.45f, 0.05f, 0.05f);
                Disc(mesh, P(-0.42f, 0.32f), r.width * 0.29f, red, red);
                Disc(mesh, P(0.42f, 0.32f), r.width * 0.29f, red, red);
                Triangle(mesh, P(-0.98f, 0.2f), P(0.98f, 0.2f), P(0f, -0.95f), red, dark);
                Disc(mesh, P(-0.42f, 0.42f), r.width * 0.09f, new Color(1f, 0.7f, 0.65f), new Color(1f, 0.55f, 0.5f, 0f));
                break;
            case Shape.Potion:
                var glass = new Color(0.82f, 0.9f, 1f, 0.9f);
                var liquid = new Color(0.9f, 0.2f, 0.35f);
                Disc(mesh, P(0f, -0.3f), r.width * 0.42f, glass, glass);
                Disc(mesh, P(0f, -0.33f), r.width * 0.34f, new Color(1f, 0.45f, 0.55f), liquid);
                Quad(mesh, P(-0.18f, 0.1f), P(0.18f, 0.1f), P(0.18f, 0.72f), P(-0.18f, 0.72f), glass);
                Quad(mesh, P(-0.26f, 0.68f), P(0.26f, 0.68f), P(0.26f, 0.95f), P(-0.26f, 0.95f), new Color(0.55f, 0.36f, 0.16f));
                break;
            case Shape.Quest:
                var paper = FantasyUI.Parchment;
                var edge = new Color(0.62f, 0.45f, 0.24f);
                Quad(mesh, P(-0.62f, -0.75f), P(0.62f, -0.75f), P(0.62f, 0.75f), P(-0.62f, 0.75f), paper);
                Quad(mesh, P(-0.8f, 0.62f), P(0.8f, 0.62f), P(0.8f, 0.92f), P(-0.8f, 0.92f), edge);
                Quad(mesh, P(-0.8f, -0.92f), P(0.8f, -0.92f), P(0.8f, -0.62f), P(-0.8f, -0.62f), edge);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.35f - i * 0.3f;
                    Quad(mesh, P(-0.42f, y - 0.05f), P(0.42f, y - 0.05f), P(0.42f, y + 0.05f), P(-0.42f, y + 0.05f), edge);
                }
                break;
        }
    }

    private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color inner, Color outer)
    {
        const int segments = 28;
        int start = mesh.currentVertCount;
        mesh.AddVert(center, inner, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, outer, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
    }

    private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color top, Color bottom)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, top, Vector2.zero);
        mesh.AddVert(b, top, Vector2.zero);
        mesh.AddVert(c, bottom, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }

    private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = mesh.currentVertCount;
        foreach (var p in new[] { a, b, c, d }) mesh.AddVert(p, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
