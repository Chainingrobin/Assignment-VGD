using TMPro;
using UnityEngine;

public class CombatHUD : MonoBehaviour
{
    [SerializeField] private SpellCaster caster;
    [SerializeField] private TMP_Text projectileCount;
    [SerializeField] private TMP_Text areaCount;
    [SerializeField] private TMP_Text reloadLabel;
    [SerializeField] private TMP_Text recoveryLabel;
    [SerializeField] private UnityEngine.UI.Image reloadFill;
    [SerializeField] private UnityEngine.UI.Image recoveryFill;
    private int previousProjectiles = -1;
    private int previousAreas = -1;
    private float projectilePulse;
    private float areaPulse;

    private void Start()
    {
        var gold = new Color(0.88f, 0.7f, 0.38f);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = gold;
            label.characterSpacing = 2f;
        }
        reloadFill.color = recoveryFill.color = gold;
    }

    private void Update()
    {
        if (caster == null) return;
        var charges = caster.Charges;
        if (previousProjectiles != charges.Projectiles) projectilePulse = 1f;
        if (previousAreas != charges.Areas) areaPulse = 1f;
        previousProjectiles = charges.Projectiles;
        previousAreas = charges.Areas;
        projectilePulse = Mathf.MoveTowards(projectilePulse, 0f, Time.deltaTime * 4f);
        areaPulse = Mathf.MoveTowards(areaPulse, 0f, Time.deltaTime * 4f);
        projectileCount.transform.localScale = Vector3.one * (1f + projectilePulse * 0.18f);
        areaCount.transform.localScale = Vector3.one * (1f + areaPulse * 0.18f);
        projectileCount.text = $"{charges.Projectiles}/15";
        areaCount.text = $"{charges.Areas}/5";
        reloadLabel.text = charges.IsReloading ? $"{charges.ReloadRemaining:0.0}s" : "R";
        recoveryLabel.text = "";
        reloadFill.transform.parent.gameObject.SetActive(charges.IsReloading);
        recoveryFill.transform.parent.gameObject.SetActive(charges.Areas < SpellCharges.AreaCapacity);
        reloadFill.fillAmount = charges.IsReloading ? 1f - charges.ReloadRemaining / SpellCharges.ReloadDuration : charges.Projectiles / 15f;
        recoveryFill.fillAmount = charges.Areas == SpellCharges.AreaCapacity ? 1f : charges.RecoveryElapsed / SpellCharges.AreaRecoveryDuration;
    }
}
