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
    private UnityEngine.UI.Image accent;
    private Coroutine notificationRoutine;
    private Sprite roundedSprite;
    private Texture2D roundedTexture;
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
        roundedSprite = CreateRoundedSprite();
        card = CreateRect(content, "ElementUnlockNotification", new Vector2(344f, 88f), Vector2.zero);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
        card.anchoredPosition = new Vector2(-24f, -96f);
        AddPanel(card, new Color(0.045f, 0.065f, 0.10f, 0.96f));
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;
        var accentRect = CreateRect(card, "Accent", new Vector2(3f, 48f), new Vector2(0f, -20f));
        accent = accentRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        accent.raycastTarget = false;
        var tile = CreateRect(card, "IconTile", new Vector2(48f, 48f), new Vector2(20f, -20f));
        AddPanel(tile, new Color(0.12f, 0.16f, 0.22f));
        var iconRect = CreateRect(tile, "ElementIcon", new Vector2(30f, 30f), new Vector2(9f, -9f));
        icon = iconRect.gameObject.AddComponent<ElementIcon>();
        icon.raycastTarget = false;
        CreateText(card, "Caption", "NEW ABILITY", 10f, new Vector2(86f, -15f), new Vector2(240f, 16f), new Color(0.58f, 0.66f, 0.78f));
        title = CreateText(card, "Title", "", 22f, new Vector2(86f, -31f), new Vector2(240f, 28f), Color.white);
        title.fontStyle = FontStyles.Bold;
        CreateText(card, "Description", "Added to your spellbook", 12f, new Vector2(86f, -61f), new Vector2(240f, 18f), new Color(0.65f, 0.72f, 0.82f));
        card.gameObject.SetActive(false);

        activeHUD = CreateRect(content, "ActiveElementHUD", new Vector2(254f, 80f), Vector2.zero);
        activeHUD.anchorMin = activeHUD.anchorMax = activeHUD.pivot = Vector2.zero;
        activeHUD.anchoredPosition = new Vector2(24f, 24f);
        AddPanel(activeHUD, new Color(0.045f, 0.065f, 0.10f, 0.92f));
        var equippedRect = CreateRect(activeHUD, "EquippedIcon", new Vector2(40f, 40f), new Vector2(20f, -14f));
        equippedIcon = equippedRect.gameObject.AddComponent<ElementIcon>();
        equippedIcon.raycastTarget = false;
        CreateText(activeHUD, "Caption", "ACTIVE ELEMENT", 10f, new Vector2(76f, -10f), new Vector2(168f, 18f), new Color(0.65f, 0.72f, 0.82f));
        activeElement = CreateText(activeHUD, "ElementName", "", 20f, new Vector2(76f, -28f), new Vector2(168f, 30f), Color.white);
        CreateText(activeHUD, "SwitchHint", "[Q]   SWITCH ELEMENT   [E]", 10f, new Vector2(20f, -60f), new Vector2(230f, 16f), new Color(0.65f, 0.72f, 0.82f));
        activeElement.outlineWidth = 0.15f;
        activeElement.outlineColor = new Color32(10, 16, 25, 255);
        coinsHUD = CreateRect(content, "CoinsHUD", new Vector2(160f, 56f), new Vector2(24f, -24f));
        AddPanel(coinsHUD, new Color(0.045f, 0.065f, 0.10f, 0.92f));
        var coinRect = CreateRect(coinsHUD, "CoinIcon", new Vector2(32f, 32f), new Vector2(14f, -12f));
        coinRect.gameObject.AddComponent<CoinIcon>().raycastTarget = false;
        coinCount = CreateText(coinsHUD, "Count", "0", 24f, new Vector2(58f, -12f), new Vector2(94f, 36f), Color.white);
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
        if (roundedSprite != null) Destroy(roundedSprite);
        if (roundedTexture != null) Destroy(roundedTexture);
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
            title.text = $"{element} unlocked";
            icon.Element = element;
            icon.color = ElementVisuals.ColorFor(element);
            accent.color = icon.color;
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
        string hex = ColorUtility.ToHtmlStringRGB(ElementVisuals.ColorFor(element));
        activeElement.text = $"<color=#{hex}>{(element == ElementType.None ? "None" : element.ToString())}</color>";
    }

    private void UpdateCoins(int count) => coinCount.text = count.ToString();

    private void AddPanel(RectTransform rect, Color panelColor)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = roundedSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = panelColor;
        image.raycastTarget = false;
    }

    private Sprite CreateRoundedSprite()
    {
        roundedTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        roundedTexture.name = "NotificationRoundedCorners";
        roundedTexture.wrapMode = TextureWrapMode.Clamp;
        roundedTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 15.5f) - 7.5f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 15.5f) - 7.5f, 0f);
                float alpha = Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy));
                roundedTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        roundedTexture.Apply(false, true);
        return Sprite.Create(roundedTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
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
