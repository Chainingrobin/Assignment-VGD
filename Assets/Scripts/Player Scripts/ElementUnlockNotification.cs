using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ElementUnlockNotification : MonoBehaviour
{
    [SerializeField] private float duration = 3f;
    private readonly Queue<ElementType> pending = new();
    private PlayerMagicAffinity affinity;
    private RectTransform card;
    private CanvasGroup cardGroup;
    private TMP_Text title;
    private ElementIcon icon;
    private UnityEngine.UI.Image glow;
    private Coroutine notificationRoutine;
    private bool ready;

    private void Awake()
    {
        // The player's own HUD canvas (other scenes, like the main menu, may have canvases too).
        var responsive = FindFirstObjectByType<ResponsiveHUD>();
        if (responsive == null)
        {
            Debug.LogError("ElementUnlockNotification: no ResponsiveHUD canvas found.", this);
            return;
        }
        var content = responsive.Content;
        // Unlock toast, top centre (where the boss bar was: a new element only ever follows a guardian's fall).
        card = CreateRect(content, "ElementUnlockNotification", new Vector2(250f, 72f), Vector2.zero);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 1f);
        card.anchoredPosition = new Vector2(0f, CardY);
        UIKit.Backing(card);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;
        icon = Medallion(card, 64f, new Vector2(4f, -4f), out glow);
        title = CreateText(card, "Title", "", 30f, new Vector2(84f, -18f), new Vector2(170f, 38f), FantasyUI.Parchment);
        title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        card.gameObject.SetActive(false);
        // The attuned element itself is the centre of the spell ring (CombatHUD).
    }

    private const float CardY = -120f;

    // Gold-rimmed socket with an element-coloured halo behind the vector icon.
    private static ElementIcon Medallion(RectTransform parent, float size, Vector2 position, out UnityEngine.UI.Image halo)
    {
        var haloRect = CreateRect(parent, "Halo", Vector2.one * size * 1.6f, position - new Vector2(size, -size) * 0.3f);
        halo = haloRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        halo.sprite = FantasyUI.Glow;
        halo.raycastTarget = false;
        var socket = CreateRect(parent, "Socket", Vector2.one * size, position);
        var socketImage = socket.gameObject.AddComponent<UnityEngine.UI.Image>();
        socketImage.sprite = FantasyUI.Socket;
        socketImage.raycastTarget = false;
        var iconRect = CreateRect(socket, "ElementIcon", Vector2.one * size * 0.5f, Vector2.one * size * 0.25f * new Vector2(1f, -1f));
        var element = iconRect.gameObject.AddComponent<ElementIcon>();
        element.raycastTarget = false;
        return element;
    }

    private void OnEnable()
    {
        affinity = PlayerMagicAffinity.Instance;
        if (affinity == null) return;
        affinity.OnElementUnlocked += ShowUnlocked;
    }

    private void Start() => ready = true;

    private void OnDisable()
    {
        if (affinity != null)
        {
            affinity.OnElementUnlocked -= ShowUnlocked;
        }
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        notificationRoutine = null;
        pending.Clear();
        if (card != null) card.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (card != null) Destroy(card.gameObject);
    }

    private void ShowUnlocked(ElementType element)
    {
        // Starting equipment is initialized before Start; only pickups show a toast.
        if (!ready || card == null || element == ElementType.None) return;
        pending.Enqueue(element);
        if (notificationRoutine == null) notificationRoutine = StartCoroutine(ShowPending());
    }

    private IEnumerator ShowPending()
    {
        while (pending.Count > 0)
        {
            var element = pending.Dequeue();
            title.text = element.ToString();
            icon.Element = element;
            icon.color = ElementVisuals.ColorFor(element);
            glow.color = icon.color * new Color(1f, 1f, 1f, 0.55f);
            cardGroup.alpha = 0f;
            card.gameObject.SetActive(true);
            card.SetAsLastSibling();
            yield return Animate(true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            yield return Animate(false);
            card.gameObject.SetActive(false);
        }
        notificationRoutine = null;
    }

    private IEnumerator Animate(bool entering)
    {
        const float animationDuration = 0.2f;
        for (float t = 0f; t < animationDuration; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / animationDuration);
            float visibility = entering ? progress : 1f - progress;
            cardGroup.alpha = visibility;
            card.anchoredPosition = new Vector2(0f, CardY + (1f - visibility) * 20f);
            yield return null;
        }
        cardGroup.alpha = entering ? 1f : 0f;
        card.anchoredPosition = new Vector2(0f, entering ? CardY : CardY + 20f);
    }



    private static RectTransform CreateRect(Transform parent, string objectName, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text CreateText(Transform parent, string objectName, string content, float fontSize, Vector2 position, Vector2 size, Color textColor)
    {
        var rect = CreateRect(parent, objectName, size, position);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = content;
        return text;
    }
}
