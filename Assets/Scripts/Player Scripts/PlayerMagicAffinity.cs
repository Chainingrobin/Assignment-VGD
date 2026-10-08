using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Attach this to the Player GameObject.
// Combat/spell scripts should subscribe to OnActiveElementChanged instead of
// polling this every frame.
public class PlayerMagicAffinity : MonoBehaviour
{
    private static PlayerMagicAffinity _instance;
    public static PlayerMagicAffinity Instance
    {
        get
        {
            if (_instance == null)
            {
                // Self-heals if the static reference was ever wiped
                // (e.g. a mid-Play-Mode script recompile).
                _instance = FindFirstObjectByType<PlayerMagicAffinity>();
                if (_instance == null)
                    Debug.LogError("No PlayerMagicAffinity found in the scene. " +
                        "Make sure it's on your Player and the Player is active.");
            }
            return _instance;
        }
    }

    [SerializeField] private ElementType startingElement = ElementType.None;

    private readonly HashSet<ElementType> unlockedElements = new HashSet<ElementType>();
    private ElementType activeElement = ElementType.None;

    public ElementType ActiveElement => activeElement;
    public IReadOnlyCollection<ElementType> UnlockedElements => unlockedElements;

    // Subscribe from your spell-casting / UI scripts
    public event Action<ElementType> OnActiveElementChanged;
    public event Action<ElementType> OnElementUnlocked;

    private void Awake()
    {
        // Simple singleton so BookCollect can reach this without a scene reference.
        // Fine for a single-player prototype; revisit if you ever need multiplayer.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (FindFirstObjectByType<ElementUnlockNotification>() == null)
            gameObject.AddComponent<ElementUnlockNotification>();

        if (startingElement != ElementType.None)
        {
            UnlockElement(startingElement);
            SetActiveElement(startingElement);
        }
    }

    public bool HasElement(ElementType element) => unlockedElements.Contains(element);

    public void UnlockElement(ElementType element)
    {
        if (element == ElementType.None) return;

        bool isNew = unlockedElements.Add(element);
        if (isNew)
        {
            OnElementUnlocked?.Invoke(element);

            // First book you ever pick up auto-equips, so the player
            // isn't stuck with "None" and no way to cast anything.
            if (activeElement == ElementType.None)
                SetActiveElement(element);
        }
    }

    public void SetActiveElement(ElementType element)
    {
        if (!unlockedElements.Contains(element))
        {
            Debug.LogWarning($"Tried to switch to {element} but it isn't unlocked yet.");
            return;
        }

        if (activeElement == element) return;

        activeElement = element;
        OnActiveElementChanged?.Invoke(activeElement);
    }

    // Cycles between unlocked elements in enum order.
    // reverse = false -> next element (E key / scroll up)
    // reverse = true  -> previous element (Q key / scroll down)
    public void CycleActiveElement(bool reverse = false)
    {
        if (unlockedElements.Count <= 1) return;

        var ordered = unlockedElements.OrderBy(e => (int)e).ToList();
        int currentIndex = ordered.IndexOf(activeElement);
        int step = reverse ? -1 : 1;
        int nextIndex = (currentIndex + step + ordered.Count) % ordered.Count;
        SetActiveElement(ordered[nextIndex]);
    }
}
