using UnityEngine;

public static class ElementVisuals
{
    public static Color ColorFor(ElementType element) => element switch
    {
        ElementType.Fire => new Color(1f, 0.42f, 0.12f),
        ElementType.Ice => new Color(0.3f, 0.72f, 1f),
        ElementType.Lightning => new Color(0.72f, 0.38f, 1f),
        ElementType.Earth => new Color(0.35f, 0.85f, 0.46f),
        _ => new Color(0.7f, 0.75f, 0.83f)
    };
}
