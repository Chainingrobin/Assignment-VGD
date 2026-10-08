using UnityEngine;

public class SpellElementLight : MonoBehaviour
{
    private Light spellLight;
    private float age;

    public static void AddTo(GameObject spell, ElementType element, bool area)
    {
        if (element == ElementType.None) return;

        // Use one light per cast instead of stacking prefab particle lights.
        foreach (var existing in spell.GetComponentsInChildren<Light>(true))
            existing.enabled = false;
        foreach (var particle in spell.GetComponentsInChildren<ParticleSystem>(true))
        {
            var lights = particle.lights;
            lights.enabled = false;
        }

        var glow = new GameObject("ElementLight");
        glow.transform.SetParent(spell.transform, false);
        glow.transform.position = spell.transform.position + (area ? Vector3.up * 0.75f : Vector3.zero);
        var controller = glow.AddComponent<SpellElementLight>();
        controller.spellLight = glow.AddComponent<Light>();
        controller.spellLight.type = LightType.Point;
        controller.spellLight.color = ElementVisuals.ColorFor(element);
        controller.spellLight.range = area ? 6f : 8f;
        controller.spellLight.intensity = area ? 1.5f : 5f;
        controller.spellLight.shadows = LightShadows.None;
        controller.spellLight.renderMode = LightRenderMode.ForcePixel;
        controller.enabled = area;
    }

    private void Update()
    {
        age += Time.deltaTime;
        // ponytail: AoE glow lasts four seconds; use effect-specific timing if future spells have longer lifetimes.
        spellLight.intensity = 1.5f * (1f - Mathf.Clamp01((age - 0.3f) / 3.7f));
        if (age >= 4f) Destroy(gameObject);
    }
}
