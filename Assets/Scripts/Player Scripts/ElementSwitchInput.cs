using UnityEngine;
using UnityEngine.InputSystem;

// Attach this to the Player alongside PlayerMagicAffinity.
public class ElementSwitchInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Gameplay";

    private InputAction cycleNext;
    private InputAction cyclePrevious;

    private void OnEnable()
    {
        var map = inputActions.FindActionMap(actionMapName);
        cycleNext = map.FindAction("CycleNext");
        cyclePrevious = map.FindAction("CyclePrevious");

        cycleNext.performed += OnCycleNext;
        cyclePrevious.performed += OnCyclePrevious;

        map.Enable();
    }

    private void OnDisable()
    {
        cycleNext.performed -= OnCycleNext;
        cyclePrevious.performed -= OnCyclePrevious;
    }

    private void OnCycleNext(InputAction.CallbackContext ctx)
    {
        PlayerMagicAffinity.Instance.CycleActiveElement(reverse: false);
    }

    private void OnCyclePrevious(InputAction.CallbackContext ctx)
    {
        PlayerMagicAffinity.Instance.CycleActiveElement(reverse: true);
    }
}
