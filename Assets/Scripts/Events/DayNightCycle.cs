using UnityEngine;
using UnityEngine.Rendering;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField] private Light moon;
    [SerializeField] private Transform moonVisual;
    [SerializeField, Min(0f)] private float moonIntensity = 0.25f;
    [SerializeField] private Color moonlightColor = new Color(0.65f, 0.75f, 1f);
    [SerializeField, Range(0.1f, 10f)] private float moonSizeDegrees = 2f;
    [Tooltip("Real minutes for a full 24-hour day and night.")]
    [SerializeField, Min(0.1f)] private float cycleLengthMinutes = 10f;
    [SerializeField, Range(0f, 24f)] private float timeOfDay = 12f;
    [SerializeField] private bool runCycle = true;
    [SerializeField, Min(0f)] private float dayIntensity = 2f;
    [SerializeField, Min(0f)] private float nightIntensity = 0.03f;
    [SerializeField] private Color daylightColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color sunsetColor = new Color(1f, 0.55f, 0.3f);
    [SerializeField] private Color nightColor = new Color(0.35f, 0.45f, 0.7f);
    [SerializeField] private Color dayAmbient = new Color(0.45f, 0.5f, 0.6f);
    [SerializeField] private Color nightAmbient = new Color(0.025f, 0.035f, 0.065f);
    [SerializeField, Min(0f)] private float daySkyExposure = 1f;
    [SerializeField, Min(0f)] private float nightSkyExposure = 0.08f;
    [SerializeField, Min(0f)] private float dayReflectionIntensity = 1f;
    [SerializeField, Min(0f)] private float nightReflectionIntensity = 0.02f;
    private Material originalSky;
    private Material runtimeSky;
    private AmbientMode originalAmbientMode;
    private Color originalAmbient;
    private Color originalFog;
    private Quaternion originalSunRotation;
    private Color originalSunColor;
    private float originalSunIntensity;
    private float originalReflectionIntensity;
    private SphericalHarmonicsL2 originalAmbientProbe;
    private Light originalRenderSun;

    private void OnEnable()
    {
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null) { Debug.LogError("DayNightCycle needs a directional sun.", this); enabled = false; return; }
        originalSunRotation = sun.transform.rotation;
        originalRenderSun = RenderSettings.sun;
        originalSunColor = sun.color;
        originalSunIntensity = sun.intensity;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbient = RenderSettings.ambientLight;
        originalAmbientProbe = RenderSettings.ambientProbe;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        originalFog = RenderSettings.fogColor;
        originalSky = RenderSettings.skybox;
        if (originalSky != null)
        {
            runtimeSky = new Material(originalSky);
            RenderSettings.skybox = runtimeSky;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        ApplyLighting();
    }

    private void Update()
    {
        if (runCycle) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * 24f / (Mathf.Max(0.1f, cycleLengthMinutes) * 60f), 24f);
        ApplyLighting();
    }

    private void LateUpdate()
    {
        var camera = Camera.main;
        if (moon == null || moonVisual == null || camera == null) return;
        float distance = camera.farClipPlane * 0.9f;
        moonVisual.position = camera.transform.position - moon.transform.forward * distance;
        moonVisual.rotation = camera.transform.rotation;
        moonVisual.localScale = Vector3.one * (2f * distance * Mathf.Tan(moonSizeDegrees * Mathf.Deg2Rad * 0.5f));
    }

    private void ApplyLighting()
    {
        if (sun == null) return;
        float daylight = Mathf.Clamp01(Mathf.Sin((timeOfDay - 6f) * Mathf.PI / 12f));
        sun.transform.rotation = Quaternion.Euler(timeOfDay * 15f - 90f, 170f, 0f);
        sun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, daylight);
        sun.color = daylight > 0f ? Color.Lerp(sunsetColor, daylightColor, Mathf.Clamp01(daylight * 3f)) : nightColor;
        if (moon != null)
        {
            float moonHeight = Mathf.Clamp01(-Mathf.Sin((timeOfDay - 6f) * Mathf.PI / 12f));
            moon.transform.rotation = Quaternion.Euler(timeOfDay * 15f + 90f, 170f, 0f);
            moon.intensity = moonIntensity * moonHeight;
            moon.color = moonlightColor;
            RenderSettings.sun = moonHeight > 0f ? moon : sun;
            if (moonVisual != null) moonVisual.gameObject.SetActive(moonHeight > 0f);
        }
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight);
        RenderSettings.reflectionIntensity = Mathf.Lerp(nightReflectionIntensity, dayReflectionIntensity, daylight);
        // The foliage graph reads spherical harmonics through its Baked GI node.
        var ambientProbe = new SphericalHarmonicsL2();
        ambientProbe.AddAmbientLight(RenderSettings.ambientLight.linear);
        RenderSettings.ambientProbe = ambientProbe;
        RenderSettings.fogColor = Color.Lerp(nightAmbient, originalFog, daylight);
        if (runtimeSky != null && runtimeSky.HasProperty("_Exposure"))
            runtimeSky.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, daySkyExposure, daylight));
    }

    private void OnDisable()
    {
        if (sun == null) return;
        sun.transform.rotation = originalSunRotation;
        sun.color = originalSunColor;
        sun.intensity = originalSunIntensity;
        RenderSettings.sun = originalRenderSun;
        if (moon != null) moon.intensity = 0f;
        if (moonVisual != null) moonVisual.gameObject.SetActive(false);
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbient;
        RenderSettings.ambientProbe = originalAmbientProbe;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        RenderSettings.fogColor = originalFog;
        if (runtimeSky != null) { RenderSettings.skybox = originalSky; Destroy(runtimeSky); }
    }
}
