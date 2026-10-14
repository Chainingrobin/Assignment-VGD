using UnityEngine;

[RequireComponent(typeof(FreezeRevealCutscene))]
public sealed class BiomePreview : MonoBehaviour
{
    [SerializeField] private Transform[] landmarks;
    [SerializeField] private Transform biomeRoot;
    [SerializeField, Min(1f)] private float range = 300f;
    [SerializeField, Range(1f, 45f)] private float viewAngle = 18f;
    private FreezeRevealCutscene cutscene;

    private void Awake() => cutscene = GetComponent<FreezeRevealCutscene>();

    public bool CanView(Camera camera)
    {
        if (camera == null || cutscene == null || !cutscene.CanPlay) return false;
        foreach (var landmark in landmarks)
        {
            if (landmark == null) continue;
            var direction = landmark.position - camera.transform.position;
            if (direction.magnitude > range || Vector3.Angle(camera.transform.forward, direction) > viewAngle) continue;
            // Do not offer a preview through unrelated mountains or buildings.
            if (Physics.Raycast(camera.transform.position, direction.normalized, out var hit, direction.magnitude,
                ~((1 << camera.transform.root.gameObject.layer) | (1 << 2)), QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(landmark) && (biomeRoot == null || !hit.transform.IsChildOf(biomeRoot))) continue;
            return true;
        }
        return false;
    }

    public void View() => cutscene.PlayPreview();
}
