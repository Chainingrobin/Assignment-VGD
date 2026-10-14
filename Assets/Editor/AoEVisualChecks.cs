using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

public static class AoEVisualChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Tick(MonoBehaviour component) => component.GetType().GetMethod("LateUpdate", Private).Invoke(component, null);
    static readonly string Folder = "Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/";
    static GameObject cleanupEffect;

    public static void BeginCleanupCheck()
    {
        Assert.IsTrue(Application.isPlaying);
        cleanupEffect = new GameObject("AoE completed effect cleanup check");
        cleanupEffect.AddComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SpellElementLight.AddTo(cleanupEffect, ElementType.Fire, true);
        var glow = cleanupEffect.GetComponentInChildren<SpellElementLight>();
        typeof(SpellElementLight).GetField("age", Private).SetValue(glow, 1f);
        Tick(glow);
    }

    public static string FinishCleanupCheck()
    {
        Assert.IsTrue(cleanupEffect == null, "A completed AoE must remove its effect and pooled lights.");
        return "PASS: completed AoE and pooled lights cleaned up in play mode.";
    }

    public static string Run()
    {
        var center = new Vector3(-1000, 10, -1000);
        var fixture = new GameObject("AoE test fixture");
        fixture.hideFlags = HideFlags.HideAndDontSave;
        var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slope.transform.SetParent(fixture.transform);
        slope.transform.position = center - Vector3.up * 0.1f;
        slope.transform.localScale = new Vector3(30, 0.2f, 30);
        slope.transform.rotation = Quaternion.Euler(0, 0, 18);
        var raised = GameObject.CreatePrimitive(PrimitiveType.Cube);
        raised.transform.SetParent(fixture.transform);
        raised.transform.position = center + new Vector3(4, 1.5f, 0);
        raised.transform.localScale = new Vector3(2, 3, 2);
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.transform.SetParent(fixture.transform);
        ceiling.transform.position = center + Vector3.up * 12;
        ceiling.transform.localScale = new Vector3(30, 1, 30);
        GameObject ice = null, earth = null, fire = null, projectile = null;
        try
        {
            Physics.SyncTransforms();
            ice = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Ise attack.prefab"), center, Quaternion.identity);
            ice.hideFlags = HideFlags.HideAndDontSave;
            TerrainAoE.AddTo(ice, ElementType.Ice, null, 1);
            SpellElementLight.AddTo(ice, ElementType.Ice, true);
            var crystals = ice.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == "Ice");
            ice.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            crystals.Play(false);
            crystals.Emit(new ParticleSystem.EmitParams { position = center + Vector3.left * 4, startLifetime = 2, startSize = 1, startColor = Color.white }, 1);
            crystals.Emit(new ParticleSystem.EmitParams { position = center, startLifetime = 2, startSize = 1, startColor = Color.white }, 1);
            crystals.Emit(new ParticleSystem.EmitParams { position = center + Vector3.right * 4, startLifetime = 2, startSize = 1, startColor = Color.white }, 1);
            Tick(ice.GetComponent<TerrainAoE>());
            var particles = new ParticleSystem.Particle[100];
            int count = crystals.GetParticles(particles);
            Assert.AreEqual(3, count);
            Assert.IsTrue(particles[0].position.y < center.y - 1, "Crystals follow the downhill slope.");
            Assert.IsTrue(Mathf.Abs(particles[1].position.y - center.y) < 0.1f, "An overhead ceiling must not capture the effect.");
            Assert.IsTrue(Mathf.Abs(particles[2].position.y - (center.y + 3.04f)) < 0.1f, "Raised terrain must not swallow crystals.");
            var iceLight = ice.GetComponentInChildren<SpellElementLight>(); Tick(iceLight);
            var lights = iceLight.GetComponentsInChildren<Light>().Where(l => l.enabled).ToArray();
            Assert.AreEqual(3, lights.Length);
            for (int i = 0; i < count; i++) Assert.IsTrue(Vector3.Distance(lights[i].transform.position, particles[i].position + Vector3.up * 0.8f) < 0.001f);
            Assert.AreEqual(16f, lights[0].range);
            crystals.Clear(); Tick(iceLight);
            Assert.IsFalse(lights.Any(l => l.enabled), "Lights turn off when their particles disappear.");
            var main = crystals.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            crystals.Emit(new ParticleSystem.EmitParams { position = Vector3.right * 4, startLifetime = 2, startSize = 1, startColor = Color.white }, 1);
            Tick(ice.GetComponent<TerrainAoE>()); Tick(iceLight);
            crystals.GetParticles(particles);
            Assert.IsTrue(Mathf.Abs(crystals.transform.TransformPoint(particles[0].position).y - (center.y + 3.04f)) < 0.1f);
            Assert.IsTrue(Vector3.Distance(lights[0].transform.position, crystals.transform.TransformPoint(particles[0].position) + Vector3.up * 0.8f) < 0.001f, "Local-space particles and lights must agree.");

            earth = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Front spikes attack.prefab"), center, Quaternion.identity);
            earth.hideFlags = HideFlags.HideAndDontSave;
            earth.GetComponent<ParticleSystem>().Simulate(0.1f, true, true);
            TerrainAoE.AddTo(earth, ElementType.Earth, null, 1);
            Tick(earth.GetComponent<TerrainAoE>());
            var mesh = earth.GetComponentsInChildren<MeshFilter>().First(f => f.name.EndsWith("terrain surface")).sharedMesh;
            Assert.IsTrue(mesh.vertexCount > 6, "Flat particle quads must be subdivided to conform across the hill.");
            float span = mesh.vertices.Max(v => v.y) - mesh.vertices.Min(v => v.y);
            Assert.IsTrue(span > 1, "Ground surfaces are actually three-dimensional.");
            Assert.IsTrue(mesh.colors.Length == mesh.vertexCount && mesh.uv.Length == mesh.vertexCount, "Preserve the source particle color and texture coordinates.");

            fire = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Meteor shower.prefab"), center + Vector3.up * 20, Quaternion.identity);
            fire.hideFlags = HideFlags.HideAndDontSave;
            SpellElementLight.AddTo(fire, ElementType.Fire, true);
            var meteors = fire.GetComponent<ParticleSystem>(); meteors.Simulate(0.5f, true, true);
            var meteorLight = fire.GetComponentInChildren<SpellElementLight>(); Tick(meteorLight);
            count = meteors.GetParticles(particles);
            lights = meteorLight.GetComponentsInChildren<Light>().Where(l => l.enabled).ToArray();
            Assert.IsTrue(count > 1); Assert.AreEqual(count, lights.Length, "Every live meteor gets a light.");
            for (int i = 0; i < count; i++) Assert.IsTrue(Vector3.Distance(lights[i].transform.position, particles[i].position) < 0.001f);
            Assert.AreEqual(20f, lights[0].range);
            Assert.IsTrue(lights[0].intensity > 1.5f);
            meteors.Simulate(0.1f, true, false); Tick(meteorLight);
            count = meteors.GetParticles(particles);
            lights = meteorLight.GetComponentsInChildren<Light>().Where(l => l.enabled).ToArray();
            for (int i = 0; i < count; i++) Assert.IsTrue(Vector3.Distance(lights[i].transform.position, particles[i].position) < 0.001f, "Lights follow moving meteors.");

            projectile = new GameObject("Projectile light check");
            SpellElementLight.AddTo(projectile, ElementType.Fire, false);
            var projectileLight = projectile.GetComponentInChildren<Light>();
            Assert.AreEqual(8f, projectileLight.range); Assert.AreEqual(5f, projectileLight.intensity);
            return "PASS: slope and raised-ground crystals, ceiling rejection, subdivided textured ground mesh, trail light distribution/expiry, each moving meteor light, projectile regression.";
        }
        finally
        {
            foreach (var go in new[] { ice, earth, fire, projectile, fixture }) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
