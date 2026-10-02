using UnityEngine;
using UnityEngine.InputSystem;

// Attach alongside SpellCaster on the Player.
public class SpellCastInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Gameplay";
    [SerializeField] private SpellCaster spellCaster;

    private InputAction castAction;

    private void OnEnable()
    {
        var map = inputActions.FindActionMap(actionMapName);
        castAction = map.FindAction("Cast");
        castAction.performed += OnCast;
        map.Enable();
    }

    private void OnDisable()
    {
        castAction.performed -= OnCast;
    }

    private void OnCast(InputAction.CallbackContext ctx)
    {
        spellCaster.CastSpell();
    }
}
