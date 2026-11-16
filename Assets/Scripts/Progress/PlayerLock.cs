using StarterAssets;
using UnityEngine;

// Pauses the player's movement, look, spells and interaction while something else owns
// the screen (dialogue, shop, death, region transitions). Calls nest: control only returns
// when every Push has been matched by a Pop.
public static class PlayerLock
{
    private static int locks;
    private static int cursorLocks;
    public static bool IsLocked => locks > 0;

    public static void Push(bool showCursor = false)
    {
        if (locks++ == 0) Apply(false);
        if (showCursor && cursorLocks++ == 0) SetCursor(true);
    }

    public static void Pop(bool showCursor = false)
    {
        if (showCursor && cursorLocks > 0 && --cursorLocks == 0) SetCursor(false);
        if (locks > 0 && --locks == 0) Apply(true);
    }

    // Scene changes destroy the player, so stale counts must not survive them.
    public static void Reset()
    {
        locks = cursorLocks = 0;
        SetCursor(false);
    }

    private static void Apply(bool enabled)
    {
        var controller = Object.FindFirstObjectByType<FirstPersonController>();
        if (controller == null) return;
        controller.enabled = enabled;
        var root = controller.transform;
        foreach (var behaviour in new Behaviour[]
        {
            root.GetComponentInChildren<SpellCastInput>(), root.GetComponentInChildren<ElementSwitchInput>(),
            root.GetComponentInChildren<PlayerInteract>(),
        })
            if (behaviour != null) behaviour.enabled = enabled;
        var inputs = root.GetComponent<StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.move = inputs.look = Vector2.zero;
            inputs.jump = inputs.sprint = false;
        }
    }

    private static void SetCursor(bool visible)
    {
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
        var inputs = Object.FindFirstObjectByType<StarterAssetsInputs>();
        if (inputs != null) inputs.cursorInputForLook = inputs.cursorLocked = !visible;
    }
}
