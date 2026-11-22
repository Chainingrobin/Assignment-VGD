using UnityEngine;
using UnityEngine.UI;

// A ring of charges drawn as arc segments around its centre, clockwise from the top (vector mesh,
// crisp at any size, no imported art). Each segment is one charge: a full one is drawn in the
// Graphic's colour over a faint track, and a recharging one fills along its own arc.
[RequireComponent(typeof(CanvasRenderer))]
public class ChargeRing : Graphic
{
    private int segments = 1;
    private float inner = 0.8f;   // inner radius as a fraction of the outer one
    private float gap = 6f;       // degrees between segments
    private float[] fill = { 1f };
    private static readonly Color Track = new(1f, 1f, 1f, 0.13f);

    public void Configure(int count, float innerFraction, float gapDegrees)
    {
        segments = Mathf.Max(1, count);
        inner = innerFraction;
        gap = gapDegrees;
        fill = new float[segments];
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void SetFill(int index, float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Abs(fill[index] - amount) < 0.002f) return;
        fill[index] = amount;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float step = 360f / segments, span = step - gap;
        for (int i = 0; i < segments; i++)
        {
            float start = 90f - i * step - gap * 0.5f;
            Arc(mesh, rect.center, outer * inner, outer, start, span, Track);
            if (fill[i] > 0f) Arc(mesh, rect.center, outer * inner, outer, start, span * fill[i], color);
        }
    }

    // A band between two radii, from `start` degrees sweeping `span` degrees clockwise.
    private static void Arc(VertexHelper mesh, Vector2 center, float r0, float r1, float start, float span, Color tint)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(span / 5f));
        int first = mesh.currentVertCount;
        for (int s = 0; s <= steps; s++)
        {
            float angle = (start - span * s / steps) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * r0, tint, Vector2.zero);
            mesh.AddVert(center + direction * r1, tint, Vector2.zero);
        }
        for (int s = 0; s < steps; s++)
        {
            int v = first + s * 2;
            mesh.AddTriangle(v, v + 1, v + 3);
            mesh.AddTriangle(v, v + 3, v + 2);
        }
    }
}
