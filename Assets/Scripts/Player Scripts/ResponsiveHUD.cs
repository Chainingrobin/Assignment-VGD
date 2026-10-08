using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public class ResponsiveHUD : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    private Rect safeArea;
    private Vector2 screenSize;
    public RectTransform Content { get { if (content == null) Initialize(); return content; } }

    private void Awake() => Initialize();
    private void Initialize()
    {
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (content == null)
        {
            content = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
        }
        ApplySafeArea();
    }

    private void Update()
    {
        if (safeArea != Screen.safeArea || screenSize != new Vector2(Screen.width, Screen.height)) ApplySafeArea();
    }
    private void ApplySafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        safeArea = Screen.safeArea;
        screenSize = new Vector2(Screen.width, Screen.height);
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;
        content.anchorMin = safeArea.min / screenSize;
        content.anchorMax = safeArea.max / screenSize;
        content.offsetMin = content.offsetMax = Vector2.zero;
    }
}
