using UnityEngine;

[RequireComponent(typeof(CombatTarget))]
public class CombatTargetVisuals : MonoBehaviour
{
    [SerializeField] private Renderer skin;
    [SerializeField] private GameObject frozenShell;
    [SerializeField] private GameObject frozenEffect;
    [SerializeField] private GameObject burnEffect;
    [SerializeField] private GameObject groundedEffect;
    [SerializeField] private GameObject chargedEffect;
    [SerializeField] private GameObject affinityEffect;
    [SerializeField] private Transform stunEffect;
    [SerializeField] private Transform model;
    [SerializeField] private Material arcMaterial;
    private Quaternion restingRotation;
    private CombatTarget target;
    private MaterialPropertyBlock properties;
    private Color baseColor;
    private Color baseEmission;
    private Animator rig;
    private LineRenderer[] arcs;
    private readonly Vector3[] arcPoints = new Vector3[13];
    private readonly Vector3[] branchPoints = new Vector3[5];
    private float nextStrike;

    private void Start()
    {
        rig = GetComponentInChildren<Animator>();
        if (target.Affinity != ElementType.Lightning || arcMaterial == null || rig == null || !rig.isHuman) return;
        // Vertex colours clamp to 1; the HDR glow comes from arcMaterial's tint.
        arcs = new[]
        {
            Arc("Affinity arc glow", 0.24f, new Color(0.45f, 0.2f, 1f, 0.3f)),
            Arc("Affinity arc", 0.05f, new Color(0.85f, 0.75f, 1f)),
            Arc("Affinity arc branch", 0.03f, new Color(0.7f, 0.5f, 1f))
        };
    }

    private LineRenderer Arc(string arcName, float width, Color color)
    {
        var arc = new GameObject(arcName).AddComponent<LineRenderer>();
        arc.transform.SetParent(transform, false);
        arc.sharedMaterial = arcMaterial;
        arc.textureMode = LineTextureMode.Stretch;
        arc.widthMultiplier = width;
        arc.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.35f));
        arc.startColor = arc.endColor = color;
        arc.numCapVertices = 2;
        arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arc.enabled = false;
        return arc;
    }

    private void OnEnable()
    {
        target = GetComponent<CombatTarget>();
        properties = new MaterialPropertyBlock();
        if (model != null) restingRotation = model.localRotation;
        if (skin != null) baseColor = skin.sharedMaterial.GetColor("_BaseColor");
        if (skin != null) baseEmission = skin.sharedMaterial.GetColor("_EmissionColor");
    }

    private void LateUpdate()
    {
        bool alive = !target.IsDead;
        SetVisible(frozenShell, alive && target.IsFrozen);
        SetVisible(frozenEffect, alive && target.IsFrozen);
        SetVisible(burnEffect, alive && target.BurnRemaining > 0f);
        // Ice owns the ground while frozen so seals never stack.
        SetVisible(groundedEffect, alive && target.IsGrounded && !target.IsFrozen);
        SetVisible(chargedEffect, alive && target.IsCharged && !target.IsFrozen);
        SetVisible(affinityEffect, alive);
        if (arcs != null)
        {
            bool discharging = alive && Time.time % 1.4f < 0.35f;
            foreach (var arc in arcs) arc.enabled = discharging;
            // Re-strike at ~20 Hz: per-frame jitter reads as noise, held shapes read as lightning.
            if (discharging && Time.time >= nextStrike)
            {
                nextStrike = Time.time + 0.05f;
                Strike();
            }
        }
        if (stunEffect != null)
        {
            SetVisible(stunEffect.gameObject, alive && target.IsStunned && !target.IsFrozen);
            stunEffect.position = target.HeadPosition + Vector3.up * 0.25f;
            stunEffect.Rotate(Vector3.up, 220f * Time.deltaTime, Space.World);
        }
        float age = Time.time - target.LastHitTime;
        if (model != null) model.localRotation = restingRotation * Quaternion.Euler(0, 0, age < 0.4f ? Mathf.Sin(age / 0.4f * Mathf.PI) * 12f : 0f);
        if (skin == null) return;
        float flash = Mathf.Clamp01(1f - age / 0.25f);
        skin.GetPropertyBlock(properties, 0);
        Color statusColor = target.IsFrozen ? new Color(0.25f, 0.85f, 1f) : target.IsStunned ? new Color(0.8f, 0.35f, 1f) : target.BurnRemaining > 0f ? new Color(1f, 0.25f, 0.04f) : baseColor;
        properties.SetColor("_BaseColor", Color.Lerp(statusColor, Color.white * 3f, flash));
        properties.SetColor("_EmissionColor", target.IsFrozen || target.IsStunned || target.BurnRemaining > 0f ? statusColor * 1.5f : baseEmission);
        skin.SetPropertyBlock(properties, 0);
    }

    private void Strike()
    {
        Vector3 start = rig.GetBoneTransform(HumanBodyBones.LeftHand).position;
        Vector3 middle = rig.GetBoneTransform(HumanBodyBones.Chest).position + Vector3.up * 0.3f;
        Vector3 end = rig.GetBoneTransform(HumanBodyBones.RightHand).position;
        for (int i = 0; i < arcPoints.Length; i++)
        {
            arcPoints[i] = i <= 6 ? Vector3.Lerp(start, middle, i / 6f) : Vector3.Lerp(middle, end, (i - 6) / 6f);
            if (i > 0 && i < arcPoints.Length - 1) arcPoints[i] += Random.insideUnitSphere * 0.09f;
        }
        Vector3 fork = arcPoints[Random.Range(2, arcPoints.Length - 2)];
        Vector3 reach = (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(0.25f, 0.45f);
        for (int i = 0; i < branchPoints.Length; i++)
            branchPoints[i] = fork + reach * (i / (branchPoints.Length - 1f)) + (i > 0 ? Random.insideUnitSphere * 0.05f : Vector3.zero);
        for (int i = 0; i < 2; i++)
        {
            arcs[i].positionCount = arcPoints.Length;
            arcs[i].SetPositions(arcPoints);
        }
        arcs[2].positionCount = branchPoints.Length;
        arcs[2].SetPositions(branchPoints);
    }

    private static void SetVisible(GameObject effect, bool visible)
    {
        if (effect != null && effect.activeSelf != visible) effect.SetActive(visible);
    }

    private void OnDisable()
    {
        if (model != null) model.localRotation = restingRotation;
        if (arcs != null) foreach (var arc in arcs) arc.enabled = false;
    }
}
