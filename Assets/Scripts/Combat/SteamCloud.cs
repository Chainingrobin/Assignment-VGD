using System.Collections.Generic;
using UnityEngine;

public class SteamCloud : MonoBehaviour
{
    private static readonly HashSet<SteamCloud> active = new();
    [SerializeField, Min(0.1f)] private float radius = 2f;
    [SerializeField, Min(0.1f)] private float duration = 7f;
    private float expiresAt;

    private void OnEnable()
    {
        active.Add(this);
        expiresAt = Time.time + duration;
    }

    private void OnDisable() => active.Remove(this);
    private void Update()
    {
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

    public static bool BlocksSight(Vector3 start, Vector3 end)
    {
        var segment = end - start;
        foreach (var cloud in active)
        {
            if (Time.time >= cloud.expiresAt) continue;
            float fraction = segment.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(cloud.transform.position - start, segment) / segment.sqrMagnitude) : 0f;
            float worldRadius = cloud.radius * Mathf.Max(cloud.transform.lossyScale.x, cloud.transform.lossyScale.y, cloud.transform.lossyScale.z);
            if ((start + segment * fraction - cloud.transform.position).sqrMagnitude <= worldRadius * worldRadius) return true;
        }
        return false;
    }
}
