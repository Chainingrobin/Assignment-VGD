using UnityEngine;

// The final boss's gimmick: he fights with all four stolen elements, one per phase
// (Lightning, Earth, Fire, Ice by default), changing at 75 / 50 / 25% health. Each change he
// blocks while the new element takes hold, his aura and the matching arena pillar light up,
// and his attacks switch to that element. Only that element's weakness hurts him properly
// (CombatTarget's non-weakness multiplier), so the player has to keep switching too.
[RequireComponent(typeof(BossBrain))]
public class ElementPhases : MonoBehaviour
{
    [SerializeField] private ElementType[] sequence = { ElementType.Lightning, ElementType.Earth, ElementType.Fire, ElementType.Ice };
    [Tooltip("One aura per element in ElementType order (Ice, Fire, Lightning, Earth); the active one is shown.")]
    [SerializeField] private GameObject[] auras = new GameObject[4];
    [Tooltip("Arena pillar lights in ElementType order; the active element's pillar shines.")]
    [SerializeField] private Light[] pillars = new Light[4];
    [SerializeField] private string blockState = "Block";
    [SerializeField, Min(0f)] private float changeDuration = 1.6f;

    private BossBrain brain;
    private CombatTarget target;
    private int phase = -1;
    private float invulnerableUntil;

    private void Awake()
    {
        brain = GetComponent<BossBrain>();
        target = GetComponent<CombatTarget>();
    }

    private void OnEnable() => SetPhase(0, false);

    private void Update()
    {
        if (target.Invulnerable && Time.time >= invulnerableUntil) target.Invulnerable = false;
        if (target.IsDead) return;
        float fraction = target.Health / target.MaxHealth;
        int wanted = Mathf.Clamp(Mathf.FloorToInt((1f - fraction) * sequence.Length), 0, sequence.Length - 1);
        // Health only drops in a fight; a rise means the fight was reset after the player died.
        if (wanted != phase) SetPhase(wanted, wanted > phase);
    }

    private void SetPhase(int index, bool animate)
    {
        phase = index;
        var element = sequence[index];
        target.SetAffinity(element);
        for (int i = 0; i < auras.Length; i++)
            if (auras[i] != null) auras[i].SetActive(i == (int)element - 1);
        for (int i = 0; i < pillars.Length; i++)
            if (pillars[i] != null) pillars[i].intensity = i == (int)element - 1 ? 6f : 0.3f;
        if (!animate) return;
        target.Invulnerable = true;
        invulnerableUntil = Time.time + changeDuration;
        brain.Interlude(blockState, changeDuration);
        Subtitle.Show($"[Ganfaul draws on {element}!]", 2.5f);
    }
}
