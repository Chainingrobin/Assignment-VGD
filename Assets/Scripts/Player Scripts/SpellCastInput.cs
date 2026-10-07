using UnityEngine;
using UnityEngine.InputSystem;

public class SpellCastInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Gameplay";
    [SerializeField] private string projectileActionName = "CastProjectile";
    [SerializeField] private string aoeActionName = "CastAoE";
    [SerializeField] private SpellCaster spellCaster;

    private InputAction projectileAction, aoeAction;

    private void OnEnable()
    {
        var map = inputActions.FindActionMap(actionMapName, true);
        projectileAction = map.FindAction(projectileActionName, true);
        aoeAction = map.FindAction(aoeActionName, true);
        projectileAction.performed += OnProjectile;
        aoeAction.performed += OnAoE;
        map.Enable();
    }

    private void OnDisable()
    {
        if (projectileAction != null) projectileAction.performed -= OnProjectile;
        if (aoeAction != null) aoeAction.performed -= OnAoE;
    }

    private void OnProjectile(InputAction.CallbackContext ctx) => spellCaster.CastProjectile();
    private void OnAoE(InputAction.CallbackContext ctx) => spellCaster.CastAoE();
}