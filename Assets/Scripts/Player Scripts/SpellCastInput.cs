using UnityEngine;
using UnityEngine.InputSystem;

public class SpellCastInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Gameplay";
    [SerializeField] private string projectileActionName = "CastProjectile";
    [SerializeField] private string aoeActionName = "CastAoE";
    [SerializeField] private SpellCaster spellCaster;

    private InputAction projectileAction, aoeAction, reloadAction;

    private void OnEnable()
    {
        var map = inputActions.FindActionMap(actionMapName, true);
        projectileAction = map.FindAction(projectileActionName, true);
        aoeAction = map.FindAction(aoeActionName, true);
        reloadAction = map.FindAction("Reload", true);
        projectileAction.performed += OnProjectile;
        aoeAction.performed += OnAoE;
        reloadAction.performed += OnReload;
        map.Enable();
    }

    private void OnDisable()
    {
        if (projectileAction != null) projectileAction.performed -= OnProjectile;
        if (aoeAction != null) aoeAction.performed -= OnAoE;
        if (reloadAction != null) reloadAction.performed -= OnReload;
    }

    private void OnProjectile(InputAction.CallbackContext ctx) => spellCaster.CastProjectile();
    private void OnAoE(InputAction.CallbackContext ctx) => spellCaster.CastAoE();
    private void OnReload(InputAction.CallbackContext ctx) => spellCaster.Reload();
}
