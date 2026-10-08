using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CoinIcon : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 32;
        var rect = rectTransform.rect;
        mesh.AddVert(rect.center, new Color(1f, 0.83f, 0.25f), Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            mesh.AddVert(rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Min(rect.width, rect.height) * 0.48f, new Color(1f, 0.83f, 0.25f), Vector2.zero);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
        int start = mesh.currentVertCount;
        float hole = Mathf.Min(rect.width, rect.height) * 0.14f;
        foreach (var corner in new[] {new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})
            mesh.AddVert(rect.center + corner * hole, new Color(0.15f, 0.11f, 0.04f), Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2);
        mesh.AddTriangle(start,start+2,start+3);
    }
}
