using System.Collections.Generic;
using UnityEngine;

// Ground particles are positioned first, then their lights follow in LateUpdate.
[DefaultExecutionOrder(110)]
public class SpellElementLight : MonoBehaviour
{
    private ParticleSystem source;
    private ParticleSystem.Particle[] particles;
    private readonly List<Light> lights = new List<Light>();
    private Color color;
    private int limit;
    private float range, intensity, height;
    private ParticleSystem rootSystem;
    private float age;

    public static void AddTo(GameObject spell, ElementType element, bool area)
    {
        if (element == ElementType.None) return;
        foreach (var existing in spell.GetComponentsInChildren<Light>(true)) existing.enabled = false;
        foreach (var particle in spell.GetComponentsInChildren<ParticleSystem>(true))
        {
            var module = particle.lights;
            module.enabled = false;
        }
        var glow = new GameObject("ElementLight");
        glow.transform.SetParent(spell.transform, false);
        var controller = glow.AddComponent<SpellElementLight>();
        controller.color = ElementVisuals.ColorFor(element);
        controller.range = area ? (element == ElementType.Fire ? 20f : 16f) : 8f;
        controller.intensity = area ? (element == ElementType.Fire ? 9f : 5f) : 5f;
        if (!area)
        {
            controller.CreateLight().enabled = true;
            controller.enabled = false;
            return;
        }
        controller.source = spell.GetComponent<ParticleSystem>();
        controller.rootSystem = controller.source;
        foreach (var particle in spell.GetComponentsInChildren<ParticleSystem>())
            if ((element == ElementType.Ice && particle.name == "Ice") ||
                (element == ElementType.Earth && particle.name == "Spikes")) controller.source = particle;
        if (controller.source == null) { Destroy(glow); return; }
        controller.particles = new ParticleSystem.Particle[controller.source.main.maxParticles];
        // shortcut: sample dense crystal trails with 24 lights; raise this only after profiling target hardware.
        controller.limit = element == ElementType.Fire ? controller.source.main.maxParticles : 24;
        controller.height = element == ElementType.Fire ? 0f : 0.8f;
    }

    private Light CreateLight()
    {
        var lightObject = new GameObject("Particle glow");
        lightObject.transform.SetParent(transform, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        lights.Add(light);
        return light;
    }

    private void LateUpdate()
    {
        if (source == null) return;
        int count = source.GetParticles(particles);
        age += Time.deltaTime;
        if (count == 0 && age > 0.5f && rootSystem != null && !rootSystem.IsAlive(true))
        {
            Destroy(transform.parent.gameObject);
            return;
        }
        int used = Mathf.Min(count, limit);
        while (lights.Count < used) CreateLight();
        for (int i = 0; i < lights.Count; i++)
        {
            var light = lights[i];
            light.enabled = i < used;
            if (i >= used) continue;
            int index = used <= 1 ? 0 : Mathf.RoundToInt(i * (count - 1f) / (used - 1f));
            var particle = particles[index];
            light.transform.position = ParticleWorldPosition(source, particle.position) + Vector3.up * height;
            light.intensity = intensity * particle.GetCurrentColor(source).a / 255f;
        }
    }

    internal static Vector3 ParticleWorldPosition(ParticleSystem system, Vector3 position)
    {
        var main = system.main;
        if (main.simulationSpace == ParticleSystemSimulationSpace.World) return position;
        var space = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : system.transform;
        return space != null ? space.TransformPoint(position) : position;
    }
}
