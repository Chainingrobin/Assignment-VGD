using UnityEngine;
using UnityEngine.Rendering;

// The Sanctum's fixed sky. It floats outside the Vale's days, so while its region is loaded the World's
// day/night cycle is paused and the sky, sun, ambient light and fog are held at one stormy dusk: a
// magic-storm skybox and a low violet sun that still lights the platform well. Broad shafts of light
// (unlit beams) slant down onto the platform. Leaving the region puts everything back and the cycle
// carries on.
public class ArenaAtmosphere : MonoBehaviour
{
    [SerializeField] private Material skybox;
    [SerializeField] private Material shaftMaterial;
    [SerializeField] private Vector3 sunEuler = new(22f, 210f, 0f);
    [SerializeField] private Color sunColor = new(0.9f, 0.82f, 1f);
    [SerializeField, Min(0f)] private float sunIntensity = 1.6f;
    [SerializeField] private Color ambient = new(0.36f, 0.33f, 0.48f);

    private DayNightCycle cycle;
    private Light sun;
    private Material savedSky;
    private AmbientMode savedMode;
    private Color savedAmbient, savedSunColor;
    private SphericalHarmonicsL2 savedProbe;
    private float savedSunIntensity;
    private Quaternion savedSunRotation;
    private GameObject shafts;

    private void OnEnable()
    {
        cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle != null) cycle.enabled = false; // restores its own originals first
        sun = RenderSettings.sun;
        savedSky = RenderSettings.skybox;
        savedMode = RenderSettings.ambientMode;
        savedAmbient = RenderSettings.ambientLight;
        savedProbe = RenderSettings.ambientProbe;
        if (sun != null)
        {
            savedSunRotation = sun.transform.rotation;
            savedSunColor = sun.color;
            savedSunIntensity = sun.intensity;
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }
        if (skybox != null) RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        var probe = new SphericalHarmonicsL2();
        probe.AddAmbientLight(ambient.linear); // the foliage graph reads ambient through spherical harmonics
        RenderSettings.ambientProbe = probe;
        Shader.SetGlobalVector("_CartoonSunDirection", sun != null ? -sun.transform.forward : Vector3.up);
        BuildShafts();
    }

    // Broad, faint beams slanting down from the storm onto the platform: the "light through haze" look of
    // volumetric light (URP has none built in), at the cost of a few transparent cylinders.
    private void BuildShafts()
    {
        if (shaftMaterial == null || shafts != null) return;
        shafts = new GameObject("Light shafts");
        shafts.transform.SetParent(transform, false);
        var center = transform.position;
        // Each shaft's axis leans halfway from straight up toward the sun.
        var axis = Vector3.Slerp(Vector3.up, -(Quaternion.Euler(sunEuler) * Vector3.forward), 0.5f).normalized;
        var tilt = Quaternion.FromToRotation(Vector3.up, axis);
        var spots = new[] { new Vector2(0f, 0f), new Vector2(11f, 6f), new Vector2(-9f, 9f), new Vector2(-6f, -12f), new Vector2(13f, -9f) };
        for (int i = 0; i < spots.Length; i++)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Shaft";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(shafts.transform, false);
            float width = i == 0 ? 14f : 6f + i;
            beam.transform.localScale = new Vector3(width, 40f, width);
            // The cylinder's foot rests on the platform (brightest there); it leans toward the sun.
            beam.transform.rotation = tilt;
            beam.transform.position = center + new Vector3(spots[i].x, 0f, spots[i].y) + axis * 40f;

            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shaftMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void OnDisable()
    {
        if (sun != null)
        {
            sun.transform.rotation = savedSunRotation;
            sun.color = savedSunColor;
            sun.intensity = savedSunIntensity;
        }
        RenderSettings.skybox = savedSky;
        RenderSettings.ambientMode = savedMode;
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.ambientProbe = savedProbe;
        if (cycle != null) cycle.enabled = true;
        if (shafts != null) Destroy(shafts);
    }
}
