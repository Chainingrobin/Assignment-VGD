using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FloatingPickup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float hoverHeight = 0.12f;
    [SerializeField, Min(0f)] private float hoverSpeed = 0.4f;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Color lightColor = new Color(1f, 0.86f, 0.35f);
    [SerializeField, Min(0f)] private float lightIntensity = 2f;
    [SerializeField, Min(0f)] private float lightRange = 3f;

    private Rigidbody body;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float elapsed;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        restPosition = body.position;
        restRotation = body.rotation;

        var glow = new GameObject("PickupLight");
        glow.transform.SetParent(transform, false);
        var meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null) glow.transform.position = meshRenderer.bounds.center;
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        var book = GetComponent<BookCollect>();
        light.color = book != null ? ElementVisuals.ColorFor(book.Element) : lightColor;
        light.intensity = lightIntensity;
        light.range = lightRange;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        body.MovePosition(restPosition + Vector3.up * (Mathf.Sin(elapsed * hoverSpeed * Mathf.PI * 2f) * hoverHeight));
        if (rotationSpeed != 0f)
            body.MoveRotation(Quaternion.AngleAxis(elapsed * rotationSpeed, Vector3.up) * restRotation);
    }
}
