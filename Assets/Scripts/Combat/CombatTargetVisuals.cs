using UnityEngine;

[RequireComponent(typeof(CombatTarget))]
public class CombatTargetVisuals : MonoBehaviour
{
    [SerializeField] private Renderer skin;
    [SerializeField] private GameObject frozenShell;
    [SerializeField] private GameObject burnEffect;
    [SerializeField] private GameObject affinityEffect;
    [SerializeField] private Transform stunEffect;
    [SerializeField] private Transform model;
    private Quaternion restingRotation;
    private CombatTarget target;
    private MaterialPropertyBlock properties;
    private Color baseColor;
    private Color baseEmission;
    private LineRenderer statusRing;
    private Material ringMaterial;
    private LineRenderer lightningCorona;
    private Animator rig;

    private void Start()
    {
        var ring = new GameObject("Status seal");
        ring.transform.SetParent(transform, false);
        statusRing = ring.AddComponent<LineRenderer>();
        ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        ringMaterial.SetColor("_BaseColor", Color.white);
        statusRing.sharedMaterial = ringMaterial;
        statusRing.useWorldSpace = true;
        statusRing.loop = true;
        statusRing.positionCount = 48;
        statusRing.widthMultiplier = 0.065f;
        statusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        statusRing.enabled = false;
        if (target.Affinity == ElementType.Lightning)
        {
            rig = GetComponentInChildren<Animator>();
            var corona = new GameObject("Affinity lightning discharge");
            corona.transform.SetParent(transform, false);
            lightningCorona = corona.AddComponent<LineRenderer>();
            lightningCorona.sharedMaterial = ringMaterial;
            lightningCorona.positionCount = 13;
            lightningCorona.widthMultiplier = 0.035f;
            lightningCorona.startColor = lightningCorona.endColor = new Color(0.45f, 0.85f, 1f) * 2f;
            lightningCorona.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
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
        SetVisible(frozenShell, !target.IsDead && target.IsFrozen);
        SetVisible(burnEffect, !target.IsDead && target.BurnRemaining > 0f);
        SetVisible(affinityEffect, !target.IsDead);
        if (lightningCorona != null)
        {
            lightningCorona.enabled = !target.IsDead && Time.time % 1.4f < 0.3f;
            if (lightningCorona.enabled && rig != null && rig.isHuman)
            {
                Vector3 start = rig.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Vector3 middle = rig.GetBoneTransform(HumanBodyBones.Chest).position + Vector3.up * 0.3f;
                Vector3 end = rig.GetBoneTransform(HumanBodyBones.RightHand).position;
                for (int i = 0; i < 13; i++)
                {
                    Vector3 point = i <= 6 ? Vector3.Lerp(start, middle, i / 6f) : Vector3.Lerp(middle, end, (i - 6) / 6f);
                    if (i > 0 && i < 12) point += Random.insideUnitSphere * 0.1f;
                    lightningCorona.SetPosition(i, point);
                }
            }
        }
        if (statusRing != null)
        {
            statusRing.enabled = !target.IsDead && (target.IsFrozen || target.IsStunned || target.IsGrounded || target.IsCharged);
            Color seal = target.IsFrozen ? new Color(0.15f, 0.9f, 1f) : target.IsStunned ? new Color(1f, 0.6f, 0.1f) : target.IsGrounded ? new Color(0.3f, 0.85f, 0.15f) : new Color(0.8f, 0.35f, 1f);
            statusRing.startColor = statusRing.endColor = seal;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48 + Time.time;
                float radius = target.IsFrozen ? (i % 4 == 0 ? 0.85f : 0.65f) : 0.5f;
                Vector3 center = target.IsStunned ? target.HeadPosition + Vector3.up * 0.3f : transform.position + Vector3.up * 0.12f;
                statusRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            }
        }
        if (stunEffect != null)
        {
            SetVisible(stunEffect.gameObject, !target.IsDead && target.IsStunned && !target.IsFrozen);
            stunEffect.position = target.HeadPosition + Vector3.up * 0.15f;
            stunEffect.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
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

    private static void SetVisible(GameObject effect, bool visible)
    {
        if (effect != null && effect.activeSelf != visible) effect.SetActive(visible);
    }

    private void OnDisable()
    {
        if (model != null) model.localRotation = restingRotation;
        if (statusRing != null) statusRing.enabled = false;
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
