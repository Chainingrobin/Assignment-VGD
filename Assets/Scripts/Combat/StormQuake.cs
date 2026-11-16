using System.Collections.Generic;
using UnityEngine;

// The Earth + Lightning combo, Fire + Ice's offensive counterpart. Steam hides Wulang; a
// Storm Quake pins enemies down. It triggers when Lightning hits a Grounded enemy or Earth
// hits a Charged one: an electrified shockwave staggers and damages everything nearby, then
// a crackling field keeps staggering any enemy that walks in. Bosses resist ordinary stuns,
// but a Storm Quake always staggers them, and staggered targets take +50% damage, which is
// the reason to keep switching between the two elements.
public class StormQuake : MonoBehaviour
{
    public const float Radius = 5f;
    public const float FieldDuration = 5f;
    public const float BonusDamage = 15f;
    public const float EnemyStagger = 3f;
    public const float BossStagger = 2f;
    public const float FieldStagger = 1.5f;

    private readonly HashSet<CombatTarget> affected = new();
    private CombatTarget source;
    private Material arcMaterial;
    private LineRenderer ring;
    private Light flash;
    private float age;
    private float nextScan;

    public static StormQuake Spawn(CombatTarget source, Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            point = hit.point;
        var quake = new GameObject("Storm Quake").AddComponent<StormQuake>();
        quake.transform.position = point;
        quake.source = source;
        var visuals = source.GetComponent<CombatTargetVisuals>();
        quake.arcMaterial = visuals != null ? visuals.ArcMaterial : null;
        quake.BuildVisuals();
        quake.Shockwave();
        return quake;
    }

    private void BuildVisuals()
    {
        ring = gameObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = 48;
        ring.widthMultiplier = 0.25f;
        ring.sharedMaterial = arcMaterial;
        ring.startColor = ring.endColor = new Color(1f, 0.85f, 0.45f);
        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(0.75f, 0.55f, 1f);
        flash.range = Radius * 2.5f;
        flash.intensity = 8f;
    }

    // The initial blast: bonus damage and a stagger for everything in range, source included.
    private void Shockwave()
    {
        foreach (var target in TargetsInRange())
        {
            affected.Add(target);
            target.Stagger(target.ResistsStatus ? BossStagger : EnemyStagger);
            target.TakeDamage(BonusDamage, ElementType.None);
            if (target != source && arcMaterial != null) LightningArc.Create(source, target, arcMaterial);
        }
    }

    private IEnumerable<CombatTarget> TargetsInRange()
    {
        var found = new HashSet<CombatTarget>();
        foreach (var collider in Physics.OverlapSphere(transform.position, Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = collider.GetComponentInParent<CombatTarget>();
            if (target != null && !target.IsDead && found.Add(target)) yield return target;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= FieldDuration) { Destroy(gameObject); return; }
        // Shockwave expands in the first half second, then the field ring crackles at full size.
        float radius = Mathf.Lerp(0.5f, Radius, Mathf.Clamp01(age / 0.5f));
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            float jitter = age > 0.5f ? Random.Range(-0.25f, 0.25f) : 0f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * (radius + jitter), 0.15f, Mathf.Sin(angle) * (radius + jitter)));
        }
        flash.intensity = Mathf.Lerp(8f, 1.5f, Mathf.Clamp01(age / 0.6f)) * (age > FieldDuration - 0.5f ? (FieldDuration - age) * 2f : 1f);

        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.25f;
        foreach (var target in TargetsInRange())
            if (affected.Add(target))
            {
                target.Stagger(FieldStagger);
                if (arcMaterial != null && source != null) LightningArc.Create(source, target, arcMaterial);
            }
    }
}
