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
    public Vector3 AimPoint => transform.position + Vector3.up;
    public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 1.8f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        animatorSpeed = animator != null ? animator.speed : 1f;
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        hitboxes = GetComponentsInChildren<Collider>();
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
        if (burnRemaining > 0f)
        {
            float burningTime = Mathf.Min(seconds, burnRemaining);
            burnRemaining = Mathf.Max(0f, burnRemaining - seconds);
            LoseHealth(4f * burningTime);
        }
        if (animator != null) animator.speed = IsFrozen || IsStunned ? 0f : animatorSpeed;
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
        if (IsDead || !float.IsFinite(amount) || amount <= 0f) return;
        float damage = amount * Multiplier(element, affinity);
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
            stunRemaining = Mathf.Max(stunRemaining, 0.65f);
        }
        if (element == ElementType.Lightning)
        {
            chargedRemaining = 4.5f;
            stunRemaining = Mathf.Max(stunRemaining, 1.25f);
        }
        if (seismic)
        {
            groundedRemaining = chargedRemaining = 0f;
            stunRemaining = 3f;
            reaction = "SEISMIC STUN";
            if (seismicPrefab != null) Instantiate(seismicPrefab, AimPoint, Quaternion.identity);
        }
        if (element == ElementType.Ice) frozenRemaining = 3f;
        if (element == ElementType.Fire) burnRemaining = 4f;

        LastFeedback = string.IsNullOrEmpty(reaction) ? $"{damage:0.#}" : reaction;
        LastHitTime = Time.time;
        LoseHealth(damage);
        if (!IsDead && element == ElementType.Earth) PushFrom(origin, slippery ? 27f : 18f);
        if (animator != null) animator.speed = IsFrozen || IsStunned ? 0f : animatorSpeed;
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
        Health = Mathf.Max(0f, Health - amount);
        if (!IsDead) return;
        frozenRemaining = burnRemaining = stunRemaining = groundedRemaining = chargedRemaining = 0f;
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
    }

    public void ResetTarget()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        body.position = spawnPosition;
        body.rotation = spawnRotation;
        body.isKinematic = false;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        Health = maxHealth;
        frozenRemaining = burnRemaining = stunRemaining = groundedRemaining = chargedRemaining = 0f;
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
