using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CombatTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private ElementType affinity;
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float trainingRespawnDelay = 5f;
    [SerializeField] private Transform model;
    [SerializeField] private GameObject steamPrefab;
    [SerializeField] private GameObject seismicPrefab;
    [Tooltip("Scales freeze, stun, grounded and charged durations. Bosses use ~0.3 so they can't be stun-locked.")]
    [SerializeField, Min(0f)] private float statusScale = 1f;
    [Tooltip("Damage multiplier for hits that aren't this target's weakness (1 = normal). The final boss uses this to force element switching.")]
    [SerializeField, Range(0f, 1f)] private float nonWeaknessMultiplier = 1f;

    private Rigidbody body;
    private Animator animator;
    private Collider[] hitboxes;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float frozenRemaining;
    private float burnRemaining;
    private float respawnRemaining;
    private float animatorSpeed;
    private Transform head;
    private float stunRemaining;
    private float groundedRemaining;
    private float chargedRemaining;
    private float staggerRemaining;
    private Collider bodyCollider;

    // Fired once each time this target's health reaches zero (loot drops, boss rewards).
    public event Action Died;
    // Fired on every hit that landed, with the element and the damage actually dealt.
    public event Action<ElementType, float> Hit;
    // A combo can stagger even status-resistant bosses; while staggered they take bonus damage.
    public bool IsStaggered => staggerRemaining > 0f;
    // Bosses resist ordinary stuns; combos still stagger them.
    public bool ResistsStatus => statusScale < 1f;
    // Set by scripted moments (e.g. the final boss changing element); hits are ignored.
    public bool Invulnerable { get; set; }

    public ElementType Affinity => affinity;
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => Health <= 0f;
    public bool IsFrozen => frozenRemaining > 0f;
    public float FreezeRemaining => frozenRemaining;
    public float BurnRemaining => burnRemaining;
    public bool IsStunned => stunRemaining > 0f;
    public float StunRemaining => stunRemaining;
    public bool IsGrounded => groundedRemaining > 0f;
    public bool IsCharged => chargedRemaining > 0f;
    public string LastFeedback { get; private set; }
    public float LastHitTime { get; private set; } = float.NegativeInfinity;
    // Collider centre, so scaled-up bosses are aimed at their body rather than their knees.
    public Vector3 AimPoint => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up;
    public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 1.8f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        animatorSpeed = animator != null ? animator.speed : 1f;
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        hitboxes = GetComponentsInChildren<Collider>();
        bodyCollider = GetComponent<Collider>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        Health = maxHealth;
    }

    private void Update() => Advance(Time.deltaTime);

    private void Advance(float seconds)
    {
        if (IsDead)
        {
            if (trainingRespawnDelay <= 0f) return;
            respawnRemaining -= seconds;
            if (respawnRemaining <= 0f) ResetTarget();
            return;
        }
        frozenRemaining = Mathf.Max(0f, frozenRemaining - seconds);
        stunRemaining = Mathf.Max(0f, stunRemaining - seconds);
        groundedRemaining = Mathf.Max(0f, groundedRemaining - seconds);
        chargedRemaining = Mathf.Max(0f, chargedRemaining - seconds);
        staggerRemaining = Mathf.Max(0f, staggerRemaining - seconds);
        if (burnRemaining > 0f)
        {
            float burningTime = Mathf.Min(seconds, burnRemaining);
            burnRemaining = Mathf.Max(0f, burnRemaining - seconds);
            LoseHealth(4f * burningTime);
        }
        if (animator != null) animator.speed = IsFrozen || IsStunned || IsStaggered ? 0f : animatorSpeed;
    }

    public void SetAffinity(ElementType element) => affinity = element;

    // New Game+ bosses are tougher.
    public void SetMaxHealthScale(float scale)
    {
        maxHealth *= scale;
        Health = maxHealth;
    }

    // A combo stagger ignores statusScale: it's the reliable way to interrupt a boss.
    public void Stagger(float seconds)
    {
        if (IsDead) return;
        staggerRemaining = Mathf.Max(staggerRemaining, seconds);
        stunRemaining = Mathf.Max(stunRemaining, seconds);
        if (animator != null) animator.speed = 0f;
    }

    public static float Multiplier(ElementType attack, ElementType target)
    {
        if (attack == ElementType.None || target == ElementType.None) return 1f;
        if (attack == target) return 0.5f;
        if ((attack == ElementType.Fire && target == ElementType.Ice) ||
            (attack == ElementType.Ice && target == ElementType.Fire) ||
            (attack == ElementType.Earth && target == ElementType.Lightning) ||
            (attack == ElementType.Lightning && target == ElementType.Earth)) return 1.5f;
        return 1f;
    }

    public void TakeDamage(float amount, ElementType sourceElement = ElementType.None)
    {
        ReceiveSpell(amount, sourceElement, transform.position - transform.forward);
    }

    public void ReceiveSpell(float amount, ElementType element, Vector3 origin)
    {
        if (IsDead || Invulnerable || !float.IsFinite(amount) || amount <= 0f) return;
        float multiplier = Multiplier(element, affinity);
        float damage = amount * multiplier;
        bool resisted = multiplier < 1.5f && nonWeaknessMultiplier < 1f;
        if (resisted) damage *= nonWeaknessMultiplier;
        if (IsStaggered) damage *= 1.5f;
        string reaction = "";
        bool slippery = element == ElementType.Earth && IsFrozen;
        bool seismic = (element == ElementType.Lightning && IsGrounded) || (element == ElementType.Earth && IsCharged);
        if (element == ElementType.Fire && IsFrozen)
        {
            frozenRemaining = 0f;
            reaction = "STEAM BURST";
            if (steamPrefab != null) Instantiate(steamPrefab, AimPoint, Quaternion.identity);
        }
        else if (element == ElementType.Ice && burnRemaining > 0f)
        {
            burnRemaining = 0f;
            damage += 10f;
            reaction = "THERMAL SHOCK +10";
        }
        else if (slippery) reaction = "SLIPPERY IMPACT";
        if (element == ElementType.Earth)
        {
            groundedRemaining = 4.5f;
            stunRemaining = Mathf.Max(stunRemaining, 0.65f * statusScale);
        }
        if (element == ElementType.Lightning)
        {
            chargedRemaining = 4.5f;
            stunRemaining = Mathf.Max(stunRemaining, 1.25f * statusScale);
        }
        if (seismic)
        {
            groundedRemaining = chargedRemaining = 0f;
            reaction = "STORM QUAKE";
            if (seismicPrefab != null) Instantiate(seismicPrefab, AimPoint, Quaternion.identity);
            StormQuake.Spawn(this, AimPoint);
        }
        if (element == ElementType.Ice) frozenRemaining = 3f * statusScale;
        if (element == ElementType.Fire) burnRemaining = 4f;

        if (resisted && string.IsNullOrEmpty(reaction)) reaction = $"RESISTED {damage:0.#}";
        LastFeedback = string.IsNullOrEmpty(reaction) ? $"{damage:0.#}" : reaction;
        LastHitTime = Time.time;
        LoseHealth(damage);
        Hit?.Invoke(element, damage);
        if (!IsDead && element == ElementType.Earth) PushFrom(origin, slippery ? 27f : 18f);
        if (animator != null) animator.speed = IsFrozen || IsStunned || IsStaggered ? 0f : animatorSpeed;
    }

    public void TransferBurn(float remaining)
    {
        if (!IsDead && remaining > 0f)
        {
            burnRemaining = Mathf.Max(burnRemaining, remaining);
            LastFeedback = "WILDFIRE ARC";
        }
    }

    private void PushFrom(Vector3 origin, float impulse)
    {
        Vector3 direction = Vector3.ProjectOnPlane(transform.position - origin, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        body.AddForce(direction * impulse, ForceMode.Impulse);
    }

    private void LoseHealth(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Max(0f, Health - amount);
        if (!IsDead) return;
        frozenRemaining = burnRemaining = stunRemaining = groundedRemaining = chargedRemaining = staggerRemaining = 0f;
        respawnRemaining = trainingRespawnDelay;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        foreach (var hitbox in hitboxes) hitbox.enabled = false;
        if (animator != null && animator.HasState(0, Animator.StringToHash("Base Layer.Death")))
        {
            animator.speed = animatorSpeed;
            animator.CrossFadeInFixedTime("Death", 0.12f, 0);
        }
        else if (model != null) model.gameObject.SetActive(false);
        Died?.Invoke();
    }


    public void ResetTarget()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        body.position = spawnPosition;
        body.rotation = spawnRotation;
        body.isKinematic = false;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        Health = maxHealth;
        frozenRemaining = burnRemaining = stunRemaining = groundedRemaining = chargedRemaining = staggerRemaining = 0f;
        LastFeedback = "";
        LastHitTime = float.NegativeInfinity;
        foreach (var hitbox in hitboxes) hitbox.enabled = true;
        if (model != null) model.gameObject.SetActive(true);
        if (animator != null) animator.speed = animatorSpeed;
        if (animator != null && animator.HasState(0, Animator.StringToHash("Base Layer.Locomotion")))
        {
            animator.Play("Locomotion", 0, 0f);
        }
    }

    private void OnDisable()
    {
        if (animator != null) animator.speed = animatorSpeed;
    }
}
