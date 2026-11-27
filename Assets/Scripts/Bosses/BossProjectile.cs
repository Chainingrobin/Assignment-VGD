using UnityEngine;
using UnityEngine.SceneManagement;

// A boss's element shot (fireball, ice shard, boulder). Reuses the player's projectile prefab
// for that element as the visual, but flies and hits on its own: straight or lobbed toward
// where Wulang stood, splashing on impact and damaging her through PlayerHealth. It looks like
// Wulang's own shot of that element, only larger, with a bright core and a long trail so it reads
// against any sky; a lobbed shot also marks where it will land with a warning ring.
public class BossProjectile : MonoBehaviour
{
    private const float Speed = 18f;
    private const float Lifetime = 6f;

    // One splash sound per volley: three shots landing together used to stack three impacts.
    private static float lastImpactSound = float.NegativeInfinity;
    private static Material glowMaterial;

    private Vector3 start, end;
    private float flightTime, age;
    private bool lobbed;
    private ElementType element;
    private float damage, splash;
    private GameObject impactPrefab, landing;

    public static void Launch(GameObject visualPrefab, Vector3 origin, Vector3 target, ElementType element, float damage,
        float splash, bool lobbed, Scene scene, GameObject impactPrefab)
    {
        var go = new GameObject($"{element} boss projectile");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = origin;
        var projectile = go.AddComponent<BossProjectile>();
        projectile.start = origin;
        projectile.end = target;
        projectile.lobbed = lobbed;
        projectile.element = element;
        projectile.damage = damage;
        projectile.splash = Mathf.Max(1.2f, splash);
        projectile.impactPrefab = impactPrefab;
        projectile.flightTime = Mathf.Max(0.4f, Vector3.Distance(origin, target) / (lobbed ? Speed * 0.7f : Speed));
        if (visualPrefab != null)
        {
            var visual = Instantiate(visualPrefab, go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale *= 2.4f;
            // Strip the player-projectile behaviour; this object owns flight and damage.
            foreach (var spell in visual.GetComponentsInChildren<SpellProjectile>()) Destroy(spell);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>()) Destroy(body);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
        }
        projectile.Highlight();
        if (lobbed) projectile.landing = BossBrain.Telegraph(BossBrain.Ground(target), projectile.splash, element, scene);
        SpellElementLight.AddTo(go, element, false);
        GameAudio.Play("Sfx/Whoosh", origin, 0.8f, lobbed ? 0.8f : 1.1f);
    }

    // A glowing core and a trail in the element's colour, so the shot is easy to track and dodge.
    private void Highlight()
    {
        if (glowMaterial == null)
        {
            var telegraph = Resources.Load<Material>("BossTelegraph");
            if (telegraph == null) return;
            glowMaterial = new Material(telegraph) { name = "Boss shot glow" };
            glowMaterial.SetTexture("_BaseMap", null);
        }
        var color = ElementVisuals.ColorFor(element);
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(core.GetComponent<Collider>());
        core.name = "Glow core";
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * 0.9f;
        var renderer = core.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.35f));
        renderer.SetPropertyBlock(block);
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = glowMaterial;
        trail.time = 0.4f;
        trail.startWidth = 0.8f;
        trail.endWidth = 0f;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / flightTime;
        var previous = transform.position;
        var next = Vector3.LerpUnclamped(start, end, t);
        if (lobbed) next.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * Mathf.Min(8f, Vector3.Distance(start, end) * 0.35f);
        var step = next - previous;
        if (step.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(step);

        // Hit the player directly, or the ground/walls along the way.
        var health = PlayerHealth.Instance;
        if (health != null && Vector3.Distance(next, health.transform.position + Vector3.up) < 1.1f) { Impact(next); return; }
        if (step.sqrMagnitude > 0f && Physics.Raycast(previous, step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<CombatTarget>() == null && hit.collider.GetComponentInParent<CharacterController>() == null)
        { Impact(hit.point); return; }
        transform.position = next;
        if (age > Lifetime) Destroy(gameObject);
    }

    private void Impact(Vector3 point)
    {
        var health = PlayerHealth.Instance;
        if (health != null && Vector3.Distance(point, health.transform.position + Vector3.up) <= splash + 0.8f)
            health.TakeDamage(damage, element);
        bool sound = Time.time - lastImpactSound > 0.3f;
        if (sound) { lastImpactSound = Time.time; GameAudio.Play("Sfx/Impact", point, 0.9f); }
        if (impactPrefab != null)
        {
            var effect = Instantiate(impactPrefab, point, Quaternion.identity);
            SpellElementLight.Mute(effect);
            if (sound) GameAudio.PlayOwn(effect);
            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            effect.transform.localScale *= splash / 5f;
            Destroy(effect, 3f);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (landing != null) Destroy(landing);
    }
}
