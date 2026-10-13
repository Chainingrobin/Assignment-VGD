using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class LavaFlow : MonoBehaviour
{
    [SerializeField] private Vector2 speed = new Vector2(0f, 0.08f);
    private Renderer surface;
    private MaterialPropertyBlock properties;
    private Vector2 tiling;

    private void OnEnable()
    {
        surface = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        tiling = surface.sharedMaterial.GetTextureScale("_BaseMap");
    }

    private void Update()
    {
        Vector2 offset = speed * Time.time;
        surface.GetPropertyBlock(properties);
        properties.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, offset.x, offset.y));
        surface.SetPropertyBlock(properties);
    }
}
