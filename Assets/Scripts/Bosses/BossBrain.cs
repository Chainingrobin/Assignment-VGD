using System;
using System.Collections.Generic;
using UnityEngine;

// The shared brain for every guardian and the final boss. Each boss is just a different
// attack list: melee swipes, element projectiles (fireballs, ice shards, boulders) and
// element ground attacks (spikes, ice slams, meteors) that show a warning ring before they
// land. The brain only picks a gait (idle, walk, run) and a facing; the animations' own root
// motion moves the body (RootMotionBody), so strides, leaps and swipes keep the feet planted.
// The boss stays tied to its arena. At half health it roars and attacks more often. All damage
// goes through PlayerHealth, and every effect is the player's own spell effect, scaled up.
[RequireComponent(typeof(CombatTarget), typeof(Rigidbody))]
public class BossBrain : MonoBehaviour
{
    public enum AttackKind { Melee, Projectile, Ground }

    [Serializable]
    public class BossAttack
    {
        public string name;
        public AttackKind kind;
        [Tooltip("None = use the boss's current element (the final boss's phase).")]
        public ElementType element;
        [Tooltip("Animator state to play.")]
        public string state;
        public float minRange;
        public float maxRange = 4f;
        [Tooltip("Seconds from the start of the animation until the hit, launch or impact.")]
        public float windup = 0.8f;
        [Tooltip("Seconds the whole action takes before the boss can move again.")]
        public float duration = 2f;
        public float damage = 15f;
        [Tooltip("Melee reach, or the ground attack's blast radius.")]
        public float radius = 3f;
        [Tooltip("Projectiles per volley, or blast circles for a ground attack.")]
        public int count = 1;
        [Tooltip("Ground: centre the blast on the boss (slam) instead of the player.")]
        public bool atSelf;
        [Tooltip("Ground: blasts form a line from the boss to the player (spike line).")]
        public bool line;
        [Tooltip("Projectile: lob in an arc instead of flying straight.")]
        public bool lobbed;
        [Tooltip("Ground: blasts ring the player, leaving the middle safe (step in, or step out).")]
        public bool ring;
        [Tooltip("Projectile: fire the volley one shot after another, each aimed anew, instead of a fan.")]
        public bool stream;
        public float cooldown = 4f;
        public bool phaseTwoOnly;
        [NonSerialized] public float readyAt;
    }

    [SerializeField] private BossAttack[] attacks;
    [SerializeField, Min(10f)] private float turnSpeed = 160f;
    [SerializeField, Min(5f)] private float arenaRadius = 30f;
    [SerializeField, Min(0f)] private float recover = 0.8f;
    [SerializeField] private string roarState = "Roar";
    [SerializeField, Min(0f)] private float roarDuration = 2.5f;
    [SerializeField] private string hitState = "";

    private CombatTarget target;
    private Rigidbody body;
    private Animator animator;
    private Transform player;
    private SpellCaster spells;
    private Vector3 home;
    private bool engaged;
    private bool phaseTwo;
    private float busyUntil;
    private BossAttack current;
    private float actionStarted;
    private bool actionFired;
    private float speedScale = 1f;
    private readonly List<Vector3> pendingBlasts = new();
    private readonly List<GameObject> telegraphs = new();
    private static Material telegraphMaterial;
    private BossAttack last;
    private float lastSeenAt = float.NegativeInfinity;
    private Vector3 lastSeen;
    private int streamFired;

    // Stagger: every hit fills the meter by the damage it dealt, so bursts and weakness hits fill it fastest.
    // Full, the boss is stunned for a window and takes extra damage (CombatTarget.Stagger), then the meter
    // rests empty for a moment so it can't be chained. Bosses only; vampires have no meter.
    public const float StaggerSeconds = 4f, StaggerRest = 5f;
    private const float StaggerMax = 100f, StaggerPerDamage = 0.6f, StaggerDecayDelay = 4f, StaggerDecay = 8f;
    private float stagger, staggerEnds = float.NegativeInfinity, lastHit = float.NegativeInfinity;
    /// <summary>0-1 fill of the stagger meter.</summary>
    public float StaggerFill => stagger / StaggerMax;
    /// <summary>Seconds left in the stagger window; 0 when the meter hasn't broken.</summary>
    public float StaggerWindow => Mathf.Max(0f, staggerEnds - Time.time);
    /// <summary>True while the meter rests empty after a stagger.</summary>
    public bool StaggerResting => Time.time < staggerEnds + StaggerRest;
    public event Action StaggerBroken;

    public bool Engaged => engaged;

    // Used by the BossSetup editor tool to author each boss's move list.
    public void Configure(BossAttack[] moves, float arena, string roar, string hit)
    {
        attacks = moves;
        arenaRadius = arena;
        roarState = roar;
        hitState = hit;
    }
    public CombatTarget Target => target;
    public event Action PhaseTwo;

    private void Awake()
    {
        target = GetComponent<CombatTarget>();
        body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        home = transform.position;
        if (telegraphMaterial == null) telegraphMaterial = Resources.Load<Material>("BossTelegraph");
    }

    private void OnEnable() => target.Hit += OnHit;

    private void Update()
    {
        if (stagger > 0f && Time.time - lastHit > StaggerDecayDelay) stagger = Mathf.Max(0f, stagger - StaggerDecay * Time.deltaTime);
    }
    private void OnDisable() { target.Hit -= OnHit; ClearTelegraphs(); }

    public void Engage()
    {
        if (engaged || target.IsDead) return;
        var controller = FindFirstObjectByType<StarterAssets.FirstPersonController>();
        player = controller != null ? controller.transform : null;
        spells = FindFirstObjectByType<SpellCaster>();
        engaged = true;
        foreach (var attack in attacks) attack.readyAt = Time.time + 1f;
        Play(roarState, 0.2f);
        busyUntil = Time.time + roarDuration;
    }

    // Player died: reset the fight so it starts fresh.
    public void Disengage()
    {
        engaged = phaseTwo = false;
        speedScale = 1f;
        current = last = null;
        stagger = 0f;
        staggerEnds = float.NegativeInfinity;
        ClearTelegraphs();
        target.ResetTarget();
        home = transform.position;
    }

    // Scripted pause, e.g. the final boss's block animation while changing element.
    public void Interlude(string state, float seconds)
    {
        current = null;
        ClearTelegraphs();
        Play(state, 0.15f);
        busyUntil = Time.time + seconds;
    }

    private void FixedUpdate()
    {
        if (!engaged || player == null || animator == null) return;
        if (target.IsDead) { ClearTelegraphs(); engaged = false; return; }
        if (target.IsFrozen || target.IsStunned || target.IsStaggered)
        {
            // Interrupted: a stagger cancels the attack in progress (the combo payoff).
            if (current != null) { current = null; ClearTelegraphs(); busyUntil = Time.time + 0.3f; }
            SetGait(0f);
            return;
        }
        if (!phaseTwo && target.Health <= target.MaxHealth * 0.5f) EnterPhaseTwo();

        var toPlayer = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        float distance = toPlayer.magnitude;
        if (CanSee()) { lastSeenAt = Time.time; lastSeen = player.position; }

        if (current != null) { RunAttack(toPlayer, distance); return; }
        if (Time.time < busyUntil) { SetGait(0f); return; }

        Face(toPlayer);
        var next = Choose(distance, Time.time - lastSeenAt < 0.3f, Time.time - lastSeenAt < 2f);
        // Melee only once squared up, so the swing lands where the animation aims it.
        if (next != null && (next.kind != AttackKind.Melee || Vector3.Angle(transform.forward, toPlayer) < 35f)) { StartAttack(next); return; }

        // Chase, but never beyond the arena; return home if the player leaves it.
        float homeDistance = Vector3.ProjectOnPlane(transform.position - home, Vector3.up).magnitude;
        float playerHome = Vector3.ProjectOnPlane(player.position - home, Vector3.up).magnitude;
        if (playerHome > arenaRadius + 5f)
        {
            var back = Vector3.ProjectOnPlane(home - transform.position, Vector3.up);
            if (back.magnitude > 1.5f) { Face(back); SetGait(Walk); }
            else SetGait(0f);
            return;
        }
        // Walk in close, run from afar; stop at melee range or the arena edge.
        float gait = distance <= 3.5f || homeDistance >= arenaRadius ? 0f : distance > 10f ? Run : Walk;
        SetGait(gait);
    }

    private void EnterPhaseTwo()
    {
        phaseTwo = true;
        speedScale = 1.3f;
        current = null;
        ClearTelegraphs();
        Play(roarState, 0.2f);
        busyUntil = Time.time + roarDuration;
        PhaseTwo?.Invoke();
    }

    // A random pick among the attacks that fit right now, favouring ones unused for a while and hardly ever the same
    // attack twice running. Shots need a clear line to Wulang: behind cover she gets no volleys, only ground attacks
    // aimed where she was last seen (for two seconds), and otherwise the boss walks round to find her.
    private BossAttack Choose(float distance, bool inSight, bool recentlySeen)
    {
        BossAttack pick = null;
        float total = 0f;
        foreach (var attack in attacks)
        {
            if (attack.phaseTwoOnly && !phaseTwo) continue;
            if (Time.time < attack.readyAt || distance < attack.minRange || distance > attack.maxRange) continue;
            if (attack.kind == AttackKind.Projectile && !inSight) continue;
            if (attack.kind == AttackKind.Ground && !attack.atSelf && !recentlySeen) continue;
            float weight = attack == last ? 0.15f : 1f + Mathf.Min(2f, (Time.time - attack.readyAt) / 4f);
            total += weight;
            if (UnityEngine.Random.value * total < weight) pick = attack;
        }
        return pick;
    }

    // Line of sight from the boss's chest to Wulang's head, ignoring the boss and the player themselves.
    private bool CanSee()
    {
        var from = target.AimPoint;
        var to = player.position + Vector3.up * 1.5f;
        foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<CharacterController>() == null &&
                hit.collider.GetComponentInParent<CombatTarget>() == null) return false;
        return true;
    }

    private void StartAttack(BossAttack attack)
    {
        current = last = attack;
        actionStarted = Time.time;
        actionFired = false;
        streamFired = 0;
        Play(attack.state, 0.2f);
        SetGait(0f);
        pendingBlasts.Clear();
        if (attack.kind != AttackKind.Ground) return;
        // Ground attacks mark where they'll land straight away, so the player can dodge.
        var element = ElementOf(attack);
        int count = Mathf.Max(1, attack.count);
        var aimed = Time.time - lastSeenAt < 0.3f ? player.position : lastSeen;
        float spin = UnityEngine.Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            Vector3 point;
            if (attack.atSelf) point = transform.position + transform.forward * 2f;
            else if (attack.line) point = Vector3.Lerp(transform.position, aimed, (i + 1f) / count);
            else if (attack.ring)
            {
                // Circles just touching round the player: the middle stays safe.
                float angle = (spin + i * 360f / count) * Mathf.Deg2Rad;
                point = aimed + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * attack.radius * 1.9f;
            }
            else
            {
                // First circle on the player, the rest scattered around them (meteor rain).
                var scatter = i == 0 ? Vector2.zero : UnityEngine.Random.insideUnitCircle * attack.radius * 2.5f;
                point = aimed + new Vector3(scatter.x, 0f, scatter.y);
            }
            point = Ground(point);
            pendingBlasts.Add(point);
            telegraphs.Add(Telegraph(point, attack.radius, element));
        }
    }

    private void RunAttack(Vector3 toPlayer, float distance)
    {
        float elapsed = Time.time - actionStarted;
        SetGait(0f);
        if (!actionFired)
        {
            // Track the player through the wind-up only; the swing itself follows the animation.
            if (elapsed < current.windup * 0.7f) Face(toPlayer);
            // The slam follows the boss as it leaps.
            if (current.kind == AttackKind.Ground && current.atSelf && pendingBlasts.Count > 0)
            {
                pendingBlasts[0] = Ground(transform.position + transform.forward * 2f);
                if (telegraphs.Count > 0 && telegraphs[0] != null) telegraphs[0].transform.position = pendingBlasts[0] + Vector3.up * 0.1f;
            }
            if (elapsed >= current.windup) { actionFired = true; Fire(current, toPlayer, distance); }
        }
        // A stream keeps firing after its first shot, one more every 0.35 s, each aimed at where she is now.
        else if (current.stream && streamFired < Mathf.Max(1, current.count) && elapsed >= current.windup + streamFired * 0.35f)
        {
            Face(toPlayer);
            Shoot(current, 0f);
            streamFired++;
        }
        // The attack state blends back to Locomotion by itself near its end.
        if (elapsed >= current.duration)
        {
            current.readyAt = Time.time + current.cooldown / speedScale;
            current = null;
            // A short breath between attacks, longer while Wulang is badly hurt (time to drink or back off).
            var health = PlayerHealth.Instance;
            bool hurt = health != null && health.Health < health.MaxHealth * 0.3f;
            busyUntil = Time.time + recover / speedScale * (hurt ? 1.8f : 1f);
        }
    }

    private void Fire(BossAttack attack, Vector3 toPlayer, float distance)
    {
        var element = ElementOf(attack);
        var health = PlayerHealth.Instance;
        switch (attack.kind)
        {
            case AttackKind.Melee:
                GameAudio.Play("Sfx/Swing", transform.position + Vector3.up * 2f, 1f, 0.7f);
                if (distance <= attack.radius + 1f && Vector3.Angle(transform.forward, toPlayer) < 75f && health != null)
                    health.TakeDamage(attack.damage, element);
                break;
            case AttackKind.Projectile:
                int count = attack.stream ? 1 : Mathf.Max(1, attack.count);
                for (int i = 0; i < count; i++) Shoot(attack, count == 1 ? 0f : Mathf.Lerp(-12f, 12f, i / (count - 1f)));
                if (attack.stream) streamFired = 1;
                break;
            case AttackKind.Ground:
                // One light and one sound for the whole attack, on its middle blast: a spike line used to light and play
                // every circle, which tanked the frame rate and stacked the same impact sound many times over.
                for (int i = 0; i < pendingBlasts.Count; i++) Blast(pendingBlasts[i], attack, element, i == pendingBlasts.Count / 2);
                ClearTelegraphs();
                break;
        }
    }

    private void Shoot(BossAttack attack, float spread)
    {
        var element = ElementOf(attack);
        var origin = transform.position + Vector3.up * (target.AimPoint.y - transform.position.y + 0.6f) + transform.forward * 1.5f;
        var aim = player.position + Vector3.up * 1.2f;
        aim = transform.position + Quaternion.Euler(0f, spread, 0f) * (aim - transform.position);
        BossProjectile.Launch(spells != null ? spells.ProjectilePrefab(element) : null, origin, aim, element,
            attack.damage, attack.radius, attack.lobbed, gameObject.scene, spells != null ? spells.AreaPrefab(element) : null);
    }

    // The ground strike: the player's own area effect for that element, scaled to the blast. Earth's
    // effect is a line of spikes running forward, so it is aimed away from the boss (a spike line runs
    // toward the player), and a slam centred on the boss repeats it all the way round to fill its ring.
    // The other elements' effects are round.
    private void Blast(Vector3 point, BossAttack attack, ElementType element, bool lead)
    {
        var prefab = spells != null ? spells.AreaPrefab(element) : null;
        if (prefab != null)
        {
            var away = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            var facing = Quaternion.LookRotation(away.sqrMagnitude > 1f ? away : transform.forward);
            int copies = element == ElementType.Earth && attack.atSelf ? 6 : 1;
            for (int i = 0; i < copies; i++)
            {
                var effect = Instantiate(prefab, point + Vector3.up * 0.02f, facing * Quaternion.Euler(0f, i * 360f / copies, 0f));
                effect.transform.localScale *= attack.radius / 5f;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
                TerrainAoE.AddTo(effect, element, transform, spells.GroundMask);
                if (i == 0 && lead)
                {
                    GameAudio.PlayOwn(effect);
                    SpellElementLight.AddTo(effect, element, true, 8);
                }
                else SpellElementLight.Mute(effect);
                Destroy(effect, 5f);
            }
        }
        var health = PlayerHealth.Instance;
        if (health != null && Vector3.ProjectOnPlane(player.position - point, Vector3.up).magnitude <= attack.radius &&
            Mathf.Abs(player.position.y - point.y) < 3f)
            health.TakeDamage(attack.damage, element);
    }

    private ElementType ElementOf(BossAttack attack) => attack.element != ElementType.None ? attack.element : target.Affinity;

    private GameObject Telegraph(Vector3 point, float radius, ElementType element) => Telegraph(point, radius, element, gameObject.scene);

    // The warning ring: the element's colour over a dark rim, so it reads on bright grass, snow and dark rock alike.
    // Fire's is white-hot rather than orange, which vanished on the caldera's ember-lit rock.
    public static GameObject Telegraph(Vector3 point, float radius, ElementType element, UnityEngine.SceneManagement.Scene scene)
    {
        if (telegraphMaterial == null) telegraphMaterial = Resources.Load<Material>("BossTelegraph");
        var color = element == ElementType.Fire ? new Color(1f, 0.95f, 0.6f) : ElementVisuals.ColorFor(element);
        var ring = Ring(point + Vector3.up * 0.12f, radius * 2f, color, "Boss telegraph", scene);
        Ring(point + Vector3.up * 0.1f, radius * 2.15f, new Color(0f, 0f, 0f, 0.8f), "Rim", scene).transform.SetParent(ring.transform, true);
        return ring;
    }

    private static GameObject Ring(Vector3 at, float size, Color color, string name, UnityEngine.SceneManagement.Scene scene)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(ring.GetComponent<Collider>());
        ring.name = name;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ring, scene);
        ring.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));
        ring.transform.localScale = Vector3.one * size;
        var renderer = ring.GetComponent<MeshRenderer>();
        if (telegraphMaterial != null) renderer.sharedMaterial = telegraphMaterial;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ring;
    }

    private void ClearTelegraphs()
    {
        foreach (var ring in telegraphs) if (ring != null) Destroy(ring);
        telegraphs.Clear();
    }

    public static Vector3 Ground(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<CombatTarget>() == null && hit.collider.GetComponentInParent<CharacterController>() == null)
            return hit.point;
        var terrain = Terrain.activeTerrain;
        if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    // A weakness hit makes the boss flinch, if it has a hit-reaction animation and isn't mid-attack.
    private void OnHit(ElementType element, float damage)
    {
        FillStagger(damage);
        if (string.IsNullOrEmpty(hitState) || current != null || target.IsDead) return;
        if (CombatTarget.Multiplier(element, target.Affinity) > 1f) { Play(hitState, 0.1f); busyUntil = Time.time + 0.6f; }
    }

    private void FillStagger(float damage)
    {
        lastHit = Time.time;
        if (!engaged || target.IsDead || StaggerResting) return;
        stagger += damage * StaggerPerDamage;
        if (stagger < StaggerMax) return;
        stagger = 0f;
        staggerEnds = Time.time + StaggerSeconds;
        target.Stagger(StaggerSeconds);
        GameAudio.Play("Sfx/Impact", target.AimPoint, 1f, 0.7f);
        StaggerBroken?.Invoke();
    }

    private void Face(Vector3 direction)

    {
        if (direction.sqrMagnitude > 0.01f)
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction.normalized), turnSpeed * speedScale * Time.fixedDeltaTime));
    }

    private const float Walk = 1f, Run = 2f;

    // Gait for the Locomotion blend (0 idle, 1 walk, 2 run); root motion turns it into movement.
    private void SetGait(float gait) => animator.SetFloat("Speed", gait, 0.3f, Time.fixedDeltaTime);

    private void Play(string state, float blend)
    {
        if (animator == null || string.IsNullOrEmpty(state)) return;
        if (state == roarState) GameAudio.Play("Sfx/Roar", transform.position + Vector3.up * 2f, 1f, 1.15f - transform.localScale.y * 0.1f);
        if (animator.HasState(0, Animator.StringToHash("Base Layer." + state))) animator.CrossFadeInFixedTime(state, blend, 0);
    }
}
