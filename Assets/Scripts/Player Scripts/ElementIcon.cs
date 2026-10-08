using UnityEngine;

// Small vector icons stay crisp without adding imported artwork.
[RequireComponent(typeof(CanvasRenderer))]
public class ElementIcon : UnityEngine.UI.Graphic
{
    private ElementType element;
    public ElementType Element
    {
        get => element;
        set { element = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        switch (element)
        {
            case ElementType.Ice:
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var perpendicular = new Vector2(-direction.y, direction.x);
                    Line(mesh, Vector2.zero, direction * 0.92f, 0.09f, color);
                    Line(mesh, direction * 0.55f, direction * 0.76f + perpendicular * 0.22f, 0.08f, color);
                    Line(mesh, direction * 0.55f, direction * 0.76f - perpendicular * 0.22f, 0.08f, color);
                }
                break;
            case ElementType.Lightning:
                var bolt = new[] { new Vector2(0.2f, 0.95f), new Vector2(-0.55f, -0.08f), new Vector2(-0.08f, -0.08f), new Vector2(-0.2f, -0.95f), new Vector2(0.55f, 0.16f), new Vector2(0.08f, 0.16f) };
                foreach (var point in bolt) Vertex(mesh, point, color);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(0, 2, 5);
                mesh.AddTriangle(2, 3, 4);
                mesh.AddTriangle(2, 4, 5);
                break;
            case ElementType.Fire:
                Fan(mesh, new[] { new Vector2(0f, 0.95f), new Vector2(0.22f, 0.3f), new Vector2(0.47f, 0.5f), new Vector2(0.7f, -0.28f), new Vector2(0.38f, -0.8f), new Vector2(0f, -0.95f), new Vector2(-0.5f, -0.7f), new Vector2(-0.65f, -0.15f), new Vector2(-0.3f, 0.4f), new Vector2(-0.2f, 0.1f) });
                break;
            case ElementType.Earth:
                Fan(mesh, new[] { new Vector2(0.72f, 0.9f), new Vector2(0.8f, 0.2f), new Vector2(0.45f, -0.5f), new Vector2(-0.25f, -0.65f), new Vector2(-0.6f, -0.25f), new Vector2(-0.55f, 0.35f), new Vector2(0f, 0.72f) });
                Line(mesh, new Vector2(-0.55f, -0.8f), new Vector2(0.5f, 0.62f), 0.09f, new Color(0.04f, 0.12f, 0.07f, color.a));
                break;
        }
    }

    private void Vertex(UnityEngine.UI.VertexHelper mesh, Vector2 point, Color tint)
    {
        var rect = rectTransform.rect;
        var vertex = UnityEngine.UIVertex.simpleVert;
        vertex.position = rect.center + Vector2.Scale(point, rect.size * 0.5f);
        vertex.color = tint;
        mesh.AddVert(vertex);
    }

    private void Fan(UnityEngine.UI.VertexHelper mesh, Vector2[] points)
    {
        int start = mesh.currentVertCount;
        Vertex(mesh, Vector2.zero, color);
        foreach (var point in points) Vertex(mesh, point, color);
        for (int i = 0; i < points.Length; i++)
            mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
    }

    private void Line(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 delta = (to - from).normalized;
        Vector2 offset = new Vector2(-delta.y, delta.x) * width * 0.5f;
        int start = mesh.currentVertCount;
        Vertex(mesh, from - offset, tint);
        Vertex(mesh, from + offset, tint);
        Vertex(mesh, to + offset, tint);
        Vertex(mesh, to - offset, tint);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
