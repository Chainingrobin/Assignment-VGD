using UnityEngine;

// Turns this object slowly about the world's up axis (the Sanctum's drifting rocks).
public class Spin : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 2f;

    private void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
}
