using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WaterFreezer : MonoBehaviour
{
    [SerializeField] Transform ice;
    [SerializeField] Collider iceCollider;
    [SerializeField, Min(0.1f)] float duration = 4f;
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] Transform freezeOrigin;
    [SerializeField] ParticleSystem freezeParticles;
    [SerializeField] Light freezeLight;
    [SerializeField] UnityEvent onFreezeStarted = new UnityEvent();
    [SerializeField] UnityEvent onFreezeFinished = new UnityEvent();

    public UnityEvent OnFreezeStarted => onFreezeStarted;
    public UnityEvent OnFreezeFinished => onFreezeFinished;
    public float Progress { get; private set; }
    public Bounds IceBounds => iceRenderer.bounds;
    public float EdgeSoftness => iceRenderer.sharedMaterial.GetFloat("_EdgeSoftness");

    Renderer iceRenderer;
    MaterialPropertyBlock freezeProperties;
    static readonly int FreezeProgress = Shader.PropertyToID("_FreezeProgress");
    bool started;

    void Awake()
    {
        if (ice == null) { Debug.LogError("WaterFreezer: Ice is not assigned"); return; }
        iceRenderer = ice.GetComponent<Renderer>();
        if (iceRenderer == null || iceRenderer.sharedMaterial == null || !iceRenderer.sharedMaterial.HasProperty(FreezeProgress))
        {
            Debug.LogError("WaterFreezer needs an ice renderer with the Game/FreezingIce material.", this);
            enabled = false;
            return;
        }
        freezeProperties = new MaterialPropertyBlock();
        iceRenderer.GetPropertyBlock(freezeProperties);
        var bounds = ice.GetComponent<MeshFilter>().sharedMesh.bounds;
        var origin = ice.InverseTransformPoint(freezeOrigin != null ? freezeOrigin.position : ice.position);
        freezeProperties.SetVector("_FreezeOrigin", new Vector4(
            Mathf.Clamp01((origin.x - bounds.min.x) / bounds.size.x),
            Mathf.Clamp01((origin.z - bounds.min.z) / bounds.size.z), 0f, 0f));
        freezeProperties.SetVector("_FreezeSize", new Vector4(iceRenderer.bounds.size.x, iceRenderer.bounds.size.z, 0f, 0f));
        SetProgress(0f);
        ice.gameObject.SetActive(false);
        if (iceCollider) iceCollider.enabled = false;
        if (freezeParticles != null) freezeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (freezeLight != null) freezeLight.enabled = false;
    }

    public void Freeze()
    {
        Debug.Log("Freeze called");
        if (started || !enabled) return;
        if (ice == null) { Debug.LogError("WaterFreezer: Ice is not assigned", this); return; }
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Debug.Log("Freeze started");
        SetProgress(0f);
        ice.gameObject.SetActive(true);
        if (freezeParticles != null) freezeParticles.Play();
        if (freezeLight != null) freezeLight.enabled = true;
        onFreezeStarted.Invoke();

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = curve.Evaluate(t / duration);
            SetProgress(k);
            yield return null;
        }

        SetProgress(1f);
        if (iceCollider) iceCollider.enabled = true;
        if (freezeParticles != null) freezeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (freezeLight != null) freezeLight.enabled = false;
        onFreezeFinished.Invoke();
        Debug.Log("Freeze finished");
    }

    void SetProgress(float value)
    {
        Progress = Mathf.Clamp01(value);
        freezeProperties.SetFloat(FreezeProgress, Progress);
        iceRenderer.SetPropertyBlock(freezeProperties);
        var bounds = iceRenderer.bounds;
        float front = Mathf.Clamp01(Mathf.Lerp(-EdgeSoftness, 1f + EdgeSoftness, Progress));
        var position = new Vector3(Mathf.Lerp(bounds.max.x, bounds.min.x, front), bounds.center.y + .7f, bounds.center.z);
        if (freezeParticles != null) freezeParticles.transform.position = position;
        if (freezeLight != null) freezeLight.transform.position = position + Vector3.up * 3f;
    }
}
