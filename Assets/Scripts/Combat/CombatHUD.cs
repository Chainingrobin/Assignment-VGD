using UnityEngine;
using UnityEngine.UI;

// The bottom-right spell ring: the attuned element's medallion in the centre, ringed by its
// charges. The outer ring has one node per bolt (left mouse), the inner ring one segment per burst
// (right mouse), both in the element's colour, so how many shots are left reads at a glance with
// no numbers or labels. Spent nodes fade to a faint track; while reloading the bolt nodes refill
// around the circle, and the recovering burst segment fills along its arc. [Q] and [E] sit either
// side to switch element; [R] shows under the ring only when a reload would help. Full and idle,
// the ring dims until the next cast.
public class CombatHUD : MonoBehaviour
{
    [SerializeField] private SpellCaster caster;

    private ChargeRing bolts, bursts;
    private ElementIcon glyph;
    private Image halo;
    private RectTransform medallion, reloadKey;
    private CanvasGroup group;
    private ElementType shown = (ElementType)(-1);
    private float activeAt, pulse;
    private int lastBolts = -1, lastBursts = -1;

    private void Start()
    {
        var root = (RectTransform)transform;
        // The scene-authored LMB/RMB cards are replaced by the ring.
        for (int i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);
        root.anchoredPosition = new Vector2(-30f, 26f);
        root.sizeDelta = new Vector2(230f, 160f);
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var center = new Vector2(0.5f, 0.5f);

        var shade = UIKit.Rect(root, "Shade", new Vector2(200f, 200f), Vector2.zero, center);
        UIKit.Fill(shade, new Color(0f, 0f, 0f, 0.55f)).sprite = FantasyUI.Glow;
        halo = UIKit.Fill(UIKit.Rect(root, "Halo", new Vector2(150f, 150f), Vector2.zero, center), Color.clear);
        halo.sprite = FantasyUI.Glow;

        bolts = Ring(root, "Bolts", 136f, SpellCharges.ProjectileCapacity, 0.86f, 5f);
        bursts = Ring(root, "Bursts", 106f, SpellCharges.AreaCapacity, 0.84f, 10f);
        medallion = UIKit.Rect(root, "Medallion", new Vector2(76f, 76f), Vector2.zero, center);
        medallion.gameObject.AddComponent<Image>().sprite = FantasyUI.Socket;
        medallion.GetComponent<Image>().raycastTarget = false;
        glyph = UIKit.Rect(medallion, "Element", new Vector2(38f, 38f), Vector2.zero, center).gameObject.AddComponent<ElementIcon>();
        glyph.raycastTarget = false;

        UIKit.Keycap(root, "Q", new Vector2(-96f, 0f), center);
        UIKit.Keycap(root, "E", new Vector2(96f, 0f), center);
        reloadKey = UIKit.Keycap(root, "R", new Vector2(0f, -78f), center);
    }

    private static ChargeRing Ring(RectTransform parent, string name, float size, int count, float inner, float gap)
    {
        var ring = UIKit.Rect(parent, name, new Vector2(size, size), Vector2.zero, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<ChargeRing>();
        ring.Configure(count, inner, gap);
        return ring;
    }

    private void Update()
    {
        if (caster == null || bolts == null) return;
        var charges = caster.Charges;
        var affinity = PlayerMagicAffinity.Instance;
        var element = affinity != null ? affinity.ActiveElement : ElementType.None;
        var tint = ElementVisuals.ColorFor(element);
        if (element != shown)
        {
            shown = element;
            glyph.Element = element;
            glyph.color = tint;
            halo.color = element == ElementType.None ? Color.clear : tint * new Color(1f, 1f, 1f, 0.45f);
            bolts.color = bursts.color = tint;
            pulse = 1f;
            activeAt = Time.unscaledTime;
        }

        float reloaded = charges.IsReloading ? (1f - charges.ReloadRemaining / SpellCharges.ReloadDuration) * SpellCharges.ProjectileCapacity : 0f;
        for (int i = 0; i < SpellCharges.ProjectileCapacity; i++)
            bolts.SetFill(i, i < charges.Projectiles ? 1f : reloaded - i);
        for (int i = 0; i < SpellCharges.AreaCapacity; i++)
            bursts.SetFill(i, i < charges.Areas ? 1f : i == charges.Areas ? charges.RecoveryElapsed / SpellCharges.AreaRecoveryDuration : 0f);
        reloadKey.gameObject.SetActive(!charges.IsReloading && charges.Projectiles < SpellCharges.ProjectileCapacity);

        bool busy = charges.IsReloading || charges.Areas < SpellCharges.AreaCapacity || charges.Projectiles < SpellCharges.ProjectileCapacity;
        if (busy || charges.Projectiles != lastBolts || charges.Areas != lastBursts) activeAt = Time.unscaledTime;
        lastBolts = charges.Projectiles;
        lastBursts = charges.Areas;
        group.alpha = Mathf.MoveTowards(group.alpha, Time.unscaledTime - activeAt > 3f ? 0.55f : 1f, Time.unscaledDeltaTime * 3f);
        // A short swell of the medallion when the element changes.
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 3f);
        medallion.localScale = Vector3.one * (1f + 0.15f * pulse);
    }
}
