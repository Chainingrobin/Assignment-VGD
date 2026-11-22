using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class Interactable : MonoBehaviour
{
    [SerializeField] string prompt = "Press F to interact";
    [SerializeField] bool singleUse = true;
    [SerializeField] UnityEvent onInteract;

    bool used;
    bool outlineCreated;
    Material outlineMaterial;
    readonly List<GameObject> outlineObjects = new();

    public string Prompt => prompt;
    public void SetPrompt(string text) => prompt = text;
    // A disabled Interactable (e.g. switched off by a FlagGate) can't be used.
    public bool CanInteract => isActiveAndEnabled && !(singleUse && used);
    public UnityEvent OnInteract => onInteract;

    public void SetHighlighted(bool highlighted)
    {
        if (!highlighted)
        {
            foreach (var outline in outlineObjects)
                outline.SetActive(false);
            return;
        }

        if (!outlineCreated) CreateOutline();
        foreach (var outline in outlineObjects)
            outline.SetActive(true);
    }

    void CreateOutline()
    {
        outlineCreated = true;
        var shader = Resources.Load<Shader>("TargetOutline");
        if (shader == null)
        {
            Debug.LogError("Interactable: TargetOutline shader is missing from Resources", this);
            return;
        }

        outlineMaterial = new Material(shader);
        // ponytail: only static MeshRenderer meshes are outlined; add SkinnedMeshRenderer support if animated interactables need highlighting.
        foreach (var source in GetComponentsInChildren<MeshRenderer>(true))
        {
            var sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

            var outline = new GameObject("TargetOutline_" + source.name);
            outline.layer = source.gameObject.layer;
            outline.transform.SetParent(source.transform, false);
            outline.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            var renderer = outline.AddComponent<MeshRenderer>();
            var materialCount = Mathf.Max(sourceFilter.sharedMesh.subMeshCount, source.sharedMaterials.Length);
            var materials = new Material[materialCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outline.SetActive(false);
            outlineObjects.Add(outline);
        }
    }

    void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    public void Interact()
    {
        if (!CanInteract) return;
        used = true;
        onInteract.Invoke();
    }
}
