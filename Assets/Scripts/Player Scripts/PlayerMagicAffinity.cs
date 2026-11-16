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

    [SerializeField] private ElementType startingElement = ElementType.Lightning;

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

        // Elements earned earlier in this playthrough (or required by the region being
        // played) are story flags, so they survive scene loads and chapter select.
        var story = StoryStateManager.Instance;
        foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
            if (element != ElementType.None && story.HasFlag(Flags.Has(element))) UnlockElement(element);
        story.OnFlagSet += OnFlagSet;
    }

    private void OnDestroy()
    {
        var story = StoryStateManager.Instance;
        if (story != null) story.OnFlagSet -= OnFlagSet;
    }

    private void OnFlagSet(string flag)
    {
        foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
            if (element != ElementType.None && flag == Flags.Has(element)) UnlockElement(element);
    }

    public bool HasElement(ElementType element) => unlockedElements.Contains(element);

    public void UnlockElement(ElementType element)
    {
        if (element == ElementType.None) return;

        bool isNew = unlockedElements.Add(element);
        if (isNew)
        {
            StoryStateManager.Instance.SetFlag(Flags.Has(element));
            OnElementUnlocked?.Invoke(element);

            // First book you ever pick up auto-equips, so the player
            // isn't stuck with "None" and no way to cast anything.
            if (activeElement == ElementType.None)
                SetActiveElement(element);
        }
    }

    // The submission ending: Ganfaul takes the elements back one by one.
    public void Revoke(ElementType element)
    {
        if (!unlockedElements.Remove(element)) return;
        if (activeElement == element)
        {
            activeElement = unlockedElements.Count > 0 ? unlockedElements.First() : ElementType.None;
            OnActiveElementChanged?.Invoke(activeElement);
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
