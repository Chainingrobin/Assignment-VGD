using UnityEngine;

public class LightningArc : MonoBehaviour
{
    public const float Lifetime = 1.5f;
    private CombatTarget source;
    private CombatTarget destination;
    private LineRenderer core;
    private LineRenderer glow;
    private Light illumination;
    private float age;
    private readonly Vector3[] offsets = new Vector3[13];

    public static void Create(CombatTarget source, CombatTarget destination, Material material)
    {
        var arc = new GameObject("Lightning chain arc").AddComponent<LightningArc>();
        arc.source = source;
        arc.destination = destination;
        arc.glow = arc.MakeLine("Purple glow", material, 0.2f, 0);
        arc.core = arc.MakeLine("White core", material, 0.055f, 1);
        arc.illumination = arc.gameObject.AddComponent<Light>();
        arc.illumination.type = LightType.Point;
        arc.illumination.color = new Color(0.6f, 0.35f, 1f);
        arc.illumination.range = 4f;
        for (int i = 1; i < arc.offsets.Length - 1; i++) arc.offsets[i] = Random.insideUnitSphere * 0.3f;
        arc.LateUpdate();
    }

    private LineRenderer MakeLine(string name, Material material, float width, int order)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = offsets.Length;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.sortingOrder = order;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime || source == null || destination == null) Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (source == null || destination == null || core == null) return;
        Vector3 start = source.AimPoint;
        Vector3 end = destination.AimPoint;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, i / (offsets.Length - 1f));
            point += offsets[i] * (0.8f + 0.2f * Mathf.Sin(age * 24f + i * 2f));
            core.SetPosition(i, point);
            glow.SetPosition(i, point);
        }
        float fade = Mathf.Clamp01((Lifetime - age) / 0.6f);
        core.startColor = core.endColor = new Color(3.2f, 3.6f, 4f, fade);
        glow.startColor = glow.endColor = new Color(1.2f, 0.3f, 3f, fade * 0.4f);
        transform.position = (start + end) * 0.5f;
        illumination.intensity = fade * 2f;
    }
}
