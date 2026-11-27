using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

// Wulang's body. In first person the camera sits inside her head, so a headless body mesh is drawn and
// the full body ("FullBodyShadow") only casts her shadow. Whenever another camera holds the shot (the
// intro, arrival flights, memories, the reveal), the full body is drawn instead, head and all, and the
// swap is undone once the view is back behind her eyes.
public class PlayerBodyView : MonoBehaviour
{
    private SkinnedMeshRenderer firstPerson, full;
    private CinemachineBrain brain;
    private CinemachineCamera eyes;
    private bool outside;

    private void Start()
    {
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.name == "FullBodyShadow") full = skin;
            else if (skin.sharedMesh != null && skin.sharedMesh.name.Contains("FirstPerson")) firstPerson = skin;
        }
        foreach (var camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            if (camera.transform.IsChildOf(transform) || (camera.Follow != null && camera.Follow.IsChildOf(transform))) eyes = camera;
        if (full == null || firstPerson == null || eyes == null) enabled = false;
    }

    private void LateUpdate()
    {
        if (brain == null) brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain == null) return;
        bool wanted = brain.IsBlending || !ReferenceEquals(brain.ActiveVirtualCamera, eyes);
        if (wanted == outside) return;
        outside = wanted;
        firstPerson.enabled = !outside;
        full.shadowCastingMode = outside ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
    }
}
