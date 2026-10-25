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
    private TMP_Text activeElement;
    private RectTransform activeHUD;
    private ElementIcon equippedIcon;
    private ElementIcon icon;
    private UnityEngine.UI.Image equippedGlow;
    private UnityEngine.UI.Image glow;
    private Coroutine notificationRoutine;
    private bool ready;
    private RectTransform coinsHUD;
    private TMP_Text coinCount;
    private PlayerCoins wallet;

    private void Awake()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("ElementUnlockNotification: no Canvas found.", this);
            return;
        }
        var responsive = canvas.GetComponent<ResponsiveHUD>();
        if (responsive == null) responsive = canvas.gameObject.AddComponent<ResponsiveHUD>();
        var content = responsive.Content;
        card = CreateRect(content, "ElementUnlockNotification", new Vector2(372f, 96f), Vector2.zero);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
        card.anchoredPosition = new Vector2(-24f, -96f);
        FantasyUI.Panel(card);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;
        icon = Medallion(card, 64f, new Vector2(16f, -16f), out glow);
        CreateText(card, "Caption", "A New Power Awakens", 12f, new Vector2(94f, -14f), new Vector2(260f, 18f), FantasyUI.Gold);
        title = CreateText(card, "Title", "", 24f, new Vector2(94f, -32f), new Vector2(260f, 30f), FantasyUI.Parchment);
        title.fontStyle = FontStyles.Bold;
        Divider(card, new Vector2(94f, -62f), 200f);
        CreateText(card, "Description", "Inscribed in your spellbook", 12f, new Vector2(94f, -70f), new Vector2(260f, 18f), FantasyUI.Muted);
        card.gameObject.SetActive(false);

        activeHUD = CreateRect(content, "ActiveElementHUD", new Vector2(276f, 92f), Vector2.zero);
        activeHUD.anchorMin = activeHUD.anchorMax = activeHUD.pivot = Vector2.zero;
        activeHUD.anchoredPosition = new Vector2(24f, 24f);
        FantasyUI.Panel(activeHUD);
        equippedIcon = Medallion(activeHUD, 62f, new Vector2(15f, -15f), out equippedGlow);
        CreateText(activeHUD, "Caption", "Attuned Element", 12f, new Vector2(90f, -13f), new Vector2(176f, 18f), FantasyUI.Muted);
        activeElement = CreateText(activeHUD, "ElementName", "", 24f, new Vector2(90f, -30f), new Vector2(176f, 32f), Color.white);
        activeElement.fontStyle = FontStyles.Bold;
        Divider(activeHUD, new Vector2(90f, -54f), 168f);
        CreateText(activeHUD, "SwitchHint", "<color=#E8C77F>Q</color>   Switch   <color=#E8C77F>E</color>", 12f, new Vector2(90f, -66f), new Vector2(168f, 18f), FantasyUI.Muted).alignment = TextAlignmentOptions.Top;
        activeElement.outlineWidth = 0.2f;
        activeElement.outlineColor = new Color32(12, 7, 4, 255);
        coinsHUD = CreateRect(content, "CoinsHUD", new Vector2(150f, 56f), new Vector2(24f, -24f));
        FantasyUI.Panel(coinsHUD);
        var coinRect = CreateRect(coinsHUD, "CoinIcon", new Vector2(32f, 32f), new Vector2(14f, -12f));
        coinRect.gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        coinCount = CreateText(coinsHUD, "Count", "0", 26f, new Vector2(56f, -10f), new Vector2(84f, 36f), FantasyUI.Parchment);
    }

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

    private static void Divider(RectTransform parent, Vector2 position, float width)
    {
        var image = CreateRect(parent, "Divider", new Vector2(width, 12f), position).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = FantasyUI.Divider;
        image.raycastTarget = false;
    }

    private void OnEnable()
    {
        affinity = PlayerMagicAffinity.Instance;
        if (affinity == null) return;
        affinity.OnElementUnlocked += ShowUnlocked;
        affinity.OnActiveElementChanged += UpdateActiveElement;
        if (activeHUD != null) activeHUD.gameObject.SetActive(true);
        UpdateActiveElement(affinity.ActiveElement);
        wallet = affinity.GetComponentInParent<PlayerCoins>();
        if (wallet != null) { wallet.OnChanged += UpdateCoins; UpdateCoins(wallet.Count); }
        if (coinsHUD != null) coinsHUD.gameObject.SetActive(true);
    }

    private void Start() => ready = true;

    private void OnDisable()
    {
        if (wallet != null) wallet.OnChanged -= UpdateCoins;
        if (coinsHUD != null) coinsHUD.gameObject.SetActive(false);
        if (affinity != null)
        {
            affinity.OnElementUnlocked -= ShowUnlocked;
            affinity.OnActiveElementChanged -= UpdateActiveElement;
        }
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        notificationRoutine = null;
        pending.Clear();
        if (card != null) card.gameObject.SetActive(false);
        if (activeHUD != null) activeHUD.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (card != null) Destroy(card.gameObject);
        if (activeHUD != null) Destroy(activeHUD.gameObject);
        if (coinsHUD != null) Destroy(coinsHUD.gameObject);
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
            title.text = $"{element} Unlocked";
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
            card.anchoredPosition = new Vector2(-24f + (1f - visibility) * 20f, -96f);
            yield return null;
        }
        cardGroup.alpha = entering ? 1f : 0f;
        card.anchoredPosition = new Vector2(entering ? -24f : -4f, -96f);
    }

    private void UpdateActiveElement(ElementType element)
    {
        if (activeElement == null) return;
        equippedIcon.Element = element;
        equippedIcon.color = ElementVisuals.ColorFor(element);
        equippedGlow.color = element == ElementType.None ? Color.clear : equippedIcon.color * new Color(1f, 1f, 1f, 0.5f);
        string hex = ColorUtility.ToHtmlStringRGB(ElementVisuals.ColorFor(element));
        activeElement.text = $"<color=#{hex}>{(element == ElementType.None ? "None" : element.ToString())}</color>";
    }

    private void UpdateCoins(int count) => coinCount.text = count.ToString();

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
