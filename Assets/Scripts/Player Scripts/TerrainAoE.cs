using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
public class TerrainAoE : MonoBehaviour
{
    private Transform owner;
    private LayerMask groundMask;
    private float baseHeight;
    private readonly Dictionary<Vector2Int, float> heights = new Dictionary<Vector2Int, float>();
    private readonly List<ParticleSystem> grounded = new List<ParticleSystem>();
    private readonly List<GroundLayer> layers = new List<GroundLayer>();
    private ParticleSystem.Particle[] particles;

    private sealed class GroundLayer
    {
        public ParticleSystemRenderer source;
        public Mesh baked, fitted;
        public MeshRenderer renderer;
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<int> triangles = new List<int>();
        public readonly List<Vector4>[] uv = { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
        public readonly List<Vector4>[] sourceUV = { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
    }

    public static void AddTo(GameObject spell, ElementType element, Transform owner, LayerMask mask)
    {
        if (element != ElementType.Ice && element != ElementType.Earth) return;
        var follow = spell.AddComponent<TerrainAoE>();
        follow.owner = owner;
        follow.groundMask = mask;
        follow.baseHeight = spell.transform.position.y;
        int capacity = 1;
        foreach (var system in spell.GetComponentsInChildren<ParticleSystem>())
        {
            capacity = Mathf.Max(capacity, system.main.maxParticles);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if ((element == ElementType.Ice && (system == spell.GetComponent<ParticleSystem>() || system.name == "Ice" || system.name == "Glow")) ||
                (element == ElementType.Earth && system.name == "Spikes")) follow.grounded.Add(system);
            bool groundLayer = renderer.alignment == ParticleSystemRenderSpace.Local &&
                (renderer.renderMode == ParticleSystemRenderMode.Billboard || system.name == "ShockWave");
            if (!groundLayer) continue;
            var go = new GameObject(system.name + " terrain surface");
            go.transform.SetParent(spell.transform, false);
            // Baked particle vertices are converted to world space, so this mesh uses an identity transform.
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var layer = new GroundLayer { source = renderer, baked = new Mesh(), fitted = new Mesh() };
            layer.baked.MarkDynamic(); layer.fitted.MarkDynamic();
            layer.fitted.indexFormat = IndexFormat.UInt32;
            go.AddComponent<MeshFilter>().sharedMesh = layer.fitted;
            layer.renderer = go.AddComponent<MeshRenderer>();
            layer.renderer.sharedMaterials = renderer.sharedMaterials;
            layer.renderer.shadowCastingMode = ShadowCastingMode.Off;
            layer.renderer.receiveShadows = false;
            renderer.enabled = false;
            follow.layers.Add(layer);
        }
        follow.particles = new ParticleSystem.Particle[capacity];
    }

    private void LateUpdate()
    {
        foreach (var system in grounded)
        {
            int count = system.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                var position = SpellElementLight.ParticleWorldPosition(system, particles[i].position);
                position.y = GroundHeight(position) + 0.04f;
                var main = system.main;
                var space = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : system.transform;
                particles[i].position = main.simulationSpace == ParticleSystemSimulationSpace.World ? position : space.InverseTransformPoint(position);
            }
            system.SetParticles(particles, count);
        }
        if (Camera.main == null) return;
        foreach (var layer in layers) FitLayer(layer);
    }

    private float GroundHeight(Vector3 position)
    {
        float x = position.x * 2f, z = position.z * 2f;
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        return Mathf.Lerp(Mathf.Lerp(Sample(ix, iz), Sample(ix + 1, iz), x - ix),
            Mathf.Lerp(Sample(ix, iz + 1), Sample(ix + 1, iz + 1), x - ix), z - iz);
    }

    private float Sample(int x, int z)
    {
        var key = new Vector2Int(x, z);
        if (heights.TryGetValue(key, out float height)) return height;
        height = baseHeight - 0.02f;
        float highest = float.NegativeInfinity;
        var hits = Physics.RaycastAll(new Vector3(x * 0.5f, baseHeight + 24f, z * 0.5f), Vector3.down, 128f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.normal.y < 0.25f || hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            // Do not project an indoor cast onto the top of a separate overhead ceiling.
            if (hit.collider.bounds.min.y > baseHeight + 2f) continue;
            if (hit.point.y <= highest) continue;
            highest = hit.point.y;
            height = hit.point.y;
        }
        // shortcut: cache the static ground during each short cast; resample if spells later need moving platforms.
        heights.Add(key, height);
        return height;
    }

    private void FitLayer(GroundLayer layer)
    {
        layer.source.BakeMesh(layer.baked, Camera.main, false);
        var vertices = layer.baked.vertices;
        var colors = layer.baked.colors;
        var indices = layer.baked.triangles;
        layer.positions.Clear(); layer.colors.Clear(); layer.triangles.Clear();
        for (int channel = 0; channel < 4; channel++)
        {
            layer.uv[channel].Clear(); layer.sourceUV[channel].Clear();
            layer.baked.GetUVs(channel, layer.sourceUV[channel]);
        }
        var system = layer.source.GetComponent<ParticleSystem>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            Subdivide(layer, system, vertices, colors, a, b, c, Vector3.right, Vector3.up, Vector3.forward, 0);
        }
        layer.fitted.Clear();
        layer.fitted.SetVertices(layer.positions); layer.fitted.SetColors(layer.colors);
        for (int channel = 0; channel < 4; channel++)
            if (layer.sourceUV[channel].Count == vertices.Length) layer.fitted.SetUVs(channel, layer.uv[channel]);
        layer.fitted.SetTriangles(layer.triangles, 0);
        layer.fitted.RecalculateNormals();
        layer.fitted.RecalculateBounds();
    }

    private void Subdivide(GroundLayer layer, ParticleSystem system, Vector3[] positions, Color[] colors,
        int a, int b, int c, Vector3 wa, Vector3 wb, Vector3 wc, int depth)
    {
        Vector3 pa = positions[a] * wa.x + positions[b] * wa.y + positions[c] * wa.z;
        Vector3 pb = positions[a] * wb.x + positions[b] * wb.y + positions[c] * wb.z;
        Vector3 pc = positions[a] * wc.x + positions[b] * wc.y + positions[c] * wc.z;
        if (depth < 4 && Mathf.Max((pa - pb).sqrMagnitude, (pb - pc).sqrMagnitude, (pc - pa).sqrMagnitude) > 1f)
        {
            Vector3 ab = (wa + wb) * 0.5f, bc = (wb + wc) * 0.5f, ca = (wc + wa) * 0.5f;
            Subdivide(layer, system, positions, colors, a, b, c, wa, ab, ca, depth + 1);
            Subdivide(layer, system, positions, colors, a, b, c, ab, wb, bc, depth + 1);
            Subdivide(layer, system, positions, colors, a, b, c, ca, bc, wc, depth + 1);
            Subdivide(layer, system, positions, colors, a, b, c, ab, bc, ca, depth + 1);
            return;
        }
        AddVertex(layer, system, pa, colors, a, b, c, wa);
        AddVertex(layer, system, pb, colors, a, b, c, wb);
        AddVertex(layer, system, pc, colors, a, b, c, wc);
    }

    private void AddVertex(GroundLayer layer, ParticleSystem system, Vector3 position, Color[] colors, int a, int b, int c, Vector3 weights)
    {
        position = SpellElementLight.ParticleWorldPosition(system, position);
        position.y += GroundHeight(position) - baseHeight + 0.04f;
        layer.triangles.Add(layer.positions.Count);
        layer.positions.Add(position);
        layer.colors.Add(colors.Length > c && colors.Length > a && colors.Length > b ? colors[a] * weights.x + colors[b] * weights.y + colors[c] * weights.z : Color.white);
        for (int channel = 0; channel < 4; channel++)
        {
            var uv = layer.sourceUV[channel];
            if (uv.Count > a && uv.Count > b && uv.Count > c) layer.uv[channel].Add(uv[a] * weights.x + uv[b] * weights.y + uv[c] * weights.z);
        }
    }

    private void OnDestroy()
    {
        foreach (var layer in layers) { Destroy(layer.baked); Destroy(layer.fitted); }
    }
}
